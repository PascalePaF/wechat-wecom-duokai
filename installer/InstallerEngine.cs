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
using WechatDuokai.Update;

namespace WechatDuokai.Installer
{
    internal static class InstallerEngine
    {
        internal const string ProductName = "微信 · 企业微信多开助手";
        internal const string Version = "1.0.7";
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

        internal static string DefaultInstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WechatDuokai");

        internal static string InstallDirectory => DefaultInstallDirectory;

        internal static string InstalledExecutable => GetInstalledExecutable(DefaultInstallDirectory);

        internal static string InstalledUninstaller => GetInstalledUninstaller(DefaultInstallDirectory);

        internal static string UserDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatDuokai");

        internal static string GetInstalledDataDirectory(string installDirectory)
        {
            return Path.Combine(installDirectory, "data");
        }

        internal static string StartMenuDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "微信企业微信多开助手");

        internal static string DesktopShortcut => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "微信企业微信多开助手.lnk");

        internal static string SuggestedInstallDirectory
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, false))
                    {
                        var registered = key?.GetValue("InstallLocation") as string;
                        if (ValidateInstallDirectory(registered))
                        {
                            return Path.GetFullPath(registered);
                        }
                    }
                }
                catch (Exception)
                {
                    // The default remains available if registry data is missing or invalid.
                }

                return DefaultInstallDirectory;
            }
        }

        internal static string GetInstalledExecutable(string installDirectory)
        {
            return Path.Combine(installDirectory, "wechat_duokai.exe");
        }

        internal static string GetInstalledUninstaller(string installDirectory)
        {
            return Path.Combine(installDirectory, "wechat_duokai-uninstall.exe");
        }

        internal static string GetLegacyUninstaller(string installDirectory)
        {
            return Path.Combine(installDirectory, "uninstall.exe");
        }

        internal static InstallResult Install(bool createDesktopShortcut)
        {
            return Install(DefaultInstallDirectory, createDesktopShortcut);
        }

        internal static InstallResult Install(string requestedInstallDirectory, bool createDesktopShortcut)
        {
            string validationError;
            string installDirectory;
            if (!TryNormalizeInstallTarget(requestedInstallDirectory, out installDirectory, out validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(Path.Combine(installDirectory, InstallMarkerName), InstallMarkerValue, Encoding.UTF8);
            PrepareInstalledDataDirectory(installDirectory);

            var installedExecutable = GetInstalledExecutable(installDirectory);
            var installedUninstaller = GetInstalledUninstaller(installDirectory);

            ExtractResource("Payload.wechat_duokai.exe", installedExecutable);
            ExtractResource("Payload.wechat_duokai.exe.config", installedExecutable + ".config");
            ExtractResource("Payload.WechatDuokai.Core.dll", Path.Combine(installDirectory, "WechatDuokai.Core.dll"));
            ExtractResource("Payload.LICENSE.txt", Path.Combine(installDirectory, "LICENSE.txt"));

            ExtractResource("Payload.wechat_duokai-cleanup.exe", installedUninstaller);
            var legacyUninstaller = GetLegacyUninstaller(installDirectory);
            if (File.Exists(legacyUninstaller) && !PathsEqual(legacyUninstaller, InstallerExecutablePath))
            {
                File.Delete(legacyUninstaller);
            }

            Directory.CreateDirectory(StartMenuDirectory);
            Shortcut.Create(Path.Combine(StartMenuDirectory, "微信企业微信多开助手.lnk"), installedExecutable, string.Empty,
                installDirectory, installedExecutable, "启动微信 · 企业微信多开助手");
            Shortcut.Create(Path.Combine(StartMenuDirectory, "完全卸载.lnk"), installedUninstaller, "/uninstall",
                installDirectory, installedUninstaller, "卸载并清理微信 · 企业微信多开助手");

            if (createDesktopShortcut)
            {
                Shortcut.Create(DesktopShortcut, installedExecutable, string.Empty,
                    installDirectory, installedExecutable, "微信 · 企业微信多开助手");
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
                key?.SetValue("InstallLocation", installDirectory);
                key?.SetValue("DisplayIcon", installedExecutable);
                key?.SetValue("UninstallString", Quote(installedUninstaller) + " /uninstall");
                key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key?.SetValue("EstimatedSize", 4096, RegistryValueKind.DWord);
                key?.SetValue("SourceRoot", sourceRoot ?? string.Empty);
                key?.SetValue("ArtifactRoot", artifactRoot ?? string.Empty);
                key?.SetValue("PackageRoot", packageRoot ?? string.Empty);
            }

            return new InstallResult
            {
                InstallDirectory = installDirectory,
                ExecutablePath = installedExecutable,
                UninstallerPath = installedUninstaller,
                SourceRoot = sourceRoot,
                ArtifactRoot = artifactRoot
            };
        }

        internal static InstallResult ApplyVerifiedUpdate(UpdatePlan plan, string planPath)
        {
            ValidateUpdatePlan(plan, planPath, true);
            WaitForUpdateParent(plan);
            WaitForApplicationExit(plan.TargetDirectory, TimeSpan.FromSeconds(30));

            var installed = plan.Mode == UpdateTargetMode.Installed;
            var desktopShortcutExisted = installed && File.Exists(DesktopShortcut);
            try
            {
                ApplyPayloadTransaction(plan.TargetDirectory, plan.Mode, 0);
                var completionReason = "payload-and-integration-complete";
                try
                {
                    if (installed)
                    {
                        RefreshInstalledIntegration(plan.TargetDirectory, desktopShortcutExisted);
                    }
                    else
                    {
                        RemoveObsoletePortableCleanupFiles(plan.TargetDirectory);
                    }
                }
                catch (Exception integrationError)
                {
                    // The executable set is already committed and valid. Shortcut, uninstall
                    // metadata, or obsolete-file cleanup is best effort and must not turn a
                    // successful binary update into a false rollback claim.
                    completionReason = "payload-complete-integration-warning-" +
                                       integrationError.GetType().Name;
                }

                AppendUpdateLog(plan.TargetDirectory, plan, "success", completionReason);
                return new InstallResult
                {
                    InstallDirectory = plan.TargetDirectory,
                    ExecutablePath = GetInstalledExecutable(plan.TargetDirectory),
                    UninstallerPath = installed
                        ? GetInstalledUninstaller(plan.TargetDirectory)
                        : Path.Combine(plan.TargetDirectory,
                            "wechat_duokai-cleanup-v" + Version + ".exe")
                };
            }
            catch (Exception ex)
            {
                AppendUpdateLog(plan.TargetDirectory, plan, "failed", ex.GetType().Name);
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(planPath)) File.Delete(planPath);
                }
                catch (Exception)
                {
                    // The application removes stale plans during a later launch.
                }
            }
        }

        internal static void ValidateUpdatePlan(UpdatePlan plan, string planPath, bool verifyPackageIdentity)
        {
            if (plan == null)
            {
                throw new InvalidDataException("更新计划不存在。");
            }

            plan.ValidateFields();
            System.Version expected;
            System.Version packageVersion;
            if (!System.Version.TryParse(plan.TargetVersion, out expected) ||
                !System.Version.TryParse(Version, out packageVersion) || expected != packageVersion)
            {
                throw new InvalidDataException("更新计划目标版本与安装包版本不一致。");
            }

            var created = new DateTime(plan.CreatedUtcTicks, DateTimeKind.Utc);
            if (created < DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)) ||
                created > DateTime.UtcNow.AddMinutes(5))
            {
                throw new InvalidDataException("更新计划已经过期或时间无效。");
            }

            var dataDirectory = Path.Combine(plan.TargetDirectory, "data");
            var updatesDirectory = Path.Combine(dataDirectory, "updates");
            if (!ValidateUpdateDataDirectory(dataDirectory, updatesDirectory) ||
                !UpdatePlan.PathsEqual(Path.GetDirectoryName(Path.GetFullPath(planPath)), updatesDirectory) ||
                !UpdatePlan.PathsEqual(Path.GetDirectoryName(Path.GetFullPath(plan.PackagePath)), updatesDirectory))
            {
                throw new InvalidDataException("更新计划或安装包不在目标程序自己的 data\\updates 目录中。");
            }

            if (plan.Mode == UpdateTargetMode.Installed)
            {
                if (!ValidateInstallDirectory(plan.TargetDirectory))
                    throw new InvalidDataException("目标目录不是有效的安装版目录。");
            }
            else if (!ValidatePortableUpdateDirectory(plan.TargetDirectory))
            {
                throw new InvalidDataException("目标目录不是有效的绿色版目录。");
            }

            var executable = GetInstalledExecutable(plan.TargetDirectory);
            var currentVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
            if (!VersionsEquivalent(currentVersion, plan.CurrentVersion))
            {
                throw new InvalidDataException("目标程序版本与更新计划不一致。");
            }

            if (verifyPackageIdentity)
            {
                if (!UpdatePlan.PathsEqual(plan.PackagePath, InstallerExecutablePath))
                {
                    throw new InvalidDataException("执行中的安装包与更新计划不一致。");
                }

                var packageHash = UpdatePlan.ComputeSha256(InstallerExecutablePath);
                if (!FixedTimeEquals(packageHash, plan.PackageSha256))
                {
                    throw new InvalidDataException("安装包 SHA-256 与更新计划不一致。");
                }
            }
        }

        internal static bool ValidatePortableUpdateDirectory(string path)
        {
            if (!ValidateMarkedDirectory(path, PortableMarkerName, PortableMarkerValue))
            {
                return false;
            }

            try
            {
                return File.Exists(GetInstalledExecutable(Path.GetFullPath(path))) &&
                       ValidateUserDataBelowTarget(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void ApplyPayloadTransactionForTests(string targetDirectory,
            bool portable, int failAfterReplacement)
        {
            ApplyPayloadTransaction(targetDirectory,
                portable ? UpdateTargetMode.Portable : UpdateTargetMode.Installed,
                failAfterReplacement);
        }

        private static void ApplyPayloadTransaction(string targetDirectory,
            UpdateTargetMode mode, int failAfterReplacement)
        {
            var fullTarget = Path.GetFullPath(targetDirectory);
            if (!ValidateUserDataBelowTarget(fullTarget))
            {
                throw new InvalidDataException("更新目标缺少有效的本地 data 目录。");
            }

            var updatesRoot = Path.Combine(fullTarget, "data", "updates");
            Directory.CreateDirectory(updatesRoot);
            EnsurePlainDirectory(updatesRoot);
            var transactionId = Guid.NewGuid().ToString("N");
            var staging = Path.Combine(updatesRoot, "staging-" + transactionId);
            var backup = Path.Combine(updatesRoot, "backup-" + transactionId);
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(backup);

            var payloads = GetUpdatePayloads(mode).ToArray();
            var replaced = new List<ReplacedPayload>();
            try
            {
                foreach (var payload in payloads)
                {
                    if (!string.Equals(Path.GetFileName(payload.RelativePath), payload.RelativePath,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("更新包包含无效的目标文件名。");
                    }
                    ExtractResourceToNewFile(payload.ResourceName,
                        Path.Combine(staging, payload.RelativePath));
                }

                var stagedApplication = Path.Combine(staging, "wechat_duokai.exe");
                if (!VersionsEquivalent(FileVersionInfo.GetVersionInfo(stagedApplication).FileVersion, Version))
                {
                    throw new InvalidDataException("安装包内的主程序版本不正确。");
                }

                foreach (var payload in payloads)
                {
                    var stagedPath = Path.Combine(staging, payload.RelativePath);
                    var targetPath = Path.Combine(fullTarget, payload.RelativePath);
                    var backupPath = Path.Combine(backup, payload.RelativePath);
                    var existed = File.Exists(targetPath);
                    if (existed)
                    {
                        File.Replace(stagedPath, targetPath, backupPath, true);
                    }
                    else
                    {
                        File.Move(stagedPath, targetPath);
                    }
                    replaced.Add(new ReplacedPayload
                    {
                        TargetPath = targetPath,
                        BackupPath = backupPath,
                        Existed = existed
                    });

                    if (failAfterReplacement > 0 && replaced.Count >= failAfterReplacement)
                    {
                        throw new IOException("Simulated update interruption.");
                    }
                }
            }
            catch (Exception updateError)
            {
                Exception rollbackError = null;
                for (var index = replaced.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        RestorePayload(replaced[index]);
                    }
                    catch (Exception ex)
                    {
                        rollbackError = rollbackError == null
                            ? ex
                            : new AggregateException(rollbackError, ex);
                    }
                }
                if (rollbackError != null)
                {
                    throw new IOException("更新和自动回滚都未能完整完成。请从 GitHub Release 手动覆盖安装。",
                        new AggregateException(updateError, rollbackError));
                }
                throw;
            }
            finally
            {
                DeleteOwnedWorkingDirectory(staging, updatesRoot);
                DeleteOwnedWorkingDirectory(backup, updatesRoot);
            }
        }

        private static IEnumerable<UpdatePayload> GetUpdatePayloads(UpdateTargetMode mode)
        {
            yield return new UpdatePayload("Payload.wechat_duokai.exe", "wechat_duokai.exe");
            yield return new UpdatePayload("Payload.wechat_duokai.exe.config", "wechat_duokai.exe.config");
            yield return new UpdatePayload("Payload.WechatDuokai.Core.dll", "WechatDuokai.Core.dll");
            yield return new UpdatePayload("Payload.LICENSE.txt", "LICENSE.txt");
            yield return new UpdatePayload("Payload.wechat_duokai-cleanup.exe",
                mode == UpdateTargetMode.Installed
                    ? "wechat_duokai-uninstall.exe"
                    : "wechat_duokai-cleanup-v" + Version + ".exe");

            if (mode != UpdateTargetMode.Portable)
            {
                yield break;
            }

            yield return new UpdatePayload("Payload.README.md", "README.md");
            yield return new UpdatePayload("Payload.SECURITY-AUDIT.md", "SECURITY-AUDIT.md");
            yield return new UpdatePayload("Payload.PORTABLE-README.txt", "使用说明.txt");
            yield return new UpdatePayload("Payload.release-notes.md", "版本说明.md");
            yield return new UpdatePayload("Payload.security-report.txt", "完整安全审计与卡巴斯基告警调查报告.txt");
            yield return new UpdatePayload("Payload.update-validation.md", "一键更新安全验证报告.md");
        }

        private static void RestorePayload(ReplacedPayload payload)
        {
            try
            {
                if (payload.Existed && File.Exists(payload.BackupPath))
                {
                    if (File.Exists(payload.TargetPath))
                    {
                        File.Replace(payload.BackupPath, payload.TargetPath, null, true);
                    }
                    else
                    {
                        File.Move(payload.BackupPath, payload.TargetPath);
                    }
                }
                else if (!payload.Existed && File.Exists(payload.TargetPath))
                {
                    File.Delete(payload.TargetPath);
                }
            }
            catch (Exception ex)
            {
                throw new IOException("更新失败，且旧文件自动恢复未完成：" +
                                      Path.GetFileName(payload.TargetPath), ex);
            }
        }

        private static void RefreshInstalledIntegration(string installDirectory, bool createDesktopShortcut)
        {
            var installedExecutable = GetInstalledExecutable(installDirectory);
            var installedUninstaller = GetInstalledUninstaller(installDirectory);
            Directory.CreateDirectory(StartMenuDirectory);
            Shortcut.Create(Path.Combine(StartMenuDirectory, "微信企业微信多开助手.lnk"),
                installedExecutable, string.Empty, installDirectory, installedExecutable,
                "启动微信 · 企业微信多开助手");
            Shortcut.Create(Path.Combine(StartMenuDirectory, "完全卸载.lnk"),
                installedUninstaller, "/uninstall", installDirectory, installedUninstaller,
                "卸载并清理微信 · 企业微信多开助手");
            if (createDesktopShortcut)
            {
                Shortcut.Create(DesktopShortcut, installedExecutable, string.Empty,
                    installDirectory, installedExecutable, "微信 · 企业微信多开助手");
            }

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
            {
                key?.SetValue("DisplayName", ProductName);
                key?.SetValue("DisplayVersion", Version);
                key?.SetValue("Publisher", "wechat_duokai contributors");
                key?.SetValue("InstallLocation", installDirectory);
                key?.SetValue("DisplayIcon", installedExecutable);
                key?.SetValue("UninstallString", Quote(installedUninstaller) + " /uninstall");
                key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key?.SetValue("EstimatedSize", 4096, RegistryValueKind.DWord);
            }
        }

        private static void RemoveObsoletePortableCleanupFiles(string targetDirectory)
        {
            var keep = "wechat_duokai-cleanup-v" + Version + ".exe";
            foreach (var file in new DirectoryInfo(targetDirectory)
                         .EnumerateFiles("wechat_duokai-cleanup-v*.exe", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(file.Name, keep, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    file.Delete();
                }
                catch (Exception)
                {
                    // An obsolete cleanup binary is harmless and can be removed manually.
                }
            }
        }

        private static void WaitForUpdateParent(UpdatePlan plan)
        {
            try
            {
                using (var process = Process.GetProcessById(plan.ParentProcessId))
                {
                    var parentPath = process.MainModule?.FileName;
                    var parentStart = process.StartTime.ToUniversalTime().Ticks;
                    if (!UpdatePlan.PathsEqual(parentPath, GetInstalledExecutable(plan.TargetDirectory)) ||
                        parentStart != plan.ParentStartTimeUtcTicks)
                    {
                        throw new InvalidDataException("更新发起进程与计划不一致。");
                    }
                    process.WaitForExit(30000);
                }
            }
            catch (ArgumentException)
            {
                // The verified parent already exited after launching this setup.
            }
            catch (InvalidOperationException)
            {
                // The verified parent already exited after launching this setup.
            }
        }

        private static void WaitForApplicationExit(string targetDirectory, TimeSpan timeout)
        {
            var expectedPath = GetInstalledExecutable(targetDirectory);
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsExactApplicationRunning(expectedPath)) return;
                Thread.Sleep(100);
            }
            throw new IOException("当前助手仍在运行，未覆盖任何程序文件。请退出后重试更新。");
        }

        private static bool IsExactApplicationRunning(string expectedPath)
        {
            foreach (var process in Process.GetProcessesByName("wechat_duokai"))
            {
                try
                {
                    if (UpdatePlan.PathsEqual(process.MainModule?.FileName, expectedPath)) return true;
                }
                catch (Exception)
                {
                    // A process can exit while it is inspected.
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }

        private static bool ValidateUserDataBelowTarget(string targetDirectory)
        {
            try
            {
                var data = Path.Combine(Path.GetFullPath(targetDirectory), "data");
                return ValidateMarkedDirectory(data, UserDataMarkerName, UserDataMarkerValue) &&
                       (new DirectoryInfo(data).Attributes & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ValidateUpdateDataDirectory(string dataDirectory, string updatesDirectory)
        {
            try
            {
                return ValidateMarkedDirectory(dataDirectory, UserDataMarkerName, UserDataMarkerValue) &&
                       Directory.Exists(updatesDirectory) &&
                       (new DirectoryInfo(updatesDirectory).Attributes & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsurePlainDirectory(string path)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(path));
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("更新工作目录不能是符号链接或目录联接。");
            }
        }

        private static void ExtractResourceToNewFile(string resourceName, string targetPath)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("安装包缺少更新资源：" + resourceName);
                }
                using (var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
            }
        }

        private static void DeleteOwnedWorkingDirectory(string path, string expectedParent)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(path));
                if (!directory.Exists || !UpdatePlan.PathsEqual(directory.Parent?.FullName, expectedParent) ||
                    (!directory.Name.StartsWith("staging-", StringComparison.Ordinal) &&
                     !directory.Name.StartsWith("backup-", StringComparison.Ordinal)) ||
                    (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return;
                }
                foreach (var file in directory.EnumerateFiles()) file.Delete();
                foreach (var child in directory.EnumerateDirectories())
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) == 0 && !child.EnumerateFileSystemInfos().Any())
                        child.Delete(false);
                }
                if (!directory.EnumerateFileSystemInfos().Any()) directory.Delete(false);
            }
            catch (Exception)
            {
                // Staging evidence can remain for diagnostics; application data is untouched.
            }
        }

        private static void AppendUpdateLog(string targetDirectory, UpdatePlan plan,
            string outcome, string reason)
        {
            try
            {
                var logs = Path.Combine(targetDirectory, "data", "logs");
                Directory.CreateDirectory(logs);
                EnsurePlainDirectory(logs);
                var log = Path.Combine(logs, "update.log");
                if (File.Exists(log) && new FileInfo(log).Length > 256 * 1024)
                {
                    var previous = log + ".1";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(log, previous);
                }
                File.AppendAllText(log,
                    DateTime.UtcNow.ToString("o") + " transaction=" + plan.TransactionId +
                    " from=" + plan.CurrentVersion + " to=" + plan.TargetVersion +
                    " mode=" + plan.Mode + " outcome=" + outcome + " reason=" + reason +
                    Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Logging must not turn a successful rollback into another failure.
            }
        }

        private static bool VersionsEquivalent(string left, string right)
        {
            System.Version leftVersion;
            System.Version rightVersion;
            if (!System.Version.TryParse(left, out leftVersion) ||
                !System.Version.TryParse(right, out rightVersion))
                return false;
            return leftVersion.Major == rightVersion.Major &&
                   leftVersion.Minor == rightVersion.Minor &&
                   Math.Max(0, leftVersion.Build) == Math.Max(0, rightVersion.Build);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.ASCII.GetBytes(UpdatePlan.NormalizeSha256(left));
            var rightBytes = Encoding.ASCII.GetBytes(UpdatePlan.NormalizeSha256(right));
            if (leftBytes.Length != rightBytes.Length) return false;
            var difference = 0;
            for (var index = 0; index < leftBytes.Length; index++)
                difference |= leftBytes[index] ^ rightBytes[index];
            return difference == 0;
        }

        private sealed class UpdatePayload
        {
            internal UpdatePayload(string resourceName, string relativePath)
            {
                ResourceName = resourceName;
                RelativePath = relativePath;
            }

            internal string ResourceName { get; }
            internal string RelativePath { get; }
        }

        private sealed class ReplacedPayload
        {
            internal string TargetPath { get; set; }
            internal string BackupPath { get; set; }
            internal bool Existed { get; set; }
        }

        private static void PrepareInstalledDataDirectory(string installDirectory)
        {
            var dataDirectory = GetInstalledDataDirectory(installDirectory);
            Directory.CreateDirectory(dataDirectory);
            var dataInfo = new DirectoryInfo(Path.GetFullPath(dataDirectory));
            if ((dataInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("程序 data 文件夹不能是符号链接或目录联接。");
            }

            File.WriteAllText(Path.Combine(dataDirectory, UserDataMarkerName),
                UserDataMarkerValue, Encoding.UTF8);
            MigrateLegacyUserData(dataDirectory);
        }

        private static void MigrateLegacyUserData(string destinationDirectory)
        {
            if (!ValidateUserDataDirectory(UserDataDirectory))
            {
                return;
            }

            foreach (var fileName in new[] { "settings.ini", "theme.ini" })
            {
                var source = Path.Combine(UserDataDirectory, fileName);
                var destination = Path.Combine(destinationDirectory, fileName);
                try
                {
                    if (File.Exists(source) && !File.Exists(destination))
                    {
                        File.Copy(source, destination, false);
                    }
                    if (File.Exists(source)) File.Delete(source);
                    if (File.Exists(source + ".new")) File.Delete(source + ".new");
                }
                catch (Exception)
                {
                    // The application repeats this safe migration on first launch.
                }
            }

            try
            {
                var remaining = Directory.EnumerateFileSystemEntries(UserDataDirectory)
                    .Where(path => !string.Equals(Path.GetFileName(path), UserDataMarkerName,
                        StringComparison.OrdinalIgnoreCase))
                    .Any();
                if (!remaining)
                {
                    var marker = Path.Combine(UserDataDirectory, UserDataMarkerName);
                    if (File.Exists(marker)) File.Delete(marker);
                    Directory.Delete(UserDataDirectory, false);
                }
            }
            catch (Exception)
            {
                // Cleanup can remove any locked legacy directory later.
            }
        }

        internal static IReadOnlyList<RunningApplicationInfo> FindRunningApplications(string requestedInstallDirectory)
        {
            string normalized;
            string error;
            if (!TryNormalizeInstallTarget(requestedInstallDirectory, out normalized, out error))
            {
                return new RunningApplicationInfo[0];
            }

            var target = GetInstalledExecutable(normalized);
            var result = new List<RunningApplicationInfo>();
            foreach (var process in Process.GetProcessesByName("wechat_duokai"))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (PathsEqual(path, target))
                    {
                        result.Add(new RunningApplicationInfo { ProcessId = process.Id, ExecutablePath = path });
                    }
                }
                catch (Exception)
                {
                    // A process can exit or be inaccessible. Never act without an exact path.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return result;
        }

        internal static IReadOnlyList<RunningApplicationInfo> CloseRunningApplications(
            string requestedInstallDirectory, IEnumerable<RunningApplicationInfo> applications, bool force)
        {
            string normalized;
            string error;
            if (!TryNormalizeInstallTarget(requestedInstallDirectory, out normalized, out error))
            {
                throw new InvalidOperationException(error);
            }

            var target = GetInstalledExecutable(normalized);
            var candidates = (applications ?? Enumerable.Empty<RunningApplicationInfo>()).ToArray();
            foreach (var candidate in candidates)
            {
                try
                {
                    using (var process = Process.GetProcessById(candidate.ProcessId))
                    {
                        var livePath = process.MainModule?.FileName;
                        if (!PathsEqual(livePath, target) || !PathsEqual(candidate.ExecutablePath, target))
                        {
                            continue;
                        }

                        if (force)
                        {
                            process.Kill();
                        }
                        else
                        {
                            process.CloseMainWindow();
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // Already exited.
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                }
            }

            var deadline = DateTime.UtcNow.AddSeconds(force ? 3 : 5);
            IReadOnlyList<RunningApplicationInfo> remaining;
            do
            {
                remaining = FindRunningApplications(normalized);
                if (remaining.Count == 0) return remaining;
                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            return remaining;
        }

        internal static CleanupLocations GetCleanupLocations()
        {
            string sourceRoot = null;
            string artifactRoot = null;
            string packageRoot = null;
            string installDirectory = null;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, false))
                {
                    installDirectory = key?.GetValue("InstallLocation") as string;
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
            installDirectory = FirstValid(
                installDirectory,
                FindMarkedParent(InstallerExecutablePath, InstallMarkerName, InstallMarkerValue),
                ValidateInstallDirectory);

            if (installDirectory == null && ValidateInstallDirectory(DefaultInstallDirectory))
            {
                installDirectory = Path.GetFullPath(DefaultInstallDirectory);
            }

            return new CleanupLocations
            {
                InstallDirectory = installDirectory,
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
            if (!ValidateMarkedDirectory(path, InstallMarkerName, InstallMarkerValue))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var attributes = new DirectoryInfo(fullPath).Attributes;
                return (attributes & FileAttributes.ReparsePoint) == 0 &&
                       (File.Exists(GetInstalledExecutable(fullPath)) ||
                        File.Exists(GetInstalledUninstaller(fullPath)) ||
                        File.Exists(GetLegacyUninstaller(fullPath)));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ValidateInstallTarget(string path, out string error)
        {
            string normalized;
            return TryNormalizeInstallTarget(path, out normalized, out error);
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
                       directory.EnumerateFiles("wechat_duokai-cleanup-v*.exe", SearchOption.TopDirectoryOnly).Any();
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
                if (!Directory.Exists(fullPath) ||
                    (new DirectoryInfo(fullPath).Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                return
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

        private static bool TryNormalizeInstallTarget(string path, out string normalizedPath, out string error)
        {
            normalizedPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "请选择安装文件夹。";
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (normalizedPath.Length > 210)
                {
                    error = "安装路径过长，请选择更短的文件夹。";
                    return false;
                }

                var root = Path.GetPathRoot(normalizedPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase))
                {
                    error = "不能直接安装到磁盘根目录，请选择或新建一个专用文件夹。";
                    return false;
                }

                var protectedLocations = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                };
                var matchesProtectedLocation = false;
                foreach (var protectedLocation in protectedLocations)
                {
                    if (PathsEqual(normalizedPath, protectedLocation))
                    {
                        matchesProtectedLocation = true;
                        break;
                    }
                }
                if (matchesProtectedLocation)
                {
                    error = "该位置是 Windows 或个人资料的关键目录，请选择其中的专用子文件夹。";
                    return false;
                }

                if (Directory.Exists(normalizedPath))
                {
                    var attributes = new DirectoryInfo(normalizedPath).Attributes;
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        error = "为避免卸载时跨目录删除，不能安装到链接或联接目录。";
                        return false;
                    }

                    if (ValidateSourceRoot(normalizedPath) ||
                        ValidateArtifactRoot(normalizedPath) ||
                        ValidatePortableRoot(normalizedPath))
                    {
                        error = "不能把程序安装到源码或发布包目录，请另选专用文件夹。";
                        return false;
                    }

                    var hasEntries = Directory.EnumerateFileSystemEntries(normalizedPath).Any();
                    var isExistingInstallation = ValidateInstallDirectory(normalizedPath);
                    if (hasEntries && !isExistingInstallation)
                    {
                        error = "所选文件夹不是空文件夹。请新建一个专用文件夹，避免覆盖或卸载其他文件。";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                       ex is PathTooLongException || ex is IOException ||
                                       ex is UnauthorizedAccessException)
            {
                error = "安装路径无效或无法访问：" + ex.Message;
                normalizedPath = null;
                return false;
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
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
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
            ExecuteCore(planPath, true, true, true);
        }

        internal static void ExecuteForTests(string planPath)
        {
            ExecuteCore(planPath, false, false, false);
        }

        private static void ExecuteCore(string planPath, bool showMessages, bool scheduleSelfDeletion, bool verifyWorkerIdentity)
        {
            CleanupPlan plan = null;
            try
            {
                plan = CleanupPlan.Read(planPath);
                if (verifyWorkerIdentity &&
                    (!PathsEqual(plan.PlanPath, planPath) ||
                     !PathsEqual(plan.WorkerPath, Application.ExecutablePath)))
                {
                    throw new InvalidDataException("清理计划与工作进程不匹配。未删除任何文件。");
                }

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

                if (showMessages)
                {
                    MessageBox.Show(plan.DeleteSource
                            ? "已删除安装程序、发布包和已确认的源码目录。"
                            : "已删除安装程序和发布包，源码已保留。",
                        "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!showMessages)
                {
                    throw;
                }

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

                if (scheduleSelfDeletion)
                {
                    MoveFileEx(Application.ExecutablePath, null, 0x4);
                    var workerDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                    if (!string.IsNullOrWhiteSpace(workerDirectory))
                    {
                        MoveFileEx(workerDirectory, null, 0x4);
                    }
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
                    DeleteDirectoryWithoutFollowingLinks(path);
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

        private static void DeleteDirectoryWithoutFollowingLinks(string directory)
        {
            var root = new DirectoryInfo(directory);
            if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("拒绝删除目录联接或符号链接：" + directory);
            }

            foreach (var file in root.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
                file.Delete();
            }

            foreach (var child in root.EnumerateDirectories())
            {
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    child.Delete(false);
                    continue;
                }

                DeleteDirectoryWithoutFollowingLinks(child.FullName);
            }

            root.Delete(false);
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
        public string ExecutablePath { get; set; }
        public string UninstallerPath { get; set; }
        public string SourceRoot { get; set; }
        public string ArtifactRoot { get; set; }
    }

    internal sealed class RunningApplicationInfo
    {
        public int ProcessId { get; set; }

        public string ExecutablePath { get; set; }
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
