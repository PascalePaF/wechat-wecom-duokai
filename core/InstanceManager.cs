using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WechatDuokai.Core
{
    public sealed class InstanceManager
    {
        private const uint SnapshotProcesses = 0x00000002;
        private const uint InvalidHandleValue = 0xFFFFFFFF;

        public int GetInstanceCount(AppDefinition application)
        {
            if (application == null)
            {
                return 0;
            }

            var processIds = GetApplicationProcessIds(application);
            if (processIds.Count == 0)
            {
                return 0;
            }

            // WeChat/WeCom helpers normally use different executable names. If a newer
            // version reuses the main name for child processes, count only process roots.
            var parentMap = GetParentProcessMap();
            var roots = processIds.Count(processId =>
                !parentMap.TryGetValue(processId, out var parentId) || !processIds.Contains(parentId));

            return Math.Max(1, roots);
        }

        public async Task<LaunchResult> EnsureTargetCountAsync(
            AppDefinition application,
            int targetCount,
            Action<string> reportStatus,
            CancellationToken cancellationToken)
        {
            if (application == null || !application.IsAvailable)
            {
                return new LaunchResult
                {
                    Success = false,
                    RequestedCount = targetCount,
                    Message = "未找到可用的程序文件。"
                };
            }

            targetCount = Math.Max(1, Math.Min(10, targetCount));
            var result = new LaunchResult
            {
                RequestedCount = targetCount,
                BeforeCount = GetInstanceCount(application)
            };

            if (result.BeforeCount >= targetCount)
            {
                result.Success = true;
                result.AfterCount = result.BeforeCount;
                result.Message = $"{application.DisplayName}已经运行 {result.BeforeCount} 个实例，无需补开。";
                return result;
            }

            if (application.Kind == AppKind.WeCom)
            {
                ConfigureWeComMultiInstance(targetCount);
            }

            var missing = targetCount - result.BeforeCount;
            for (var index = 0; index < missing; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentCount = GetInstanceCount(application);
                if (currentCount >= targetCount)
                {
                    break;
                }

                reportStatus?.Invoke($"正在准备第 {currentCount + 1} 个{application.DisplayName}实例…");

                var processIds = GetApplicationProcessIds(application);
                if (processIds.Count > 0)
                {
                    var unlock = await Task.Run(
                        () => WindowsHandleUnlocker.ReleaseSingleInstanceLocks(application.Kind, processIds),
                        cancellationToken);
                    result.ReleasedLockCount += unlock.ClosedHandleCount + (unlock.RemovedLockFile ? 1 : 0);
                }

                var beforeLaunch = GetInstanceCount(application);
                StartApplication(application.ExecutablePath);
                result.StartedCount++;

                reportStatus?.Invoke($"已发出启动请求，等待{application.DisplayName}窗口出现…");
                await WaitForInstanceIncreaseAsync(application, beforeLaunch, cancellationToken);
            }

            result.AfterCount = GetInstanceCount(application);
            result.Success = result.AfterCount >= targetCount;
            result.Message = result.Success
                ? $"{application.DisplayName}已达到目标数量：{result.AfterCount} 个。"
                : $"已请求补开 {result.StartedCount} 个，但目前检测到 {result.AfterCount}/{targetCount} 个。" +
                  " 如果窗口稍后出现，状态会自动刷新；若仍未补开，请尝试以管理员身份运行本工具。";
            return result;
        }

        public HashSet<int> GetApplicationProcessIds(AppDefinition application)
        {
            var result = new HashSet<int>();
            if (application == null)
            {
                return result;
            }

            var currentSession = Process.GetCurrentProcess().SessionId;
            foreach (var processName in application.ProcessNames)
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (process.SessionId != currentSession)
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
                        {
                            string runningPath;
                            try
                            {
                                runningPath = process.MainModule?.FileName;
                            }
                            catch (Exception)
                            {
                                // Fail closed: a matching file name is not enough to prove that this
                                // is the Tencent client selected by ApplicationLocator.
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(runningPath) ||
                                !PathsEqual(runningPath, application.ExecutablePath))
                            {
                                continue;
                            }
                        }

                        result.Add(process.Id);
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

        private static async Task WaitForInstanceIncreaseAsync(AppDefinition application, int previousCount, CancellationToken cancellationToken)
        {
            var manager = new InstanceManager();
            for (var attempt = 0; attempt < 32; attempt++)
            {
                await Task.Delay(250, cancellationToken);
                if (manager.GetInstanceCount(application) > previousCount)
                {
                    // Give the client enough time to establish its single-instance lock before
                    // the next loop tries to release it.
                    await Task.Delay(350, cancellationToken);
                    return;
                }
            }
        }

        private static void StartApplication(string executablePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }

        private static void ConfigureWeComMultiInstance(int targetCount)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Tencent\WXWork"))
                {
                    key?.SetValue("multi_instances", targetCount, RegistryValueKind.DWord);
                }
            }
            catch (Exception)
            {
                // The mutex release path can still work when this optional registry hint fails.
            }
        }

        private static Dictionary<int, int> GetParentProcessMap()
        {
            var result = new Dictionary<int, int>();
            var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == new IntPtr(unchecked((int)InvalidHandleValue)))
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

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
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
