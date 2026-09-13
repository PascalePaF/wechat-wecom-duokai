using System;
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
                var settingsPath = Path.Combine(dataDirectory, "settings.ini");
                if (!HasValidMarker(dataDirectory) || !File.Exists(settingsPath))
                {
                    return 2;
                }

                foreach (var line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    if (!line.StartsWith("TargetInstanceCount=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (int.TryParse(line.Substring("TargetInstanceCount=".Length), out var count))
                    {
                        return Math.Max(1, Math.Min(10, count));
                    }
                }
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
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, DataMarkerName), DataMarkerValue, Encoding.UTF8);

            var settingsPath = Path.Combine(dataDirectory, "settings.ini");
            var temporaryPath = settingsPath + ".new";
            File.WriteAllText(temporaryPath, "TargetInstanceCount=" + count, Encoding.UTF8);
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
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
