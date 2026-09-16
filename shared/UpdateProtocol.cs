using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WechatDuokai.Update
{
    internal enum UpdateTargetMode
    {
        Installed,
        Portable
    }

    /// <summary>
    /// Small, dependency-free handoff format shared by the application and the
    /// downloaded setup executable. Paths are encoded so whitespace and non-ASCII
    /// installation folders cannot change the argument boundary.
    /// </summary>
    internal sealed class UpdatePlan
    {
        public const string Header = "wechat-duokai-update-plan:v1";
        private static readonly Regex TransactionPattern = new Regex(
            "^[0-9a-f]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Sha256Pattern = new Regex(
            "^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string TransactionId { get; set; }

        public long CreatedUtcTicks { get; set; }

        public int ParentProcessId { get; set; }

        public long ParentStartTimeUtcTicks { get; set; }

        public UpdateTargetMode Mode { get; set; }

        public string CurrentVersion { get; set; }

        public string TargetVersion { get; set; }

        public string TargetDirectory { get; set; }

        public string PackagePath { get; set; }

        public string PackageSha256 { get; set; }

        public static UpdatePlan Create(int parentProcessId, long parentStartTimeUtcTicks,
            UpdateTargetMode mode, string currentVersion, string targetVersion,
            string targetDirectory, string packagePath, string packageSha256)
        {
            return new UpdatePlan
            {
                TransactionId = Guid.NewGuid().ToString("N"),
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                ParentProcessId = parentProcessId,
                ParentStartTimeUtcTicks = parentStartTimeUtcTicks,
                Mode = mode,
                CurrentVersion = currentVersion,
                TargetVersion = targetVersion,
                TargetDirectory = Path.GetFullPath(targetDirectory),
                PackagePath = Path.GetFullPath(packagePath),
                PackageSha256 = NormalizeSha256(packageSha256)
            };
        }

        public void WriteDurable(string path)
        {
            ValidateFields();
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("更新计划目录不存在。");
            }

            var temporaryPath = fullPath + ".new";
            var lines = new[]
            {
                Header,
                TransactionId,
                CreatedUtcTicks.ToString(CultureInfo.InvariantCulture),
                ParentProcessId.ToString(CultureInfo.InvariantCulture),
                ParentStartTimeUtcTicks.ToString(CultureInfo.InvariantCulture),
                Mode.ToString(),
                CurrentVersion,
                TargetVersion,
                Encode(TargetDirectory),
                Encode(PackagePath),
                PackageSha256
            };

            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }

                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }

        public static UpdatePlan Read(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var planFile = new FileInfo(fullPath);
            if (!planFile.Exists || planFile.Length <= 0 || planFile.Length > 16 * 1024)
            {
                throw new InvalidDataException("更新计划文件大小无效。");
            }
            var lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            if (lines.Length != 11 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
            {
                throw new InvalidDataException("更新计划格式无效。");
            }

            int parentProcessId;
            long createdUtcTicks;
            long parentStartTimeUtcTicks;
            UpdateTargetMode mode;
            if (!long.TryParse(lines[2], NumberStyles.None, CultureInfo.InvariantCulture, out createdUtcTicks) ||
                !int.TryParse(lines[3], NumberStyles.None, CultureInfo.InvariantCulture, out parentProcessId) ||
                !long.TryParse(lines[4], NumberStyles.None, CultureInfo.InvariantCulture,
                    out parentStartTimeUtcTicks) ||
                !Enum.TryParse(lines[5], true, out mode) ||
                !Enum.IsDefined(typeof(UpdateTargetMode), mode))
            {
                throw new InvalidDataException("更新计划字段无效。");
            }

            var plan = new UpdatePlan
            {
                TransactionId = lines[1],
                CreatedUtcTicks = createdUtcTicks,
                ParentProcessId = parentProcessId,
                ParentStartTimeUtcTicks = parentStartTimeUtcTicks,
                Mode = mode,
                CurrentVersion = lines[6],
                TargetVersion = lines[7],
                TargetDirectory = Decode(lines[8]),
                PackagePath = Decode(lines[9]),
                PackageSha256 = NormalizeSha256(lines[10])
            };
            plan.ValidateFields();
            return plan;
        }

        public void ValidateFields()
        {
            Version current;
            Version target;
            if (!TransactionPattern.IsMatch(TransactionId ?? string.Empty) ||
                CreatedUtcTicks <= 0 || ParentProcessId <= 0 || ParentStartTimeUtcTicks <= 0 ||
                !Enum.IsDefined(typeof(UpdateTargetMode), Mode) ||
                !Version.TryParse(CurrentVersion, out current) ||
                !Version.TryParse(TargetVersion, out target) || target <= current ||
                string.IsNullOrWhiteSpace(TargetDirectory) || string.IsNullOrWhiteSpace(PackagePath) ||
                !Sha256Pattern.IsMatch(NormalizeSha256(PackageSha256)))
            {
                throw new InvalidDataException("更新计划内容无效。");
            }

            TargetDirectory = Path.GetFullPath(TargetDirectory);
            PackagePath = Path.GetFullPath(PackagePath);
            PackageSha256 = NormalizeSha256(PackageSha256);
        }

        public static string ComputeSha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.Read, 81920, FileOptions.SequentialScan))
            using (var sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(stream));
            }
        }

        public static string NormalizeSha256(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.StartsWith("sha256:", StringComparison.Ordinal)
                ? normalized.Substring("sha256:".Length)
                : normalized;
        }

        public static bool PathsEqual(string left, string right)
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

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("更新计划路径编码无效。", ex);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
    }
}
