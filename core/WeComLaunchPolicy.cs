using System;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace WechatDuokai.Core
{
    /// <summary>
    /// Defines the short-lived environment used while WeCom instances are started.
    /// Implementations must restore any user setting when the returned scope is disposed.
    /// </summary>
    public interface IWeComLaunchPolicy
    {
        IDisposable BeginLaunchSession(int targetCount);
    }

    /// <summary>
    /// Uses WeCom's documented current-user double-instance hint for two windows. For
    /// three or more windows, the hint is temporarily removed because current WeCom
    /// releases cap that registry path at two. The caller's original value is restored
    /// exactly after the launch loop, including its original registry value kind. Restore
    /// is conditional: if another program changes the value while the session is active,
    /// that newer external value is preserved.
    /// </summary>
    internal sealed class WindowsWeComLaunchPolicy : IWeComLaunchPolicy
    {
        private const string DefaultRegistryPath = @"SOFTWARE\Tencent\WXWork";
        private const string ValueName = "multi_instances";
        private const string DefaultGateName = @"Local\WechatDuokai.WeComRegistryPolicy";
        private readonly string _registryPath;
        private readonly string _gateName;

        internal WindowsWeComLaunchPolicy()
            : this(DefaultRegistryPath, DefaultGateName)
        {
        }

        internal WindowsWeComLaunchPolicy(string registryPath, string gateName)
        {
            _registryPath = registryPath ?? throw new ArgumentNullException(nameof(registryPath));
            _gateName = gateName ?? throw new ArgumentNullException(nameof(gateName));
        }

        public IDisposable BeginLaunchSession(int targetCount)
        {
            if (targetCount <= 1)
            {
                return EmptyScope.Instance;
            }

            Semaphore gate = null;
            var ownsGate = false;
            RegistryKey key = null;
            RegistryValueState originalState = null;
            RegistryValueState temporaryState = null;
            try
            {
                gate = new Semaphore(1, 1, _gateName);
                ownsGate = gate.WaitOne(TimeSpan.FromSeconds(3));

                if (!ownsGate)
                {
                    gate.Dispose();
                    return EmptyScope.Instance;
                }

                key = Registry.CurrentUser.CreateSubKey(_registryPath);
                if (key == null)
                {
                    ReleaseGate(gate, ownsGate);
                    return EmptyScope.Instance;
                }

                originalState = ReadValueState(key);

                if (targetCount == 2)
                {
                    key.SetValue(ValueName, 2, RegistryValueKind.DWord);
                    temporaryState = new RegistryValueState(true, 2, RegistryValueKind.DWord);
                }
                else
                {
                    // On WeCom 5.0.11.6018, values above two still cap the client at
                    // two windows. Removing the hint during the exact mutex-release loop
                    // allows a third instance without modifying or injecting into WeCom.
                    key.DeleteValue(ValueName, false);
                    temporaryState = RegistryValueState.Missing;
                }

                return new RegistryRestoreScope(key, gate, ownsGate, originalState, temporaryState);
            }
            catch (Exception)
            {
                if (key != null && originalState != null && temporaryState != null)
                {
                    RestoreValueIfUnchanged(key, originalState, temporaryState);
                }
                key?.Dispose();
                ReleaseGate(gate, ownsGate);
                // Registry preparation is a compatibility aid. The exact mutex path can
                // still succeed, so a locked or unavailable registry must not crash launch.
                return EmptyScope.Instance;
            }
        }

        private static RegistryValueState ReadValueState(RegistryKey key)
        {
            var exists = key.GetValueNames().Any(name =>
                string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                return RegistryValueState.Missing;
            }

            return new RegistryValueState(true,
                key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                key.GetValueKind(ValueName));
        }

        private static bool RestoreValueIfUnchanged(RegistryKey key, RegistryValueState originalState,
            RegistryValueState expectedTemporaryState)
        {
            try
            {
                var currentState = ReadValueState(key);
                if (!RegistryValueState.AreEqual(currentState, expectedTemporaryState))
                {
                    // Another program has written a newer policy while our launch scope was
                    // active. Its value takes precedence over our stale snapshot.
                    return false;
                }

                if (originalState.Exists)
                {
                    key.SetValue(ValueName, originalState.Value, originalState.Kind);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }

                return true;
            }
            catch (Exception)
            {
                // Best effort: the user-scoped key may have been locked or its access
                // changed by another process after this launch session began.
                return false;
            }
        }

        private static void ReleaseGate(Semaphore gate, bool ownsGate)
        {
            if (gate == null)
            {
                return;
            }

            if (ownsGate)
            {
                try
                {
                    gate.Release();
                }
                catch (SemaphoreFullException)
                {
                }
            }

            gate.Dispose();
        }

        private sealed class RegistryRestoreScope : IDisposable
        {
            private RegistryKey _key;
            private Semaphore _gate;
            private bool _ownsGate;
            private readonly RegistryValueState _originalState;
            private readonly RegistryValueState _temporaryState;

            internal RegistryRestoreScope(RegistryKey key, Semaphore gate, bool ownsGate,
                RegistryValueState originalState, RegistryValueState temporaryState)
            {
                _key = key;
                _gate = gate;
                _ownsGate = ownsGate;
                _originalState = originalState;
                _temporaryState = temporaryState;
            }

            public void Dispose()
            {
                var key = Interlocked.Exchange(ref _key, null);
                var gate = Interlocked.Exchange(ref _gate, null);
                var ownsGate = _ownsGate;
                _ownsGate = false;

                try
                {
                    if (key != null)
                    {
                        RestoreValueIfUnchanged(key, _originalState, _temporaryState);
                    }
                }
                catch (Exception)
                {
                    // Best-effort restore. The scope never hides the original launch result.
                }
                finally
                {
                    key?.Dispose();
                    ReleaseGate(gate, ownsGate);
                }
            }
        }

        private sealed class RegistryValueState
        {
            internal static readonly RegistryValueState Missing =
                new RegistryValueState(false, null, RegistryValueKind.None);

            internal RegistryValueState(bool exists, object value, RegistryValueKind kind)
            {
                Exists = exists;
                Value = value;
                Kind = kind;
            }

            internal bool Exists { get; }

            internal object Value { get; }

            internal RegistryValueKind Kind { get; }

            internal static bool AreEqual(RegistryValueState left, RegistryValueState right)
            {
                if (left == null || right == null || left.Exists != right.Exists)
                {
                    return false;
                }

                if (!left.Exists)
                {
                    return true;
                }

                if (left.Kind != right.Kind)
                {
                    return false;
                }

                var leftBytes = left.Value as byte[];
                var rightBytes = right.Value as byte[];
                if (leftBytes != null || rightBytes != null)
                {
                    return leftBytes != null && rightBytes != null && leftBytes.SequenceEqual(rightBytes);
                }

                var leftStrings = left.Value as string[];
                var rightStrings = right.Value as string[];
                if (leftStrings != null || rightStrings != null)
                {
                    return leftStrings != null && rightStrings != null && leftStrings.SequenceEqual(rightStrings);
                }

                return Equals(left.Value, right.Value);
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();

            public void Dispose()
            {
            }
        }
    }
}
