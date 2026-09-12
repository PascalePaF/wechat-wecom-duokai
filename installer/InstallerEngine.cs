using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WechatDuokai.Installer
{
    internal static class InstallerEngine
    {
        internal const string ProductName = "微信 · 企业微信多开助手";
        internal const string Version = "1.0.0";
        internal const string SourceMarkerName = ".wechat-duokai-source-root";
        internal const string SourceMarkerValue = "wechat-duokai-source-root:8f8b922d-244d-45c6-b7a8-a47ab3073f7d";
        internal const string ArtifactMarkerName = ".wechat-duokai-artifacts";
        internal const string ArtifactMarkerValue = "wechat-duokai-artifacts:6f5919ee-24d0-43df-8de5-6b0558d79a98";
        internal const string PortableMarkerName = ".wechat-duokai-portable";
        internal const string PortableMarkerValue = "wechat-duokai-portable:c4ad4e76-7449-4f7b-9ab7-5b9379dd3631";
        internal const string InstallMarkerName = ".wechat-duokai-install";
        internal const string InstallMarkerValue = "wechat-duokai-install:e14d0ef8-9e2f-4020-899c-68aa4d04fa2c";
        internal const string UserDataMarkerName = ".wechat-duokai-user-data";
        internal const string UserDataMarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";

        private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WechatDuokai";

        internal static string InstallerExecutablePath => typeof(InstallerEngine).Assembly.Location;

        internal static string InstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WechatDuokai");

        internal static string InstalledExecutable => Path.Combine(InstallDirectory, "wechat_duokai.exe");

        internal static string InstalledUninstaller => Path.Combine(InstallDirectory, "uninstall.exe");

        internal static string UserDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatDuokai");

        internal static string StartMenuDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "微信企业微信多开助手");

        internal static string DesktopShortcut => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "微信企业微信多开助手.lnk");

        internal static InstallResult Install(bool createDesktopShortcut)
        {
            Directory.CreateDirectory(InstallDirectory);

            ExtractResource("Payload.wechat_duokai.exe", InstalledExecutable);
            ExtractResource("Payload.wechat_duokai.exe.config", InstalledExecutable + ".config");
            ExtractResource("Payload.LICENSE.txt", Path.Combine(InstallDirectory, "LICENSE.txt"));
            File.WriteAllText(Path.Combine(InstallDirectory, InstallMarkerName), InstallMarkerValue, Encoding.UTF8);

            File.Copy(InstallerExecutablePath, InstalledUninstaller, true);

            Directory.CreateDirectory(StartMenuDirectory);
            Shortcut.Create(Path.Combine(StartMenuDirectory, "微信企业微信多开助手.lnk"), InstalledExecutable, string.Empty,
                InstallDirectory, InstalledExecutable, "启动微信 · 企业微信多开助手");
            Shortcut.Create(Path.Combine(StartMenuDirectory, "完全卸载.lnk"), InstalledUninstaller, "/uninstall",
                InstallDirectory, InstalledUninstaller, "卸载并清理微信 · 企业微信多开助手");

            if (createDesktopShortcut)
            {
                Shortcut.Create(DesktopShortcut, InstalledExecutable, string.Empty,
                    InstallDirectory, InstalledExecutable, "微信 · 企业微信多开助手");
            }
            else if (File.Exists(DesktopShortcut))
            {
                File.Delete(DesktopShortcut);
            }

            var sourceRoot = FindMarkedParent(InstallerExecutablePath, SourceMarkerName, SourceMarkerValue);
            var artifactRoot = FindMarkedParent(InstallerExecutablePath, ArtifactMarkerName, ArtifactMarkerValue);
            var packageRoot = FindMarkedParent(InstallerExecutablePath, PortableMarkerName, PortableMarkerValue);

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
            {
                key?.SetValue("DisplayName", ProductName);
                key?.SetValue("DisplayVersion", Version);
                key?.SetValue("Publisher", "wechat_duokai contributors");
                key?.SetValue("InstallLocation", InstallDirectory);
                key?.SetValue("DisplayIcon", InstalledExecutable);
                key?.SetValue("UninstallString", Quote(InstalledUninstaller) + " /uninstall");
                key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key?.SetValue("EstimatedSize", 2048, RegistryValueKind.DWord);
                key?.SetValue("SourceRoot", sourceRoot ?? string.Empty);
                key?.SetValue("ArtifactRoot", artifactRoot ?? string.Empty);
                key?.SetValue("PackageRoot", packageRoot ?? string.Empty);
            }

            return new InstallResult
            {
                InstallDirectory = InstallDirectory,
                SourceRoot = sourceRoot,
                ArtifactRoot = artifactRoot
            };
        }

        internal static CleanupLocations GetCleanupLocations()
        {
            string sourceRoot = null;
            string artifactRoot = null;
            string packageRoot = null;
            string installDirectory = InstallDirectory;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, false))
                {
                    installDirectory = key?.GetValue("InstallLocation") as string ?? InstallDirectory;
                    sourceRoot = key?.GetValue("SourceRoot") as string;
                    artifactRoot = key?.GetValue("ArtifactRoot") as string;
                    packageRoot = key?.GetValue("PackageRoot") as string;
                }
            }
            catch (Exception)
            {
                // Fall back to safely discoverable local markers.
            }

            sourceRoot = FirstValid(
                sourceRoot,
                FindMarkedParent(InstallerExecutablePath, SourceMarkerName, SourceMarkerValue),
                ValidateSourceRoot);
            artifactRoot = FirstValid(
                artifactRoot,
                FindMarkedParent(InstallerExecutablePath, ArtifactMarkerName, ArtifactMarkerValue),
                ValidateArtifactRoot);
            packageRoot = FirstValid(
                packageRoot,
                FindMarkedParent(InstallerExecutablePath, PortableMarkerName, PortableMarkerValue),
                ValidatePortableRoot);

            return new CleanupLocations
            {
                InstallDirectory = ValidateInstallDirectory(installDirectory) ? Path.GetFullPath(installDirectory) : InstallDirectory,
                SourceRoot = sourceRoot,
                ArtifactRoot = artifactRoot,
                PackageRoot = packageRoot
            };
        }

        internal static void StartCleanup(CleanupLocations locations, bool deleteSource)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "wechat-duokai-cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var workerPath = Path.Combine(tempDirectory, "cleanup-worker.exe");
            var planPath = Path.Combine(tempDirectory, "cleanup.plan");
            File.Copy(InstallerExecutablePath, workerPath, true);

            var plan = new CleanupPlan
            {
                ParentProcessId = Process.GetCurrentProcess().Id,
                DeleteSource = deleteSource,
                InstallDirectory = locations.InstallDirectory,
                SourceRoot = locations.SourceRoot,
                ArtifactRoot = locations.ArtifactRoot,
                PackageRoot = locations.PackageRoot,
                DesktopShortcut = DesktopShortcut,
                StartMenuDirectory = StartMenuDirectory,
                UserDataDirectory = UserDataDirectory,
                WorkerPath = workerPath,
                PlanPath = planPath
            };
            plan.Write(planPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = workerPath,
                Arguments = "/cleanup-worker " + Quote(planPath),
                WorkingDirectory = tempDirectory,
                UseShellExecute = true
            });
        }

        internal static bool ValidateSourceRoot(string path)
        {
            return ValidateMarkedDirectory(path, SourceMarkerName, SourceMarkerValue) &&
                   File.Exists(Path.Combine(path, "duokai.sln")) &&
                   Directory.Exists(Path.Combine(path, "duokai"));
        }

        internal static bool ValidateInstallDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var expected = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
                var actual = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ValidateUserDataDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var expected = Path.GetFullPath(UserDataDirectory).TrimEnd(Path.DirectorySeparatorChar);
                var actual = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) &&
                       ValidateMarkedDirectory(actual, UserDataMarkerName, UserDataMarkerValue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ValidateArtifactRoot(string path)
        {
            if (!ValidateMarkedDirectory(path, ArtifactMarkerName, ArtifactMarkerValue))
            {
                return false;
            }

            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(path));
                return directory.Name.StartsWith("V", StringComparison.OrdinalIgnoreCase) &&
                       File.Exists(Path.Combine(directory.FullName, "SHA256SUMS.txt")) &&
                       Directory.Exists(Path.Combine(directory.FullName, "installer")) &&
                       Directory.Exists(Path.Combine(directory.FullName, "portable"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ValidatePortableRoot(string path)
        {
            if (!ValidateMarkedDirectory(path, PortableMarkerName, PortableMarkerValue))
            {
                return false;
            }

            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(path));
                return directory.Name.StartsWith("wechat_duokai-portable-", StringComparison.OrdinalIgnoreCase) &&
                       File.Exists(Path.Combine(directory.FullName, "wechat_duokai.exe")) &&
                       File.Exists(Path.Combine(directory.FullName, "wechat_duokai-cleanup-v1.0.0.exe"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ValidateMarkedDirectory(string path, string markerName, string markerValue)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var markerPath = Path.Combine(fullPath, markerName);
                return Directory.Exists(fullPath) &&
                       File.Exists(markerPath) &&
                       string.Equals(File.ReadAllText(markerPath, Encoding.UTF8).Trim(), markerValue, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string FindMarkedParent(string startPath, string markerName, string markerValue)
        {
            try
            {
                var directory = File.Exists(startPath)
                    ? new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(startPath)))
                    : new DirectoryInfo(Path.GetFullPath(startPath));

                for (var depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
                {
                    if (ValidateMarkedDirectory(directory.FullName, markerName, markerValue))
                    {
                        return directory.FullName;
                    }
                }
            }
            catch (Exception)
            {
                // Invalid or inaccessible paths are not cleanup targets.
            }

            return null;
        }

        internal static void RemoveUninstallRegistration()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, false);
            }
            catch (Exception)
            {
                // The registration may already have been removed.
            }
        }

        private static string FirstValid(string first, string second, Func<string, bool> validator)
        {
            if (validator(first))
            {
                return Path.GetFullPath(first);
            }

            return validator(second) ? Path.GetFullPath(second) : null;
        }

        private static void ExtractResource(string resourceName, string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("安装包缺少资源：" + resourceName);
                }

                var temporaryPath = targetPath + ".new";
                using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(temporaryPath, targetPath);
            }
        }

        internal static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", string.Empty) + "\"";
        }
    }

    internal static class CleanupWorker
    {
        internal static void Execute(string planPath)
        {
            CleanupPlan plan = null;
            try
            {
                plan = CleanupPlan.Read(planPath);
                WaitForParent(plan.ParentProcessId);

                StopOwnedProcesses(plan);
                InstallerEngine.RemoveUninstallRegistration();
                DeleteFileIfExact(plan.DesktopShortcut, InstallerEngine.DesktopShortcut);
                DeleteDirectoryIfExact(plan.StartMenuDirectory, InstallerEngine.StartMenuDirectory);

                if (InstallerEngine.ValidateInstallDirectory(plan.InstallDirectory))
                {
                    DeleteDirectoryWithRetries(plan.InstallDirectory);
                }

                if (InstallerEngine.ValidateUserDataDirectory(plan.UserDataDirectory))
                {
                    DeleteDirectoryWithRetries(plan.UserDataDirectory);
                }

                if (!plan.DeleteSource)
                {
                    if (InstallerEngine.ValidatePortableRoot(plan.PackageRoot))
                    {
                        DeleteDirectoryWithRetries(plan.PackageRoot);
                    }

                    if (InstallerEngine.ValidateArtifactRoot(plan.ArtifactRoot))
                    {
                        DeleteDirectoryWithRetries(plan.ArtifactRoot);
                    }
                }
                else if (InstallerEngine.ValidateSourceRoot(plan.SourceRoot))
                {
                    DeleteDirectoryWithRetries(plan.SourceRoot);
                }

                MessageBox.Show(plan.DeleteSource
                        ? "已删除安装程序、发布包和已确认的源码目录。"
                        : "已删除安装程序和发布包，源码已保留。",
                    "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("清理未能全部完成：\r\n" + ex.Message,
                    "清理提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                try
                {
                    if (plan != null && !string.IsNullOrWhiteSpace(plan.PlanPath) && File.Exists(plan.PlanPath))
                    {
                        File.Delete(plan.PlanPath);
                    }
                }
                catch (Exception)
                {
                    // Best effort only.
                }

                MoveFileEx(Application.ExecutablePath, null, 0x4);
                var workerDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(workerDirectory))
                {
                    MoveFileEx(workerDirectory, null, 0x4);
                }
            }
        }

        private static void WaitForParent(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.WaitForExit(30000);
                }
            }
            catch (Exception)
            {
                // The parent is already gone.
            }
        }

        private static void StopOwnedProcesses(CleanupPlan plan)
        {
            var validatedRoots = new List<string>();
            if (InstallerEngine.ValidateInstallDirectory(plan.InstallDirectory))
            {
                validatedRoots.Add(plan.InstallDirectory);
            }
            if (InstallerEngine.ValidateSourceRoot(plan.SourceRoot))
            {
                validatedRoots.Add(plan.SourceRoot);
            }
            if (InstallerEngine.ValidatePortableRoot(plan.PackageRoot))
            {
                validatedRoots.Add(plan.PackageRoot);
            }
            var permittedRoots = validatedRoots
                .Select(path => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
                .ToArray();

            foreach (var processName in new[] { "wechat_duokai", "duokai" })
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (process.Id == Process.GetCurrentProcess().Id)
                        {
                            continue;
                        }

                        var executablePath = process.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(executablePath) ||
                            !permittedRoots.Any(root => Path.GetFullPath(executablePath).StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        process.CloseMainWindow();
                        if (!process.WaitForExit(1500))
                        {
                            process.Kill();
                            process.WaitForExit(1500);
                        }
                    }
                    catch (Exception)
                    {
                        // A process can close on its own during cleanup.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private static void DeleteFileIfExact(string path, string expectedPath)
        {
            if (!PathsEqual(path, expectedPath))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Non-critical shortcut cleanup.
            }
        }

        private static void DeleteDirectoryIfExact(string path, string expectedPath)
        {
            if (PathsEqual(path, expectedPath))
            {
                DeleteDirectoryWithRetries(path);
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DeleteDirectoryWithRetries(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            Exception lastError = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    ClearReadOnlyAttributes(path);
                    Directory.Delete(path, true);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lastError = ex;
                    Thread.Sleep(500);
                }
            }

            if (lastError != null)
            {
                throw new IOException("无法删除目录：" + path, lastError);
            }
        }

        private static void ClearReadOnlyAttributes(string directory)
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(directory));

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var file in current.EnumerateFiles())
                {
                    try
                    {
                        if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                        {
                            file.Attributes &= ~FileAttributes.ReadOnly;
                        }
                    }
                    catch (Exception)
                    {
                        // Directory.Delete will report a useful error if this matters.
                    }
                }

                foreach (var child in current.EnumerateDirectories())
                {
                    try
                    {
                        // Do not traverse junctions or symbolic links while preparing deletion.
                        if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            pending.Push(child);
                        }
                    }
                    catch (Exception)
                    {
                        // Directory.Delete will report a useful error if this matters.
                    }
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }

    internal sealed class CleanupPlan
    {
        public int ParentProcessId { get; set; }
        public bool DeleteSource { get; set; }
        public string InstallDirectory { get; set; }
        public string SourceRoot { get; set; }
        public string ArtifactRoot { get; set; }
        public string PackageRoot { get; set; }
        public string DesktopShortcut { get; set; }
        public string StartMenuDirectory { get; set; }
        public string UserDataDirectory { get; set; }
        public string WorkerPath { get; set; }
        public string PlanPath { get; set; }

        public void Write(string path)
        {
            var lines = new[]
            {
                "wechat-duokai-cleanup-plan:v1",
                ParentProcessId.ToString(),
                DeleteSource ? "1" : "0",
                Encode(InstallDirectory),
                Encode(SourceRoot),
                Encode(ArtifactRoot),
                Encode(PackageRoot),
                Encode(DesktopShortcut),
                Encode(StartMenuDirectory),
                Encode(UserDataDirectory),
                Encode(WorkerPath),
                Encode(path)
            };
            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        public static CleanupPlan Read(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length != 12 || lines[0] != "wechat-duokai-cleanup-plan:v1")
            {
                throw new InvalidDataException("清理计划无效。未删除任何文件。");
            }

            return new CleanupPlan
            {
                ParentProcessId = int.Parse(lines[1]),
                DeleteSource = lines[2] == "1",
                InstallDirectory = Decode(lines[3]),
                SourceRoot = Decode(lines[4]),
                ArtifactRoot = Decode(lines[5]),
                PackageRoot = Decode(lines[6]),
                DesktopShortcut = Decode(lines[7]),
                StartMenuDirectory = Decode(lines[8]),
                UserDataDirectory = Decode(lines[9]),
                WorkerPath = Decode(lines[10]),
                PlanPath = Decode(lines[11])
            };
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }

    internal sealed class CleanupLocations
    {
        public string InstallDirectory { get; set; }
        public string SourceRoot { get; set; }
        public string ArtifactRoot { get; set; }
        public string PackageRoot { get; set; }
    }

    internal sealed class InstallResult
    {
        public string InstallDirectory { get; set; }
        public string SourceRoot { get; set; }
        public string ArtifactRoot { get; set; }
    }

    internal static class Shortcut
    {
        internal static void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconPath, string description)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(targetPath);
            link.SetArguments(arguments ?? string.Empty);
            link.SetWorkingDirectory(workingDirectory ?? string.Empty);
            link.SetIconLocation(iconPath ?? targetPath, 0);
            link.SetDescription(description ?? string.Empty);
            ((IPersistFile)link).Save(shortcutPath, false);
            Marshal.FinalReleaseComObject(link);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maximumPath, IntPtr findData, uint flags);
            void GetIDList(out IntPtr itemIdList);
            void SetIDList(IntPtr itemIdList);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maximumName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maximumPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maximumPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int showCommand);
            void SetShowCmd(int showCommand);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr windowHandle, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
    }
}
