using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace WechatDuokai.Presentation
{
    internal sealed class StartupIntegrationResult
    {
        internal bool Succeeded { get; set; }

        internal bool Enabled { get; set; }

        internal bool Changed { get; set; }

        internal bool Conflict { get; set; }

        internal string Message { get; set; }
    }

    internal static class WindowsStartupIntegration
    {
        internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "WechatDuokai";
        internal const string AutoStartArgument = "--autostart";

        internal static StartupIntegrationResult Synchronize(bool enabled, string executablePath)
        {
            return SynchronizeCore(RunKeyPath, ValueName, enabled, executablePath);
        }

        internal static bool RemoveIfOwnedByExactExecutables(IEnumerable<string> executablePaths)
        {
            return RemoveIfOwnedByExactExecutablesCore(RunKeyPath, ValueName, executablePaths);
        }

        internal static bool TryParseOwnedCommand(string command, out string executablePath)
        {
            executablePath = null;
            var value = (command ?? string.Empty).Trim();
            if (value.Length < 3 || value[0] != '"')
            {
                return false;
            }

            var closingQuote = value.IndexOf('"', 1);
            if (closingQuote <= 1 ||
                !string.Equals(value.Substring(closingQuote + 1).Trim(), AutoStartArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var candidate = Path.GetFullPath(value.Substring(1, closingQuote - 1));
                if (!string.Equals(Path.GetFileName(candidate), "wechat_duokai.exe",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                executablePath = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string BuildCommand(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("程序路径不能为空。", nameof(executablePath));
            }

            var fullPath = Path.GetFullPath(executablePath);
            if (!string.Equals(Path.GetFileName(fullPath), "wechat_duokai.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("开机启动项只能指向微窗助手主程序。");
            }

            return "\"" + fullPath.Replace("\"", string.Empty) + "\" " + AutoStartArgument;
        }

        internal static StartupIntegrationResult SynchronizeCore(string registryPath,
            string valueName, bool enabled, string executablePath)
        {
            try
            {
                var expectedCommand = BuildCommand(executablePath);
                if (enabled && !File.Exists(Path.GetFullPath(executablePath)))
                {
                    throw new FileNotFoundException("找不到微窗助手主程序，未创建开机启动项。",
                        executablePath);
                }

                using (var key = Registry.CurrentUser.CreateSubKey(registryPath, true))
                {
                    if (key == null)
                    {
                        throw new InvalidOperationException("Windows 未能打开当前用户的开机启动设置。");
                    }

                    var current = key.GetValue(valueName, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    string ownedExecutable;
                    var hasCurrent = !string.IsNullOrWhiteSpace(current);
                    var isOwned = hasCurrent && TryParseOwnedCommand(current, out ownedExecutable);
                    if (hasCurrent && !isOwned)
                    {
                        return new StartupIntegrationResult
                        {
                            Succeeded = !enabled,
                            Enabled = false,
                            Conflict = true,
                            Message = enabled
                                ? "检测到同名启动项已被其他程序设置；已保留原值，未进行覆盖。"
                                : "同名启动项不属于微窗助手，已保留原值；本程序不会再尝试自动启动。"
                        };
                    }

                    if (!enabled)
                    {
                        if (isOwned)
                        {
                            key.DeleteValue(valueName, false);
                        }
                        return new StartupIntegrationResult
                        {
                            Succeeded = true,
                            Enabled = false,
                            Changed = isOwned,
                            Message = isOwned ? "已关闭开机自动启动" : "开机自动启动已处于关闭状态"
                        };
                    }

                    if (isOwned && string.Equals(current, expectedCommand,
                            StringComparison.Ordinal))
                    {
                        return new StartupIntegrationResult
                        {
                            Succeeded = true,
                            Enabled = true,
                            Message = "开机自动启动已启用"
                        };
                    }

                    key.SetValue(valueName, expectedCommand, RegistryValueKind.String);
                    var written = key.GetValue(valueName, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    if (!string.Equals(written, expectedCommand, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Windows 未能保存开机启动设置。");
                    }

                    return new StartupIntegrationResult
                    {
                        Succeeded = true,
                        Enabled = true,
                        Changed = true,
                        Message = isOwned
                            ? "已修复开机启动路径"
                            : "已开启开机自动启动"
                    };
                }
            }
            catch (Exception ex)
            {
                return new StartupIntegrationResult
                {
                    Succeeded = false,
                    Enabled = false,
                    Message = "无法修改开机启动设置：" + ex.Message
                };
            }
        }

        internal static bool RemoveIfOwnedByExactExecutablesCore(string registryPath,
            string valueName, IEnumerable<string> executablePaths)
        {
            try
            {
                var permitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in executablePaths ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    permitted.Add(Path.GetFullPath(path));
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    if (key == null) return false;
                    var current = key.GetValue(valueName, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    string ownedExecutable;
                    if (!TryParseOwnedCommand(current, out ownedExecutable) ||
                        !permitted.Contains(ownedExecutable))
                    {
                        return false;
                    }

                    key.DeleteValue(valueName, false);
                    return true;
                }
            }
            catch (Exception)
            {
                // Cleanup must preserve anything it cannot prove belongs to an exact target.
                return false;
            }
        }
    }
}
