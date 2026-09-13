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
    /// exactly after the launch loop, including its original registry value kind.
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
            var snapshotReady = false;
            var hadValue = false;
            object originalValue = null;
            var originalKind = RegistryValueKind.None;
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

                hadValue = key.GetValueNames().Any(name =>
                    string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase));
                originalValue = hadValue
                    ? key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                    : null;
                originalKind = hadValue ? key.GetValueKind(ValueName) : RegistryValueKind.None;
                snapshotReady = true;

                if (targetCount == 2)
                {
                    key.SetValue(ValueName, 2, RegistryValueKind.DWord);
                }
                else
                {
                    // On WeCom 5.0.11.6018, values above two still cap the client at
                    // two windows. Removing the hint during the exact mutex-release loop
                    // allows a third instance without modifying or injecting into WeCom.
                    key.DeleteValue(ValueName, false);
                }

                return new RegistryRestoreScope(key, gate, ownsGate, hadValue, originalValue, originalKind);
            }
            catch (Exception)
            {
                if (key != null && snapshotReady)
                {
                    RestoreValue(key, hadValue, originalValue, originalKind);
                }
                key?.Dispose();
                ReleaseGate(gate, ownsGate);
                // Registry preparation is a compatibility aid. The exact mutex path can
                // still succeed, so a locked or unavailable registry must not crash launch.
                return EmptyScope.Instance;
            }
        }

        private static void RestoreValue(RegistryKey key, bool hadValue, object originalValue,
            RegistryValueKind originalKind)
        {
            try
            {
                if (hadValue)
                {
                    key.SetValue(ValueName, originalValue, originalKind);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception)
            {
                // Best effort: the user-scoped key may have been locked or its access
                // changed by another process after this launch session began.
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
            private readonly bool _hadValue;
            private readonly object _originalValue;
            private readonly RegistryValueKind _originalKind;

            internal RegistryRestoreScope(RegistryKey key, Semaphore gate, bool ownsGate, bool hadValue,
                object originalValue, RegistryValueKind originalKind)
            {
                _key = key;
                _gate = gate;
                _ownsGate = ownsGate;
                _hadValue = hadValue;
                _originalValue = originalValue;
                _originalKind = originalKind;
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
                        RestoreValue(key, _hadValue, _originalValue, _originalKind);
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

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();

            public void Dispose()
            {
            }
        }
    }
}
