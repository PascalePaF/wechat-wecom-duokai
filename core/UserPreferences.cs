using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WechatDuokai.Core
{
    public static class UserPreferences
    {
        internal const string DataMarkerName = ".wechat-duokai-user-data";
        internal const string DataMarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";

        internal static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatDuokai");

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
                if (values.TryGetValue("TargetInstanceCount", out var raw) && int.TryParse(raw, out count))
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
            SaveTargetCount(DataDirectory, count);
        }

        internal static void SaveTargetCount(string dataDirectory, int count)
        {
            count = Math.Max(1, Math.Min(10, count));
            var values = LoadValues(dataDirectory);
            values["TargetInstanceCount"] = count.ToString();
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
            try
            {
                var markerPath = Path.Combine(dataDirectory, DataMarkerName);
                return Directory.Exists(dataDirectory) &&
                       File.Exists(markerPath) &&
                       string.Equals(File.ReadAllText(markerPath, Encoding.UTF8).Trim(), DataMarkerValue, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
