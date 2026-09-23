using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WechatDuokai.Core
{
    public static class UserPreferences
    {
        internal const string DataMarkerName = ApplicationStorage.DataMarkerName;
        internal const string DataMarkerValue = ApplicationStorage.DataMarkerValue;

        internal static string DataDirectory
        {
            get
            {
                ApplicationStorage.EnsureReady();
                return ApplicationStorage.DataDirectory;
            }
        }

        public static int LoadTargetCount()
        {
            return LoadTargetCount(DataDirectory);
        }

        internal static int LoadTargetCount(string dataDirectory)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                int count;
                string raw;
                if (values.TryGetValue("TargetInstanceCount", out raw) && int.TryParse(raw, out count))
                    return Math.Max(1, Math.Min(10, count));

                string weChat;
                string weCom;
                var weChatCount = 2;
                var weComCount = 2;
                var hasWeChat = values.TryGetValue("WeChatTargetInstanceCount", out weChat) &&
                                int.TryParse(weChat, out weChatCount);
                var hasWeCom = values.TryGetValue("WeComTargetInstanceCount", out weCom) &&
                              int.TryParse(weCom, out weComCount);
                if (hasWeChat)
                    return Math.Max(1, Math.Min(10, weChatCount));
                if (hasWeCom)
                    return Math.Max(1, Math.Min(10, weComCount));
            }
            catch (Exception)
            {
                // A malformed or inaccessible cache falls back to the safe default.
            }

            return 2;
        }

        public static int LoadTargetCount(AppKind kind)
        {
            return LoadTargetCount();
        }

        internal static int LoadTargetCount(string dataDirectory, AppKind kind)
        {
            return LoadTargetCount(dataDirectory);
        }

        public static void SaveTargetCount(int count)
        {
            SaveTargetCount(DataDirectory, count);
        }

        internal static void SaveTargetCount(string dataDirectory, int count)
        {
            count = Math.Max(1, Math.Min(10, count));
            var values = LoadValues(dataDirectory);
            values["TargetInstanceCount"] = count.ToString();
            values.Remove("WeChatTargetInstanceCount");
            values.Remove("WeComTargetInstanceCount");
            SaveValues(dataDirectory, values);
        }

        public static void SaveTargetCount(AppKind kind, int count)
        {
            SaveTargetCount(count);
        }

        internal static void SaveTargetCount(string dataDirectory, AppKind kind, int count)
        {
            SaveTargetCount(dataDirectory, count);
        }

        public static bool LoadAutoCheckForUpdates()
        {
            return LoadAutoCheckForUpdates(DataDirectory);
        }

        internal static bool LoadAutoCheckForUpdates(string dataDirectory)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                string raw;
                bool enabled;
                if (values.TryGetValue("AutoCheckForUpdates", out raw) && bool.TryParse(raw, out enabled))
                {
                    return enabled;
                }
            }
            catch (Exception)
            {
                // Keep the historical update-check behaviour if preferences are unavailable.
            }

            return true;
        }

        public static void SaveAutoCheckForUpdates(bool enabled)
        {
            SaveAutoCheckForUpdates(DataDirectory, enabled);
        }

        internal static void SaveAutoCheckForUpdates(string dataDirectory, bool enabled)
        {
            var values = LoadValues(dataDirectory);
            values["AutoCheckForUpdates"] = enabled.ToString();
            SaveValues(dataDirectory, values);
        }

        public static bool LoadAutoDownloadUpdates()
        {
            return LoadAutoDownloadUpdates(DataDirectory);
        }

        internal static bool LoadAutoDownloadUpdates(string dataDirectory)
        {
            return LoadBooleanPreference(dataDirectory, "AutoDownloadUpdates", false);
        }

        public static void SaveAutoDownloadUpdates(bool enabled)
        {
            SaveAutoDownloadUpdates(DataDirectory, enabled);
        }

        internal static void SaveAutoDownloadUpdates(string dataDirectory, bool enabled)
        {
            SaveBooleanPreference(dataDirectory, "AutoDownloadUpdates", enabled);
        }

        public static bool LoadRunAtWindowsStartup()
        {
            return LoadRunAtWindowsStartup(DataDirectory);
        }

        internal static bool LoadRunAtWindowsStartup(string dataDirectory)
        {
            return LoadBooleanPreference(dataDirectory, "RunAtWindowsStartup", false);
        }

        public static void SaveRunAtWindowsStartup(bool enabled)
        {
            SaveRunAtWindowsStartup(DataDirectory, enabled);
        }

        internal static void SaveRunAtWindowsStartup(string dataDirectory, bool enabled)
        {
            SaveBooleanPreference(dataDirectory, "RunAtWindowsStartup", enabled);
        }

        public static string LoadStartupRegisteredPath()
        {
            return LoadStartupRegisteredPath(DataDirectory);
        }

        internal static string LoadStartupRegisteredPath(string dataDirectory)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                string encoded;
                return values.TryGetValue("StartupExecutableBase64", out encoded) &&
                       !string.IsNullOrWhiteSpace(encoded)
                    ? Path.GetFullPath(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)))
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveStartupRegistration(bool enabled, string executablePath)
        {
            SaveStartupRegistration(DataDirectory, enabled, executablePath);
        }

        internal static void SaveStartupRegistration(string dataDirectory, bool enabled,
            string executablePath)
        {
            var values = LoadValues(dataDirectory);
            values["RunAtWindowsStartup"] = enabled.ToString();
            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(executablePath))
                    throw new ArgumentException("开机启动程序路径不能为空。", nameof(executablePath));
                values["StartupExecutableBase64"] = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(Path.GetFullPath(executablePath)));
            }
            else
            {
                values.Remove("StartupExecutableBase64");
            }
            SaveValues(dataDirectory, values);
        }

        public static bool LoadStartMinimizedOnAutoStart()
        {
            return LoadStartMinimizedOnAutoStart(DataDirectory);
        }

        internal static bool LoadStartMinimizedOnAutoStart(string dataDirectory)
        {
            return LoadBooleanPreference(dataDirectory, "StartMinimizedOnAutoStart", false);
        }

        public static void SaveStartMinimizedOnAutoStart(bool enabled)
        {
            SaveStartMinimizedOnAutoStart(DataDirectory, enabled);
        }

        internal static void SaveStartMinimizedOnAutoStart(string dataDirectory, bool enabled)
        {
            SaveBooleanPreference(dataDirectory, "StartMinimizedOnAutoStart", enabled);
        }

        public static string LoadCustomClientPath(AppKind kind)
        {
            return LoadCustomClientPath(DataDirectory, kind);
        }

        internal static string LoadCustomClientPath(string dataDirectory, AppKind kind)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                var key = kind == AppKind.WeChat ? "WeChatExecutableBase64" : "WeComExecutableBase64";
                string encoded;
                if (!values.TryGetValue(key, out encoded) || string.IsNullOrWhiteSpace(encoded)) return null;
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveCustomClientPath(AppKind kind, string path)
        {
            SaveCustomClientPath(DataDirectory, kind, path);
        }

        internal static void SaveCustomClientPath(string dataDirectory, AppKind kind, string path)
        {
            var values = LoadValues(dataDirectory);
            var key = kind == AppKind.WeChat ? "WeChatExecutableBase64" : "WeComExecutableBase64";
            if (string.IsNullOrWhiteSpace(path)) values.Remove(key);
            else values[key] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(path)));
            SaveValues(dataDirectory, values);
        }

        private static Dictionary<string, string> LoadValues(string dataDirectory)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var settingsPath = Path.Combine(dataDirectory, "settings.ini");
            if (!HasValidMarker(dataDirectory)) return result;
            var backupPath = settingsPath + ".bak";
            if (!File.Exists(settingsPath) && !File.Exists(backupPath)) return result;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(File.Exists(settingsPath) ? settingsPath : backupPath,
                    Encoding.UTF8);
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                lines = File.ReadAllLines(backupPath, Encoding.UTF8);
            }
            catch (UnauthorizedAccessException) when (File.Exists(backupPath))
            {
                lines = File.ReadAllLines(backupPath, Encoding.UTF8);
            }
            foreach (var line in lines)
            {
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;
                result[line.Substring(0, separator)] = line.Substring(separator + 1);
            }
            return result;
        }

        private static void SaveValues(string dataDirectory, IDictionary<string, string> values)
        {
            Directory.CreateDirectory(dataDirectory);
            if (!HasValidMarker(dataDirectory))
                File.WriteAllText(Path.Combine(dataDirectory, DataMarkerName), DataMarkerValue, Encoding.UTF8);
            var settingsPath = Path.Combine(dataDirectory, "settings.ini");
            var temporaryPath = settingsPath + "." + Guid.NewGuid().ToString("N") + ".new";
            var backupPath = settingsPath + ".bak";
            var lines = new List<string>();
            string target;
            if (values.TryGetValue("TargetInstanceCount", out target)) lines.Add("TargetInstanceCount=" + target);
            string weChatTarget;
            if (values.TryGetValue("WeChatTargetInstanceCount", out weChatTarget))
                lines.Add("WeChatTargetInstanceCount=" + weChatTarget);
            string weComTarget;
            if (values.TryGetValue("WeComTargetInstanceCount", out weComTarget))
                lines.Add("WeComTargetInstanceCount=" + weComTarget);
            string autoCheck;
            if (values.TryGetValue("AutoCheckForUpdates", out autoCheck)) lines.Add("AutoCheckForUpdates=" + autoCheck);
            string autoDownload;
            if (values.TryGetValue("AutoDownloadUpdates", out autoDownload))
                lines.Add("AutoDownloadUpdates=" + autoDownload);
            string runAtStartup;
            if (values.TryGetValue("RunAtWindowsStartup", out runAtStartup))
                lines.Add("RunAtWindowsStartup=" + runAtStartup);
            string startupExecutable;
            if (values.TryGetValue("StartupExecutableBase64", out startupExecutable))
                lines.Add("StartupExecutableBase64=" + startupExecutable);
            string startMinimized;
            if (values.TryGetValue("StartMinimizedOnAutoStart", out startMinimized))
                lines.Add("StartMinimizedOnAutoStart=" + startMinimized);
            string weChat;
            if (values.TryGetValue("WeChatExecutableBase64", out weChat)) lines.Add("WeChatExecutableBase64=" + weChat);
            string weCom;
            if (values.TryGetValue("WeComExecutableBase64", out weCom)) lines.Add("WeComExecutableBase64=" + weCom);
            try
            {
                using (var output = new FileStream(temporaryPath, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(output, Encoding.UTF8, 1024, true))
                {
                    foreach (var line in lines) writer.WriteLine(line);
                    writer.Flush();
                    output.Flush(true);
                }

                if (File.Exists(settingsPath))
                {
                    File.Replace(temporaryPath, settingsPath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, settingsPath);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // A failed save must not discard the previous settings file.
                }
            }
        }

        private static bool LoadBooleanPreference(string dataDirectory, string key,
            bool defaultValue)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                string raw;
                bool enabled;
                return values.TryGetValue(key, out raw) && bool.TryParse(raw, out enabled)
                    ? enabled
                    : defaultValue;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        private static void SaveBooleanPreference(string dataDirectory, string key, bool enabled)
        {
            var values = LoadValues(dataDirectory);
            values[key] = enabled.ToString();
            SaveValues(dataDirectory, values);
        }

        internal static bool HasValidMarker()
        {
            return HasValidMarker(DataDirectory);
        }

        internal static bool HasValidMarker(string dataDirectory)
        {
            return ApplicationStorage.HasValidMarker(dataDirectory);
        }

    }
}
