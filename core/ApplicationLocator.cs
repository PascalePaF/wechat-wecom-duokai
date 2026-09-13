using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WechatDuokai.Core
{
    public static class ApplicationLocator
    {
        private static readonly string[] WeChatExecutableNames = { "Weixin.exe", "WeChat.exe" };
        private static readonly string[] WeComExecutableNames = { "WXWork.exe", "WeCom.exe", "企业微信.exe" };

        public static AppDefinition FindWeChat(string preferredPath = null)
        {
            var candidates = new List<string>();

            AddPreferredCandidate(candidates, preferredPath, AppKind.WeChat);

            AddRegistryDirectory(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\Weixin", "InstallPath", WeChatExecutableNames);
            AddRegistryDirectory(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\WeChat", "InstallPath", WeChatExecutableNames);
            AddRegistryValue(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\Weixin", "Executable");
            AddRegistryValue(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\WeChat", "Executable");
            AddAppPath(candidates, "Weixin.exe");
            AddAppPath(candidates, "WeChat.exe");
            AddRunningProcesses(candidates, new[] { "Weixin", "WeChat" });

            AddProgramFilesCandidates(candidates, @"Tencent\Weixin\Weixin.exe");
            AddProgramFilesCandidates(candidates, @"Tencent\WeChat\WeChat.exe");
            AddProgramFilesCandidates(candidates, @"WeChat\WeChat.exe");

            return new AppDefinition(AppKind.WeChat, "微信", FirstValid(candidates, AppKind.WeChat));
        }

        public static AppDefinition FindWeCom(string preferredPath = null)
        {
            var candidates = new List<string>();

            AddPreferredCandidate(candidates, preferredPath, AppKind.WeCom);

            AddRegistryValue(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\WXWork", "Executable");
            AddRegistryDirectory(candidates, RegistryHive.CurrentUser, @"SOFTWARE\Tencent\WXWork", "InstallPath", WeComExecutableNames);
            AddAppPath(candidates, "WXWork.exe");
            AddAppPath(candidates, "WeCom.exe");
            AddRunningProcesses(candidates, new[] { "WXWork", "WeCom", "企业微信" });

            AddProgramFilesCandidates(candidates, @"WXWork\WXWork.exe");
            AddProgramFilesCandidates(candidates, @"Tencent\WXWork\WXWork.exe");
            AddProgramFilesCandidates(candidates, @"WeCom\WeCom.exe");

            return new AppDefinition(AppKind.WeCom, "企业微信", FirstValid(candidates, AppKind.WeCom));
        }

        private static void AddPreferredCandidate(ICollection<string> candidates, string path, AppKind kind)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var validation = ClientExecutableValidator.Validate(path, kind);
            if (validation.IsValid)
            {
                candidates.Add(validation.NormalizedPath);
            }
        }

        private static void AddRegistryDirectory(List<string> candidates, RegistryHive hive, string keyPath, string valueName, IEnumerable<string> executableNames)
        {
            var value = ReadRegistryString(hive, keyPath, valueName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var executableName in executableNames)
            {
                candidates.Add(Path.Combine(Environment.ExpandEnvironmentVariables(value.Trim(' ', '"')), executableName));
            }
        }

        private static void AddRegistryValue(List<string> candidates, RegistryHive hive, string keyPath, string valueName)
        {
            var value = ReadRegistryString(hive, keyPath, valueName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(Environment.ExpandEnvironmentVariables(value.Trim(' ', '"')));
            }
        }

        private static void AddAppPath(List<string> candidates, string executableName)
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + executableName;
            var currentUser = ReadRegistryString(RegistryHive.CurrentUser, keyPath, null);
            var localMachine = ReadRegistryString(RegistryHive.LocalMachine, keyPath, null);

            if (!string.IsNullOrWhiteSpace(currentUser))
            {
                candidates.Add(currentUser);
            }

            if (!string.IsNullOrWhiteSpace(localMachine))
            {
                candidates.Add(localMachine);
            }
        }

        private static string ReadRegistryString(RegistryHive hive, string keyPath, string valueName)
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (var key = baseKey.OpenSubKey(keyPath, false))
                    {
                        var value = key?.GetValue(valueName) as string;
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return Environment.ExpandEnvironmentVariables(value.Trim(' ', '"'));
                        }
                    }
                }
                catch (Exception)
                {
                    // A missing or inaccessible registry view simply means this candidate is unavailable.
                }
            }

            return null;
        }

        private static void AddRunningProcesses(List<string> candidates, IEnumerable<string> processNames)
        {
            foreach (var processName in processNames)
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        candidates.Add(process.MainModule?.FileName);
                    }
                    catch (Exception)
                    {
                        // Protected/elevated processes may hide their path.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private static void AddProgramFilesCandidates(List<string> candidates, string relativePath)
        {
            foreach (var folder in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            })
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    candidates.Add(Path.Combine(folder, relativePath));
                }
            }
        }

        private static string FirstValid(IEnumerable<string> candidates, AppKind kind)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawCandidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(rawCandidate))
                {
                    continue;
                }

                try
                {
                    var candidate = Path.GetFullPath(rawCandidate.Trim(' ', '"'));
                    if (seen.Add(candidate) && ClientExecutableValidator.Validate(candidate, kind).IsValid)
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    // Ignore malformed registry or process paths.
                }
            }

            return null;
        }
    }
}
