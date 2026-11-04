using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WechatDuokai.Core
{
    public sealed partial class WindowsProcessEnvironment
    {
        private readonly object _shutdownGate = new object();
        private readonly Dictionary<string, ProcessSnapshot[]> _knownShutdownTargets = new Dictionary<string, ProcessSnapshot[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, AppKind, bool> _mainTrust;
        private readonly Func<string, bool> _helperTrust;

        public WindowsProcessEnvironment() { }

        // Only the test assembly can substitute trust for self-created, unsigned regression fixtures.
        internal WindowsProcessEnvironment(Func<string, AppKind, bool> mainTrust, Func<string, bool> helperTrust)
        { _mainTrust = mainTrust; _helperTrust = helperTrust; }

        private static string ShutdownKey(AppDefinition app) => app.Kind + "|" + Path.GetFullPath(app.ExecutablePath);

        public IClientShutdownSession BeginClientShutdown(AppDefinition application)
        {
            if (application == null || !application.IsAvailable) throw new InvalidOperationException("没有可核验的客户端程序。");
            ProcessSnapshot[] known;
            var key = ShutdownKey(application);
            lock (_shutdownGate) known = _knownShutdownTargets.TryGetValue(key, out var saved) ? saved : new ProcessSnapshot[0];
            return new WindowsClientShutdownSession(this, application, known, _mainTrust, _helperTrust, remaining =>
            {
                lock (_shutdownGate)
                {
                    if (remaining.Length == 0) _knownShutdownTargets.Remove(key);
                    else
                    {
                        if (_knownShutdownTargets.Count >= 16) _knownShutdownTargets.Clear();
                        _knownShutdownTargets[key] = remaining;
                    }
                }
            });
        }

        public int GetKnownClientProcessCount(AppDefinition application)
        {
            if (application == null || string.IsNullOrWhiteSpace(application.ExecutablePath)) return 0;
            ProcessSnapshot[] known;
            lock (_shutdownGate)
                if (!_knownShutdownTargets.TryGetValue(ShutdownKey(application), out known)) return 0;
            return known.Count(value =>
            {
                var live = NativeProcessControl.ReadSnapshot(value.Id, CurrentSessionId, out var unavailable);
                return unavailable || NativeProcessControl.SameIdentity(value, live);
            });
        }
    }

    internal sealed class ClientProcessPolicy : IDisposable
    {
        private readonly AppDefinition _app;
        private readonly Func<string, AppKind, bool> _mainTrust;
        private readonly Func<string, bool> _helperTrust;
        private readonly Dictionary<string, FileStream> _leases = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _helperNames;

        internal ClientProcessPolicy(AppDefinition app, Func<string, AppKind, bool> mainTrust = null, Func<string, bool> helperTrust = null)
        {
            _app = app;
            _mainTrust = mainTrust ?? ((path, kind) => ClientExecutableValidator.Validate(path, kind).IsValid);
            _helperTrust = helperTrust ?? (path => ClientExecutableValidator.HasValidTencentSignature(path, out _));
            var specific = app.Kind == AppKind.WeChat
                ? new[] { "WeChatAppEx", "WeChatPlayer", "WeChatUtility", "WeChatOCR", "TencentOCR", "WeChatBrowser", "WeixinCrashHandler" }
                : app.Kind == AppKind.WeCom
                    ? new[] { "WXWorkWeb", "WXWorkWebService", "WXWorkMedia", "WXWorkGPU", "WXWorkBrowser", "WXWorkUtility", "WeComWeb",
                        "WXWorkCommand", "WXWorkImageDecoder", "WXWorkPlayer", "WXWorkPlayerHD", "FlutterPlugins", "wecom-agent",
                        "crashpad_handler_extension", "anr", "imageutil", "minidump", "minidump64", "miniwalkstack", "TxBugReport",
                        "UxEnhanceHost", "wironboxd32", "wironboxd64" }
                    : new[] { "QQCrashReport", "QQGuild", "QQAppEx", "QQEX" };
            _helperNames = new HashSet<string>(specific.Concat(new[] { "crashpad_handler", "QtWebEngineProcess" }), StringComparer.OrdinalIgnoreCase);
        }

        internal IEnumerable<string> Names => _helperNames.Concat(_app.ProcessNames);
        internal bool IsMain(ProcessSnapshot process) => NativeProcessControl.PathsEqual(process.ExecutablePath, _app.ExecutablePath);

        internal bool IsTrusted(ProcessSnapshot process)
        {
            var path = process.ExecutablePath;
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (_leases.ContainsKey(path)) return true;
            if (_rejected.Contains(path) || _leases.Count >= 48) return false;
            FileStream lease = null;
            try
            {
                var main = IsMain(process);
                var name = Path.GetFileNameWithoutExtension(path);
                var nestedMain = _app.ProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                    Path.GetFullPath(path).StartsWith(Path.GetDirectoryName(Path.GetFullPath(_app.ExecutablePath)) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase);
                if (!main && !_helperNames.Contains(name) && !nestedMain) return false;
                LocalFileSafety.RejectReparsePoints(path);
                // Prevent replacing the validated image while this shutdown session is using its proof.
                lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!(main || nestedMain ? _mainTrust(path, _app.Kind) : _helperTrust(path))) return false;
                _leases[path] = lease;
                lease = null;
                return true;
            }
            catch (Exception) { return false; }
            finally { lease?.Dispose(); if (!_leases.ContainsKey(path)) _rejected.Add(path); }
        }

        public void Dispose() { foreach (var lease in _leases.Values) lease.Dispose(); _leases.Clear(); }
    }

    internal static class ClientProcessGraph
    {
        internal static IReadOnlyList<ProcessSnapshot> Select(AppDefinition app, int session,
            IReadOnlyList<ProcessSnapshot> processes, IReadOnlyDictionary<int, int> parents,
            IReadOnlyList<ProcessSnapshot> known, Func<ProcessSnapshot, bool> heldProof,
            Func<ProcessSnapshot, bool> trusted, out int unverified)
        {
            var live = processes.Where(value => value != null && value.Id > 0 && value.SessionId == session &&
                value.IsCurrentUser && value.CreationTimeFileTime > 0).GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
            var ancestors = new Dictionary<int, ProcessSnapshot>();
            var selected = new Dictionary<int, ProcessSnapshot>();
            var rejected = new HashSet<int>();
            foreach (var process in live.Values.Where(value => NativeProcessControl.PathsEqual(value.ExecutablePath, app.ExecutablePath)))
                if (trusted(process)) ancestors[process.Id] = selected[process.Id] = process;
                else rejected.Add(process.Id);
            foreach (var process in known ?? new ProcessSnapshot[0])
            {
                if (process.SessionId != session || !process.IsCurrentUser || !trusted(process)) continue;
                if (live.TryGetValue(process.Id, out var current))
                {
                    if (NativeProcessControl.SameIdentity(process, current)) ancestors[current.Id] = selected[current.Id] = current;
                }
                else if (heldProof(process)) ancestors[process.Id] = process; // Retired parent's handle still proves its identity.
            }
            for (var depth = 0; depth < 16 && selected.Count < 256; depth++)
            {
                var added = false;
                foreach (var child in live.Values.Where(value => !ancestors.ContainsKey(value.Id)))
                {
                    if (!parents.TryGetValue(child.Id, out var parentId) || parentId == child.Id ||
                        !ancestors.TryGetValue(parentId, out var parent) || child.CreationTimeFileTime < parent.CreationTimeFileTime) continue;
                    if (!trusted(child)) { rejected.Add(child.Id); continue; }
                    ancestors[child.Id] = selected[child.Id] = child;
                    added = true;
                    if (selected.Count >= 256) break;
                }
                if (!added) break;
            }
            foreach (var process in processes.Where(value => value != null && value.SessionId == session && value.InspectionUnavailable))
                if (app.ProcessNames.Contains(process.ProcessName ?? string.Empty, StringComparer.OrdinalIgnoreCase) ||
                    (parents.TryGetValue(process.Id, out var parent) && ancestors.ContainsKey(parent)) ||
                    (known != null && known.Any(value => value.Id == process.Id))) rejected.Add(process.Id);
            unverified = rejected.Count;
            return selected.Values.ToArray();
        }
    }

    internal sealed class WindowsClientShutdownSession : IClientShutdownSession
    {
        private readonly WindowsProcessEnvironment _environment;
        private readonly AppDefinition _app;
        private readonly ClientProcessPolicy _policy;
        private readonly Action<ProcessSnapshot[]> _saveRemaining;
        private readonly Dictionary<string, ProcessSnapshot> _known = new Dictionary<string, ProcessSnapshot>();
        private readonly Dictionary<string, NativeProcessLease> _held = new Dictionary<string, NativeProcessLease>();
        private readonly HashSet<string> _forced = new HashSet<string>(), _reported = new HashSet<string>(), _denied = new HashSet<string>();
        private ClientShutdownState _last = new ClientShutdownState();

        internal WindowsClientShutdownSession(WindowsProcessEnvironment environment, AppDefinition app, ProcessSnapshot[] known,
            Func<string, AppKind, bool> mainTrust, Func<string, bool> helperTrust, Action<ProcessSnapshot[]> saveRemaining)
        {
            _environment = environment; _app = app; _saveRemaining = saveRemaining;
            _policy = new ClientProcessPolicy(app, mainTrust, helperTrust);
            foreach (var item in known) _known[Key(item)] = item;
        }

        private static string Key(ProcessSnapshot snapshot) => snapshot.Id + ":" + snapshot.CreationTimeFileTime;

        public ClientShutdownState Refresh()
        {
            try
            {
                var snapshots = _environment.FindProcesses(_policy.Names);
                var parents = _environment.GetParentProcessMap();
                if (snapshots.Count > 0 && parents.Count == 0) return _last = new ClientShutdownState { InspectionFailed = true };
                var selected = ClientProcessGraph.Select(_app, _environment.CurrentSessionId, snapshots, parents,
                    _known.Values.ToArray(), snapshot => _held.TryGetValue(Key(snapshot), out var lease) &&
                        NativeProcessControl.SameIdentity(snapshot, lease.Snapshot), _policy.IsTrusted, out var unverified);
                // A live held handle is stronger evidence than a transient omission from process enumeration.
                selected = selected.Concat(_held.Values.Where(value => !value.IsExited).Select(value => value.Snapshot))
                    .GroupBy(Key).Select(group => group.First()).ToArray();
                foreach (var snapshot in selected)
                {
                    var key = Key(snapshot);
                    _known[key] = snapshot;
                    if (_held.ContainsKey(key)) continue;
                    if (_held.Count >= 256) return _last = new ClientShutdownState { InspectionFailed = true };
                    var held = NativeProcessControl.OpenVerified(snapshot, _environment.CurrentSessionId, out var error);
                    if (held == null)
                    {
                        if (error == 5) _denied.Add(key);
                        held = NativeProcessControl.OpenVerified(snapshot, _environment.CurrentSessionId, out _, false);
                    }
                    if (held != null) _held[key] = held;
                }
                var ids = new HashSet<int>(selected.Where(_policy.IsMain).Select(value => value.Id));
                var windows = NativeProcessControl.VisibleWindows();
                return _last = new ClientShutdownState
                {
                    Processes = selected, UnverifiedProcessCount = unverified,
                    InstanceCount = ids.Count(id => !parents.TryGetValue(id, out var parent) || !ids.Contains(parent)),
                    WindowInspectionSucceeded = windows != null,
                    VisibleWindowCount = windows == null ? 0 : selected.Sum(value => windows.TryGetValue(value.Id, out var count) ? count : 0)
                };
            }
            catch (Exception) { return _last = new ClientShutdownState { InspectionFailed = true }; }
        }

        public ProcessCloseSummary CloseCurrentProcesses(TimeSpan gracefulTimeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = Refresh();
            if (state.InspectionFailed) return new ProcessCloseSummary();
            var targets = _held.Values.Where(value => !value.IsExited).ToArray();
            NativeProcessControl.RequestClose(targets);
            NativeProcessControl.Wait(targets, gracefulTimeout, cancellationToken);
            // Capture late children, including children of roots that just exited gracefully.
            Refresh();
            targets = _held.Values.Where(value => !value.IsExited)
                .OrderBy(value => _policy.IsMain(value.Snapshot) ? 1 : 0)
                .ThenByDescending(value => value.Snapshot.CreationTimeFileTime).ToArray();
            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (target.IsExited) continue;
                var key = Key(target.Snapshot);
                if (target.Terminate()) _forced.Add(key);
                else if (!target.IsExited && (!target.CanTerminate || Marshal.GetLastWin32Error() == 5)) _denied.Add(key);
            }
            NativeProcessControl.Wait(targets, TimeSpan.FromSeconds(2), cancellationToken);
            var summary = new ProcessCloseSummary { EligibleProcessCount = targets.Length };
            foreach (var pair in _held.Where(value => value.Value.IsExited && !_reported.Contains(value.Key)))
            {
                _reported.Add(pair.Key);
                if (_forced.Contains(pair.Key)) summary.ForcedExitCount++;
                else summary.GracefulExitCount++;
            }
            var after = Refresh();
            summary.RemainingProcessCount = after.Processes.Count;
            summary.AccessDeniedProcessCount = after.Processes.Count(value => _denied.Contains(Key(value)));
            return summary;
        }

        public void Dispose()
        {
            try { _saveRemaining(_last.Processes.Concat(_held.Values.Where(value => !value.IsExited).Select(value => value.Snapshot))
                .Concat(_last.InspectionFailed || _last.UnverifiedProcessCount > 0 ? _known.Values : Enumerable.Empty<ProcessSnapshot>())
                .GroupBy(Key).Select(group => group.First()).Take(256).ToArray()); }
            finally { foreach (var handle in _held.Values) handle.Dispose(); _policy.Dispose(); }
        }
    }
}
