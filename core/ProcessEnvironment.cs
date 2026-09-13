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
    }

    public interface IProcessEnvironment
    {
        int CurrentSessionId { get; }

        IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> processNames);

        IReadOnlyDictionary<int, int> GetParentProcessMap();

        void StartApplication(string executablePath);

        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The only production implementation that talks directly to System.Diagnostics
    /// and the Toolhelp process snapshot API.
    /// </summary>
    public sealed class WindowsProcessEnvironment : IProcessEnvironment
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
            foreach (var processName in (processNames ?? Enumerable.Empty<string>())
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (!seen.Add(process.Id))
                        {
                            continue;
                        }

                        string executablePath = null;
                        try
                        {
                            executablePath = process.MainModule?.FileName;
                        }
                        catch (Exception)
                        {
                            // A protected/elevated process is represented without a path.
                            // Consumers fail closed when an exact path is required.
                        }

                        result.Add(new ProcessSnapshot
                        {
                            Id = process.Id,
                            SessionId = process.SessionId,
                            ExecutablePath = executablePath
                        });
                    }
                    catch (Exception)
                    {
                        // A process can exit between enumeration and inspection.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
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
