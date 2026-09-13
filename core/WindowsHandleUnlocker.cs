using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WechatDuokai.Core
{
    /// <summary>
    /// Releases only the documented, product-specific single-instance handles used by
    /// WeChat/WeCom. It does not inject code, patch vendor files, or terminate processes.
    /// </summary>
    internal static class WindowsHandleUnlocker
    {
        private const int SystemExtendedHandleInformation = 64;
        private const int ObjectNameInformation = 1;
        private const int ObjectTypeInformation = 2;
        private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
        private const uint ProcessDuplicateHandle = 0x0040;
        private const uint DuplicateCloseSource = 0x00000001;
        private const uint DuplicateSameAccess = 0x00000002;
        private const uint FileTypeDisk = 0x0001;

        public static UnlockAttempt ReleaseSingleInstanceLocks(AppKind kind, IEnumerable<int> applicationProcessIds)
        {
            return ReleaseSingleInstanceLocks(kind, applicationProcessIds,
                kind == AppKind.WeChat ? GetModernWeChatLockFile() : null);
        }

        internal static UnlockAttempt ReleaseSingleInstanceLocks(AppKind kind, IEnumerable<int> applicationProcessIds, string lockFile)
        {
            var allowedProcessIds = new HashSet<int>(applicationProcessIds ?? Enumerable.Empty<int>());
            var lockFileOnlyProcessIds = new HashSet<int>();
            var result = new UnlockAttempt();

            if (!string.IsNullOrWhiteSpace(lockFile) && File.Exists(lockFile))
            {
                try
                {
                    File.Delete(lockFile);
                    result.RemovedLockFile = true;
                }
                catch (IOException)
                {
                    AddRundll32ProcessesInCurrentSession(allowedProcessIds, lockFileOnlyProcessIds);
                }
                catch (UnauthorizedAccessException)
                {
                    AddRundll32ProcessesInCurrentSession(allowedProcessIds, lockFileOnlyProcessIds);
                }
            }

            if (allowedProcessIds.Count == 0)
            {
                return result;
            }

            var handles = QuerySystemHandles();
            var processHandles = new Dictionary<int, IntPtr>();

            try
            {
                foreach (var handle in handles)
                {
                    if (!allowedProcessIds.Contains(handle.ProcessId))
                    {
                        continue;
                    }

                    if (!processHandles.TryGetValue(handle.ProcessId, out var sourceProcess))
                    {
                        sourceProcess = OpenProcess(ProcessDuplicateHandle, false, handle.ProcessId);
                        processHandles[handle.ProcessId] = sourceProcess;
                        if (sourceProcess == IntPtr.Zero)
                        {
                            result.InaccessibleProcessCount++;
                            continue;
                        }
                    }

                    if (sourceProcess == IntPtr.Zero ||
                        !DuplicateHandleCopy(sourceProcess, handle.HandleValue, GetCurrentProcess(), out var localHandle, 0, false, DuplicateSameAccess))
                    {
                        continue;
                    }

                    var matches = false;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(lockFile) && GetFileType(localHandle) == FileTypeDisk)
                        {
                            var openPath = GetPathFromHandle(localHandle);
                            matches = PathsEqual(openPath, lockFile);
                        }

                        // rundll32 is considered only for the exact modern WeChat lock-file path.
                        // Never apply mutex-name matching to an unrelated helper process.
                        if (!matches && !lockFileOnlyProcessIds.Contains(handle.ProcessId))
                        {
                            var typeName = QueryObjectString(localHandle, ObjectTypeInformation);
                            if (string.Equals(typeName, "Mutant", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(typeName, "Mutex", StringComparison.OrdinalIgnoreCase))
                            {
                                var objectName = QueryObjectString(localHandle, ObjectNameInformation);
                                matches = IsKnownMutex(kind, objectName);
                            }
                        }
                    }
                    finally
                    {
                        CloseHandle(localHandle);
                    }

                    if (!matches)
                    {
                        continue;
                    }

                    // The target and output pointers are intentionally null. Windows permits this
                    // exact form when DUPLICATE_CLOSE_SOURCE is supplied.
                    if (DuplicateHandleClose(sourceProcess, handle.HandleValue, IntPtr.Zero, IntPtr.Zero, 0, false, DuplicateCloseSource))
                    {
                        result.ClosedHandleCount++;
                    }
                }
            }
            finally
            {
                foreach (var processHandle in processHandles.Values)
                {
                    if (processHandle != IntPtr.Zero)
                    {
                        CloseHandle(processHandle);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(lockFile) && File.Exists(lockFile))
            {
                try
                {
                    File.Delete(lockFile);
                    result.RemovedLockFile = true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    result.LockFileStillPresent = true;
                }
            }

            return result;
        }

        private static bool IsKnownMutex(AppKind kind, string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            if (kind == AppKind.WeCom)
            {
                return objectName.IndexOf("Tencent.WeWork.ExclusiveObject", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return objectName.IndexOf("_WeChat_App_Instance_Identity_Mutex_Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("WeChat_App_Instance_Identity_Mutex_Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("_Weixin_App_Instance_Identity_Mutex_Name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Weixin_App_Instance_Identity_Mutex_Name", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetModernWeChatLockFile()
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(roaming)
                ? null
                : Path.Combine(roaming, "Tencent", "xwechat", "lock", "lock.ini");
        }

        private static void AddRundll32ProcessesInCurrentSession(ISet<int> processIds, ISet<int> lockFileOnlyProcessIds)
        {
            var currentSession = Process.GetCurrentProcess().SessionId;
            foreach (var process in Process.GetProcessesByName("rundll32"))
            {
                try
                {
                    if (process.SessionId == currentSession)
                    {
                        processIds.Add(process.Id);
                        lockFileOnlyProcessIds.Add(process.Id);
                    }
                }
                catch (Exception)
                {
                    // The process may exit while being inspected.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static IReadOnlyList<SystemHandleRecord> QuerySystemHandles()
        {
            var bufferLength = 1024 * 1024;
            IntPtr buffer = IntPtr.Zero;

            try
            {
                while (bufferLength <= 256 * 1024 * 1024)
                {
                    buffer = Marshal.AllocHGlobal(bufferLength);
                    var status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, bufferLength, out var requiredLength);
                    if (status == 0)
                    {
                        var count = Marshal.ReadIntPtr(buffer).ToInt64();
                        var entrySize = Marshal.SizeOf(typeof(SystemHandleEntry));
                        var offset = IntPtr.Size * 2;
                        var records = new List<SystemHandleRecord>((int)Math.Min(count, 1000000));

                        for (long index = 0; index < count; index++)
                        {
                            var entryPointer = IntPtr.Add(buffer, checked(offset + (int)(index * entrySize)));
                            var entry = (SystemHandleEntry)Marshal.PtrToStructure(entryPointer, typeof(SystemHandleEntry));
                            var processId = unchecked((int)entry.UniqueProcessId.ToUInt64());
                            var handleValue = new IntPtr(unchecked((long)entry.HandleValue.ToUInt64()));
                            records.Add(new SystemHandleRecord(processId, handleValue));
                        }

                        return records;
                    }

                    Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;

                    if (status != StatusInfoLengthMismatch)
                    {
                        return Array.Empty<SystemHandleRecord>();
                    }

                    bufferLength = Math.Max(bufferLength * 2, requiredLength + 65536);
                }
            }
            catch (Exception)
            {
                return Array.Empty<SystemHandleRecord>();
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return Array.Empty<SystemHandleRecord>();
        }

        private static string QueryObjectString(IntPtr handle, int informationClass)
        {
            var bufferLength = 1024;
            IntPtr buffer = IntPtr.Zero;

            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    buffer = Marshal.AllocHGlobal(bufferLength);
                    var status = NtQueryObject(handle, informationClass, buffer, bufferLength, out var requiredLength);
                    if (status >= 0)
                    {
                        var text = (UnicodeString)Marshal.PtrToStructure(buffer, typeof(UnicodeString));
                        return text.Buffer == IntPtr.Zero || text.Length == 0
                            ? string.Empty
                            : Marshal.PtrToStringUni(text.Buffer, text.Length / 2);
                    }

                    Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                    if (requiredLength <= bufferLength || requiredLength > 1024 * 1024)
                    {
                        break;
                    }

                    bufferLength = requiredLength + 256;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return string.Empty;
        }

        private static string GetPathFromHandle(IntPtr handle)
        {
            var buffer = new StringBuilder(1024);
            var length = GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
            if (length == 0)
            {
                return null;
            }

            if (length >= buffer.Capacity)
            {
                buffer = new StringBuilder((int)length + 1);
                length = GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
            }

            if (length == 0)
            {
                return null;
            }

            var path = buffer.ToString();
            return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path.Substring(4) : path;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemHandleEntry
        {
            public IntPtr Object;
            public UIntPtr UniqueProcessId;
            public UIntPtr HandleValue;
            public uint GrantedAccess;
            public ushort CreatorBackTraceIndex;
            public ushort ObjectTypeIndex;
            public uint HandleAttributes;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        private readonly struct SystemHandleRecord
        {
            public SystemHandleRecord(int processId, IntPtr handleValue)
            {
                ProcessId = processId;
                HandleValue = handleValue;
            }

            public int ProcessId { get; }

            public IntPtr HandleValue { get; }
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int systemInformationLength, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryObject(IntPtr handle, int objectInformationClass, IntPtr objectInformation, int objectInformationLength, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DuplicateHandle")]
        private static extern bool DuplicateHandleCopy(IntPtr sourceProcessHandle, IntPtr sourceHandle, IntPtr targetProcessHandle, out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DuplicateHandle")]
        private static extern bool DuplicateHandleClose(IntPtr sourceProcessHandle, IntPtr sourceHandle, IntPtr targetProcessHandle, IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileType(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(IntPtr fileHandle, StringBuilder filePath, int filePathLength, uint flags);
    }

    internal sealed class UnlockAttempt
    {
        public int ClosedHandleCount { get; set; }

        public int InaccessibleProcessCount { get; set; }

        public bool LockFileStillPresent { get; set; }

        public bool RemovedLockFile { get; set; }
    }
}
