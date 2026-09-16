using System;
using System.IO;
using System.Linq;
using System.Text;

namespace WechatDuokai.Core
{
    /// <summary>
    /// Owns every persistent runtime file created by the application. V1.0.6 keeps
    /// configuration, diagnostics and recovery evidence below the executable folder
    /// so an installation remains self-contained and can be removed as one directory.
    /// </summary>
    public static class ApplicationStorage
    {
        public const string DataFolderName = "data";
        internal const string DataMarkerName = ".wechat-duokai-user-data";
        internal const string DataMarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";

        private static readonly object SyncRoot = new object();
        private static bool _ready;

        public static string ApplicationDirectory => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);

        public static string DataDirectory => Path.Combine(ApplicationDirectory, DataFolderName);

        internal static string LegacyDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatDuokai");

        public static void EnsureReady()
        {
            lock (SyncRoot)
            {
                if (_ready)
                {
                    return;
                }

                EnsureLocalDirectory(DataDirectory);
                File.WriteAllText(Path.Combine(DataDirectory, DataMarkerName), DataMarkerValue, Encoding.UTF8);
                MigrateLegacyFiles(LegacyDataDirectory, DataDirectory);
                _ready = true;
            }
        }

        internal static string EnsureSubdirectory(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains(Path.DirectorySeparatorChar.ToString()) ||
                name.Contains(Path.AltDirectorySeparatorChar.ToString()))
            {
                throw new ArgumentException("Invalid application data subdirectory.", nameof(name));
            }

            EnsureReady();
            var directory = Path.Combine(DataDirectory, name);
            EnsureLocalDirectory(directory);
            return directory;
        }

        internal static bool HasValidMarker(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return false;
                }

                var info = new DirectoryInfo(Path.GetFullPath(directory));
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                var marker = Path.Combine(info.FullName, DataMarkerName);
                return File.Exists(marker) &&
                       string.Equals(File.ReadAllText(marker, Encoding.UTF8).Trim(), DataMarkerValue,
                           StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsureLocalDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
            var info = new DirectoryInfo(Path.GetFullPath(directory));
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("程序数据目录不能是符号链接或目录联接：" + info.FullName);
            }

            var applicationRoot = ApplicationDirectory.TrimEnd(Path.DirectorySeparatorChar,
                                      Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = info.FullName.TrimEnd(Path.DirectorySeparatorChar,
                               Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(applicationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("程序数据目录必须位于程序安装目录内。");
            }
        }

        internal static void MigrateLegacyFiles(string legacy, string destination)
        {
            if (PathsEqual(legacy, destination) || !HasValidMarker(legacy))
            {
                return;
            }

            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, DataMarkerName), DataMarkerValue, Encoding.UTF8);

            foreach (var fileName in new[] { "settings.ini", "theme.ini" })
            {
                var source = Path.Combine(legacy, fileName);
                var destinationFile = Path.Combine(destination, fileName);
                try
                {
                    if (File.Exists(source) && !File.Exists(destinationFile))
                    {
                        File.Copy(source, destinationFile, false);
                    }

                    if (File.Exists(source))
                    {
                        File.Delete(source);
                    }

                    var oldTemporary = source + ".new";
                    if (File.Exists(oldTemporary))
                    {
                        File.Delete(oldTemporary);
                    }
                }
                catch (Exception)
                {
                    // A locked legacy file remains in place. The application never falls
                    // back to writing there, so a later install/cleanup can remove it.
                }
            }

            try
            {
                var remaining = Directory.EnumerateFileSystemEntries(legacy)
                    .Where(path => !string.Equals(Path.GetFileName(path), DataMarkerName,
                        StringComparison.OrdinalIgnoreCase))
                    .Any();
                if (!remaining)
                {
                    var marker = Path.Combine(legacy, DataMarkerName);
                    if (File.Exists(marker))
                    {
                        File.Delete(marker);
                    }
                    Directory.Delete(legacy, false);
                }
            }
            catch (Exception)
            {
                // Migration is best effort; no new file is ever created in the legacy path.
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
