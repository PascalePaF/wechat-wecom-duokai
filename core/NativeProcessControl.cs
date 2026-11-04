using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WechatDuokai.Core
{
    // All identity checks and termination use the same held kernel handle, never a later PID lookup.
    internal sealed class NativeProcessLease : IDisposable
    {
        internal SafeProcessHandle Handle { get; }
        internal ProcessSnapshot Snapshot { get; }
        internal bool CanTerminate { get; }
        internal bool IsExited => NativeProcessControl.WaitForSingleObject(Handle, 0) == 0;
        internal NativeProcessLease(SafeProcessHandle handle, ProcessSnapshot snapshot, bool canTerminate)
        { Handle = handle; Snapshot = snapshot; CanTerminate = canTerminate; }
        internal bool Terminate() => IsExited || (CanTerminate && NativeProcessControl.TerminateProcess(Handle, 1));
        public void Dispose() => Handle.Dispose();
    }

    internal static class NativeProcessControl
    {
        private const uint Query = 0x1000, Synchronize = 0x100000, TerminateRight = 1;
        private static readonly SecurityIdentifier CurrentUser = ReadCurrentUser();

        private static SecurityIdentifier ReadCurrentUser()
        {
            using (var identity = WindowsIdentity.GetCurrent()) return identity.User;
        }

        internal static bool PathsEqual(string left, string right)
        {
            try { return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
                string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
        }

        internal static bool SameIdentity(ProcessSnapshot left, ProcessSnapshot right) => left != null && right != null &&
            left.Id == right.Id && left.SessionId == right.SessionId && left.IsCurrentUser && right.IsCurrentUser &&
            left.CreationTimeFileTime > 0 && left.CreationTimeFileTime == right.CreationTimeFileTime &&
            PathsEqual(left.ExecutablePath, right.ExecutablePath);

        internal static ProcessSnapshot ReadSnapshot(int id, int session)
            => ReadSnapshot(id, session, out _);

        internal static ProcessSnapshot ReadSnapshot(int id, int session, out bool unavailable)
        {
            unavailable = false;
            if (id <= 0) return null;
            using (var handle = OpenProcess(Query | Synchronize, false, id))
            {
                if (handle.IsInvalid) { unavailable = Marshal.GetLastWin32Error() == 5; return null; }
                return ReadSnapshot(handle, id, session, out unavailable);
            }
        }

        private static ProcessSnapshot ReadSnapshot(SafeProcessHandle handle, int id, int session, out bool unavailable)
        {
            unavailable = false;
            var wait = WaitForSingleObject(handle, 0);
            if (wait != 258) { unavailable = wait != 0; return null; }
            if (!ProcessIdToSessionId((uint)id, out var actualSession)) { unavailable = true; return null; }
            if (actualSession != session) return null;
            var sameUser = IsSameUser(handle);
            if (sameUser != true) { unavailable = !sameUser.HasValue; return null; }
            var path = new StringBuilder(32768);
            var length = path.Capacity;
            if (!QueryFullProcessImageName(handle, 0, path, ref length) ||
                !GetProcessTimes(handle, out var created, out _, out _, out _)) { unavailable = true; return null; }
            // A process can finish while its metadata is being read; do not publish a zombie snapshot.
            if (WaitForSingleObject(handle, 0) != 258) return null;
            return new ProcessSnapshot { Id = id, SessionId = session, ExecutablePath = path.ToString(),
                CreationTimeFileTime = created, IsCurrentUser = true };
        }

        internal static NativeProcessLease OpenVerified(ProcessSnapshot expected, int session, out int error, bool terminate = true)
        {
            error = 0;
            if (expected == null || expected.Id <= 0 || expected.CreationTimeFileTime <= 0) return null;
            var handle = OpenProcess(Query | Synchronize | (terminate ? TerminateRight : 0), false, expected.Id);
            if (handle.IsInvalid) { error = Marshal.GetLastWin32Error(); handle.Dispose(); return null; }
            var live = ReadSnapshot(handle, expected.Id, session, out _);
            if (!SameIdentity(expected, live)) { handle.Dispose(); return null; }
            return new NativeProcessLease(handle, live, terminate);
        }

        private static bool? IsSameUser(SafeProcessHandle process)
        {
            if (CurrentUser == null || !OpenProcessToken(process, 8, out var token)) return null;
            using (token)
            {
                GetTokenInformation(token, 1, IntPtr.Zero, 0, out var bytes);
                if (bytes <= 0 || bytes > 65536) return null;
                var memory = Marshal.AllocHGlobal(bytes);
                try
                {
                    if (!GetTokenInformation(token, 1, memory, bytes, out _)) return null;
                    return CurrentUser.Equals(new SecurityIdentifier(Marshal.ReadIntPtr(memory)));
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
        }

        internal static IReadOnlyDictionary<int, int> VisibleWindows()
        {
            var result = new Dictionary<int, int>();
            return EnumWindows((window, state) =>
            {
                if (IsWindowVisible(window))
                {
                    GetWindowThreadProcessId(window, out var id);
                    var key = unchecked((int)id);
                    result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
                }
                return true;
            }, IntPtr.Zero) ? result : null;
        }

        internal static void RequestClose(IReadOnlyCollection<NativeProcessLease> targets)
        {
            var byId = targets.Where(value => !value.IsExited).ToDictionary(value => value.Snapshot.Id);
            EnumWindows((window, state) =>
            {
                GetWindowThreadProcessId(window, out var id);
                if (IsWindowVisible(window) && byId.TryGetValue(unchecked((int)id), out var target) && !target.IsExited)
                {
                    // Asynchronous WM_CLOSE avoids blocking indefinitely on a hung client window.
                    GetWindowThreadProcessId(window, out var recheckedId);
                    if (recheckedId == id) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }

        internal static void Wait(IReadOnlyCollection<NativeProcessLease> targets, TimeSpan timeout, CancellationToken token)
        {
            var milliseconds = Math.Max(0, Math.Min(5000, timeout.TotalMilliseconds));
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalMilliseconds < milliseconds && targets.Any(value => !value.IsExited))
            {
                token.ThrowIfCancellationRequested();
                if (token.WaitHandle.WaitOne(60)) token.ThrowIfCancellationRequested();
            }
        }

        internal static ProcessCloseSummary CloseExact(IReadOnlyCollection<int> ids, string path, int session,
            TimeSpan graceful, CancellationToken token)
        {
            var result = new ProcessCloseSummary();
            var targets = new List<NativeProcessLease>();
            try
            {
                foreach (var id in (ids ?? new int[0]).Where(value => value > 0).Distinct())
                {
                    token.ThrowIfCancellationRequested();
                    var snapshot = ReadSnapshot(id, session);
                    if (snapshot == null || !PathsEqual(snapshot.ExecutablePath, path)) continue;
                    var target = OpenVerified(snapshot, session, out var error);
                    if (target != null) targets.Add(target);
                    else if (error == 5) result.AccessDeniedProcessCount++;
                }
                result.EligibleProcessCount = targets.Count;
                RequestClose(targets);
                Wait(targets, graceful, token);
                result.GracefulExitCount = targets.Count(value => value.IsExited);
                foreach (var target in targets.Where(value => !value.IsExited))
                {
                    token.ThrowIfCancellationRequested();
                    if (!target.Terminate() && Marshal.GetLastWin32Error() == 5) result.AccessDeniedProcessCount++;
                }
                Wait(targets, TimeSpan.FromSeconds(2), token);
                result.RemainingProcessCount = targets.Count(value => !value.IsExited);
                result.ForcedExitCount = targets.Count(value => value.IsExited) - result.GracefulExitCount;
                return result;
            }
            finally { foreach (var target in targets) target.Dispose(); }
        }

        private sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeTokenHandle() : base(true) { }
            protected override bool ReleaseHandle() => CloseHandle(handle);
        }
        private delegate bool WindowCallback(IntPtr window, IntPtr state);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool TerminateProcess(SafeProcessHandle handle, uint code);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle handle, uint flags, StringBuilder name, ref int length);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ProcessIdToSessionId(uint id, out int session);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeTokenHandle token);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(SafeTokenHandle token, int informationClass, IntPtr memory, int length, out int required);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
