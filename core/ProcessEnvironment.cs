using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    /// <summary>
    /// A read-only description of one Windows process. Keeping Process out of the
    /// orchestration layer makes PID reuse, parent/child grouping and test doubles explicit.
    /// </summary>
    public sealed class ProcessSnapshot
    {
        public int Id { get; set; }

        public int SessionId { get; set; }

        public string ExecutablePath { get; set; }

        public long CreationTimeFileTime { get; set; }

        public bool IsCurrentUser { get; set; } = true;

        public string ProcessName { get; set; }

        public bool InspectionUnavailable { get; set; }
    }

    public sealed class ProcessCloseSummary
    {
        public int EligibleProcessCount { get; set; }

        public int GracefulExitCount { get; set; }

        public int ForcedExitCount { get; set; }

        public int RemainingProcessCount { get; set; }

        public int AccessDeniedProcessCount { get; set; }
    }

    public interface IProcessEnvironment
    {
        int CurrentSessionId { get; }

        IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> processNames);

        IReadOnlyDictionary<int, int> GetParentProcessMap();

        void StartApplication(string executablePath);

        ProcessCloseSummary CloseProcesses(IReadOnlyCollection<int> processIds,
            string expectedExecutablePath, TimeSpan gracefulTimeout,
            CancellationToken cancellationToken);

        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The only production implementation that talks directly to System.Diagnostics
    /// and the Toolhelp process snapshot API.
    /// </summary>
    public sealed partial class WindowsProcessEnvironment : IProcessEnvironment, IClientShutdownEnvironment
    {
        private const uint SnapshotProcesses = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public int CurrentSessionId
        {
            get
            {
                using (var process = Process.GetCurrentProcess())
                {
                    return process.SessionId;
                }
            }
        }

        public IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> processNames)
        {
            var result = new List<ProcessSnapshot>();
            var seen = new HashSet<int>();
            var names = new HashSet<string>((processNames ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
            var currentSession = CurrentSessionId;
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!names.Contains(process.ProcessName) || !seen.Add(process.Id)) continue;
                    var snapshot = NativeProcessControl.ReadSnapshot(process.Id, currentSession, out var unavailable);
                    if (snapshot != null) result.Add(snapshot);
                    else if (unavailable && process.SessionId == currentSession)
                        result.Add(new ProcessSnapshot { Id = process.Id, SessionId = currentSession,
                            ProcessName = process.ProcessName, IsCurrentUser = false, InspectionUnavailable = true });
                }
                catch (Exception) { /* Processes may exit during enumeration. */ }
                finally { process.Dispose(); }
            }

            return result;
        }

        public IReadOnlyDictionary<int, int> GetParentProcessMap()
        {
            var result = new Dictionary<int, int>();
            var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == InvalidHandleValue)
            {
                return result;
            }

            try
            {
                var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32)) };
                if (!Process32First(snapshot, ref entry))
                {
                    return result;
                }

                do
                {
                    result[unchecked((int)entry.ProcessId)] = unchecked((int)entry.ParentProcessId);
                    entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                }
                while (Process32Next(snapshot, ref entry));
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return result;
        }

        public void StartApplication(string executablePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            });
        }

        public ProcessCloseSummary CloseProcesses(IReadOnlyCollection<int> processIds,
            string expectedExecutablePath, TimeSpan gracefulTimeout,
            CancellationToken cancellationToken)
        {
            return NativeProcessControl.CloseExact(processIds, expectedExecutablePath,
                CurrentSessionId, gracefulTimeout, cancellationToken);
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int BasePriority;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
