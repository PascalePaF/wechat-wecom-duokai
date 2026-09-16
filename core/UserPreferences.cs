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
            return LoadTargetCount(AppKind.WeChat);
        }

        internal static int LoadTargetCount(string dataDirectory)
        {
            return LoadTargetCount(dataDirectory, AppKind.WeChat);
        }

        public static int LoadTargetCount(AppKind kind)
        {
            return LoadTargetCount(DataDirectory, kind);
        }

        internal static int LoadTargetCount(string dataDirectory, AppKind kind)
        {
            try
            {
                var values = LoadValues(dataDirectory);
                int count;
                string raw;
                if (values.TryGetValue(GetTargetCountKey(kind), out raw) && int.TryParse(raw, out count))
                    return Math.Max(1, Math.Min(10, count));
                if (values.TryGetValue("TargetInstanceCount", out raw) && int.TryParse(raw, out count))
                    return Math.Max(1, Math.Min(10, count));
            }
            catch (Exception)
            {
                // A malformed or inaccessible cache falls back to the safe default.
            }

            return 2;
        }

        public static void SaveTargetCount(int count)
        {
            SaveTargetCount(AppKind.WeChat, count);
        }

        internal static void SaveTargetCount(string dataDirectory, int count)
        {
            SaveTargetCount(dataDirectory, AppKind.WeChat, count);
        }

        public static void SaveTargetCount(AppKind kind, int count)
        {
            SaveTargetCount(DataDirectory, kind, count);
        }

        internal static void SaveTargetCount(string dataDirectory, AppKind kind, int count)
        {
            count = Math.Max(1, Math.Min(10, count));
            var values = LoadValues(dataDirectory);
            int legacyCount;
            string legacyRaw;
            if (values.TryGetValue("TargetInstanceCount", out legacyRaw) &&
                int.TryParse(legacyRaw, out legacyCount))
            {
                legacyCount = Math.Max(1, Math.Min(10, legacyCount));
                if (!values.ContainsKey("WeChatTargetInstanceCount"))
                    values["WeChatTargetInstanceCount"] = legacyCount.ToString();
                if (!values.ContainsKey("WeComTargetInstanceCount"))
                    values["WeComTargetInstanceCount"] = legacyCount.ToString();
            }

            values[GetTargetCountKey(kind)] = count.ToString();
            if (values.ContainsKey("WeChatTargetInstanceCount") &&
                values.ContainsKey("WeComTargetInstanceCount"))
            {
                values.Remove("TargetInstanceCount");
            }
            SaveValues(dataDirectory, values);
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
            if (!HasValidMarker(dataDirectory) || !File.Exists(settingsPath)) return result;
            foreach (var line in File.ReadAllLines(settingsPath, Encoding.UTF8))
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
            File.WriteAllText(Path.Combine(dataDirectory, DataMarkerName), DataMarkerValue, Encoding.UTF8);
            var settingsPath = Path.Combine(dataDirectory, "settings.ini");
            var temporaryPath = settingsPath + ".new";
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
            string weChat;
            if (values.TryGetValue("WeChatExecutableBase64", out weChat)) lines.Add("WeChatExecutableBase64=" + weChat);
            string weCom;
            if (values.TryGetValue("WeComExecutableBase64", out weCom)) lines.Add("WeComExecutableBase64=" + weCom);
            File.WriteAllLines(temporaryPath, lines, Encoding.UTF8);
            if (File.Exists(settingsPath)) File.Delete(settingsPath);
            File.Move(temporaryPath, settingsPath);
        }

        internal static bool HasValidMarker()
        {
            return HasValidMarker(DataDirectory);
        }

        internal static bool HasValidMarker(string dataDirectory)
        {
            return ApplicationStorage.HasValidMarker(dataDirectory);
        }

        private static string GetTargetCountKey(AppKind kind)
        {
            return kind == AppKind.WeChat
                ? "WeChatTargetInstanceCount"
                : "WeComTargetInstanceCount";
        }
    }
}
