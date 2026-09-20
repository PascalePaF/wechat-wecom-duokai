using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WechatDuokai.Update;

namespace WechatDuokai.Core
{
    public enum ApplicationInstallMode
    {
        Unknown,
        Installed,
        Portable
    }

    public sealed class UpdateProgressInfo
    {
        public string Message { get; set; }

        public long BytesReceived { get; set; }

        public long TotalBytes { get; set; }

        public int Percentage => TotalBytes <= 0
            ? 0
            : (int)Math.Max(0, Math.Min(100, BytesReceived * 100L / TotalBytes));
    }

    public sealed class PreparedUpdatePackage
    {
        public string Version { get; set; }

        public string PackagePath { get; set; }

        public string Sha256 { get; set; }

        public long Size { get; set; }

        public ApplicationInstallMode Mode { get; set; }
    }

    public sealed class ApplicationUpdateService
    {
        private static readonly string ClientVersion = GetClientVersion();
        internal const string InstallMarkerName = ".wechat-duokai-install";
        internal const string InstallMarkerValue =
            "wechat-duokai-install:e14d0ef8-9e2f-4020-899c-68aa4d04fa2c";
        internal const string PortableMarkerName = ".wechat-duokai-portable";
        internal const string PortableMarkerValue =
            "wechat-duokai-portable:c4ad4e76-7449-4f7b-9ab7-5b9379dd3631";
        private const string UpdatesFolderName = "updates";
        private static readonly Regex ChecksumLinePattern = new Regex(
            "^([0-9a-fA-F]{64})\\s+\\*?(.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ApplicationInstallMode DetectCurrentMode()
        {
            return DetectMode(ApplicationStorage.ApplicationDirectory);
        }

        public async Task<PreparedUpdatePackage> DownloadAndVerifyAsync(
            ReleaseUpdateResult release, IProgress<UpdateProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            if (release == null || !release.CanInstallUpdate)
            {
                throw new InvalidOperationException(release?.OneClickUpdateError ??
                                                    "当前 Release 不具备一键更新所需附件。");
            }

            var mode = DetectCurrentMode();
            if (mode == ApplicationInstallMode.Unknown)
            {
                throw new InvalidOperationException(
                    "当前运行目录既不是本项目安装版，也不是带标记的绿色版。请从发布页手动更新。");
            }

            var updatesDirectory = ApplicationStorage.EnsureSubdirectory(UpdatesFolderName);
            CleanupStaleDownloads(updatesDirectory, TimeSpan.FromDays(2));
            var checksumBytes = await DownloadSmallAssetAsync(release.ChecksumAsset,
                progress, cancellationToken);
            var checksumHash = ComputeSha256(checksumBytes);
            if (release.HasGitHubAssetDigests &&
                !FixedTimeEquals(checksumHash, release.ChecksumAsset.Sha256))
            {
                throw new InvalidDataException("SHA256SUMS.txt 与 GitHub 服务端摘要不一致，已停止更新。");
            }

            var checksumText = Encoding.UTF8.GetString(checksumBytes);
            var manifestHash = ParseChecksum(checksumText, release.SetupAsset.Name);
            if (release.HasGitHubAssetDigests &&
                !FixedTimeEquals(manifestHash, release.SetupAsset.Sha256))
            {
                throw new InvalidDataException(
                    "安装包的 GitHub 摘要与 SHA256SUMS.txt 不一致，已停止更新。");
            }

            var checksumEvidencePath = Path.Combine(updatesDirectory,
                "SHA256SUMS-v" + release.LatestVersion + ".txt");
            WriteBytesDurable(checksumEvidencePath, checksumBytes);

            var packagePath = Path.Combine(updatesDirectory, release.SetupAsset.Name);
            var temporaryPath = packagePath + ".part";
            try
            {
                await DownloadFileAsync(release.SetupAsset, temporaryPath, progress, cancellationToken);
                var downloadedHash = UpdatePlan.ComputeSha256(temporaryPath);
                if (!FixedTimeEquals(downloadedHash, manifestHash))
                {
                    throw new InvalidDataException("下载后的安装包 SHA-256 校验失败，文件不会被执行。");
                }

                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }
                File.Move(temporaryPath, packagePath);

                progress?.Report(new UpdateProgressInfo
                {
                    Message = release.HasGitHubAssetDigests
                        ? "三方 SHA-256 校验通过"
                        : "Release 清单与安装包 SHA-256 校验通过",
                    BytesReceived = release.SetupAsset.Size,
                    TotalBytes = release.SetupAsset.Size
                });

                return new PreparedUpdatePackage
                {
                    Version = release.LatestVersion,
                    PackagePath = packagePath,
                    Sha256 = downloadedHash,
                    Size = release.SetupAsset.Size,
                    Mode = mode
                };
            }
            catch
            {
                TryDeleteFile(temporaryPath);
                throw;
            }
        }

        public Process LaunchVerifiedInstaller(PreparedUpdatePackage package,
            string currentVersion, string themeName)
        {
            if (package == null || package.Mode == ApplicationInstallMode.Unknown ||
                string.IsNullOrWhiteSpace(package.Version) ||
                string.IsNullOrWhiteSpace(package.PackagePath) ||
                !File.Exists(package.PackagePath))
            {
                throw new InvalidOperationException("经过验证的更新包不存在。");
            }

            var updatesDirectory = ApplicationStorage.EnsureSubdirectory(UpdatesFolderName);
            var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(package.PackagePath));
            if (!UpdatePlan.PathsEqual(packageDirectory, updatesDirectory))
            {
                throw new InvalidOperationException("更新包不在程序自己的 data\\updates 目录中。");
            }

            using (var packageLock = new FileStream(package.PackagePath, FileMode.Open,
                       FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
            using (var current = Process.GetCurrentProcess())
            {
                var actualHash = ComputeSha256(packageLock);
                if (!FixedTimeEquals(actualHash, package.Sha256))
                {
                    throw new InvalidDataException("更新包在启动前发生变化，已拒绝执行。");
                }

                var plan = UpdatePlan.Create(
                    current.Id,
                    current.StartTime.ToUniversalTime().Ticks,
                    package.Mode == ApplicationInstallMode.Installed
                        ? UpdateTargetMode.Installed
                        : UpdateTargetMode.Portable,
                    currentVersion,
                    package.Version,
                    ApplicationStorage.ApplicationDirectory,
                    package.PackagePath,
                    actualHash);
                var planPath = Path.Combine(updatesDirectory,
                    "update-v" + package.Version + "-" + plan.TransactionId + ".plan");
                plan.WriteDurable(planPath);

                var arguments = "/auto-update " + Quote(planPath) +
                                " --theme=" + (string.Equals(themeName, "dark",
                                    StringComparison.OrdinalIgnoreCase) ? "dark" : "light");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = package.PackagePath,
                    Arguments = arguments,
                    WorkingDirectory = updatesDirectory,
                    UseShellExecute = true
                });
                if (process == null)
                {
                    throw new InvalidOperationException("Windows 未能启动经过验证的更新安装程序。");
                }
                try
                {
                    process.WaitForInputIdle(5000);
                }
                catch (Exception)
                {
                    // The process may validate and exit before a window becomes idle.
                    // The executable stayed non-writable from hashing through launch.
                }
                return process;
            }
        }

        public void CleanupStaleDownloads()
        {
            try
            {
                CleanupStaleDownloads(ApplicationStorage.EnsureSubdirectory(UpdatesFolderName),
                    TimeSpan.FromDays(2));
            }
            catch (Exception)
            {
                // A still-running updater can keep its executable locked. A later launch retries.
            }
        }

        internal static ApplicationInstallMode DetectMode(string applicationDirectory)
        {
            try
            {
                var fullPath = Path.GetFullPath(applicationDirectory);
                var directory = new DirectoryInfo(fullPath);
                if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    !File.Exists(Path.Combine(fullPath, "wechat_duokai.exe")))
                {
                    return ApplicationInstallMode.Unknown;
                }

                var installed = HasMarker(fullPath, InstallMarkerName, InstallMarkerValue);
                var portable = HasMarker(fullPath, PortableMarkerName, PortableMarkerValue);
                if (installed == portable)
                {
                    return ApplicationInstallMode.Unknown;
                }

                return installed ? ApplicationInstallMode.Installed : ApplicationInstallMode.Portable;
            }
            catch (Exception)
            {
                return ApplicationInstallMode.Unknown;
            }
        }

        internal static string ParseChecksum(string contents, string expectedFileName)
        {
            var matches = new List<string>();
            foreach (var rawLine in (contents ?? string.Empty).Split(new[] { "\r\n", "\n" },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var match = ChecksumLinePattern.Match(rawLine.Trim());
                if (!match.Success)
                {
                    continue;
                }

                var relative = match.Groups[2].Value.Trim().Replace('\\', '/');
                if (string.Equals(relative, expectedFileName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "installer/" + expectedFileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(match.Groups[1].Value.ToLowerInvariant());
                }
            }

            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    "SHA256SUMS.txt 中必须且只能包含一条正式安装包校验记录。");
            }
            return matches[0];
        }

        private static async Task<byte[]> DownloadSmallAssetAsync(ReleaseAssetInfo asset,
            IProgress<UpdateProgressInfo> progress, CancellationToken cancellationToken)
        {
            using (var client = CreateDownloadClient())
            using (var request = CreateDownloadRequest(asset.DownloadUrl))
            using (var response = await client.SendAsync(request,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                ValidateFinalDownloadUri(response);
                ValidateContentLength(response, asset.Size);
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[16384];
                    long total = 0;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (read == 0) break;
                        total += read;
                        if (total > asset.Size || total > ReleaseUpdateChecker.MaximumChecksumBytes)
                        {
                            throw new InvalidDataException("校验文件大小超过 Release 元数据声明。");
                        }
                        output.Write(buffer, 0, read);
                    }
                    if (total != asset.Size)
                    {
                        throw new InvalidDataException("校验文件大小与 Release 元数据不一致。");
                    }
                    progress?.Report(new UpdateProgressInfo
                    {
                        Message = "已取得 Release 校验文件",
                        BytesReceived = total,
                        TotalBytes = total
                    });
                    return output.ToArray();
                }
            }
        }

        private static async Task DownloadFileAsync(ReleaseAssetInfo asset, string temporaryPath,
            IProgress<UpdateProgressInfo> progress, CancellationToken cancellationToken)
        {
            using (var client = CreateDownloadClient())
            using (var request = CreateDownloadRequest(asset.DownloadUrl))
            using (var response = await client.SendAsync(request,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                ValidateFinalDownloadUri(response);
                ValidateContentLength(response, asset.Size);
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                           FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (read == 0) break;
                        total += read;
                        if (total > asset.Size || total > ReleaseUpdateChecker.MaximumSetupBytes)
                        {
                            throw new InvalidDataException("安装包大小超过 Release 元数据声明。");
                        }
                        await output.WriteAsync(buffer, 0, read, cancellationToken);
                        progress?.Report(new UpdateProgressInfo
                        {
                            Message = "正在下载并校验 " + asset.Name,
                            BytesReceived = total,
                            TotalBytes = asset.Size
                        });
                    }
                    output.Flush(true);
                    if (total != asset.Size)
                    {
                        throw new InvalidDataException("安装包大小与 Release 元数据不一致。");
                    }
                }
            }
        }

        private static HttpClient CreateDownloadClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        private static HttpRequestMessage CreateDownloadRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("wechat-duokai-updater/" + ClientVersion);
            request.Headers.Accept.ParseAdd("application/octet-stream");
            return request;
        }

        private static string GetClientVersion()
        {
            var version = typeof(ApplicationUpdateService).Assembly.GetName().Version;
            return version == null
                ? "0.0.0"
                : version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
        }

        private static void ValidateContentLength(HttpResponseMessage response, long expected)
        {
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value != expected)
            {
                throw new InvalidDataException("下载内容大小与 GitHub Release 元数据不一致。");
            }
        }

        private static void ValidateFinalDownloadUri(HttpResponseMessage response)
        {
            if (!ReleaseUpdateChecker.IsTrustedAssetDeliveryUri(
                    response?.RequestMessage?.RequestUri))
            {
                throw new InvalidDataException(
                    "更新附件被重定向到非 GitHub 交付地址，已停止下载。");
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static string ComputeSha256(Stream stream)
        {
            stream.Position = 0;
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.ASCII.GetBytes(UpdatePlan.NormalizeSha256(left));
            var rightBytes = Encoding.ASCII.GetBytes(UpdatePlan.NormalizeSha256(right));
            if (leftBytes.Length != rightBytes.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < leftBytes.Length; index++)
            {
                difference |= leftBytes[index] ^ rightBytes[index];
            }
            return difference == 0;
        }

        private static bool HasMarker(string directory, string markerName, string markerValue)
        {
            var marker = Path.Combine(directory, markerName);
            return File.Exists(marker) && string.Equals(
                File.ReadAllText(marker, Encoding.UTF8).Trim(), markerValue, StringComparison.Ordinal);
        }

        private static void WriteBytesDurable(string path, byte[] bytes)
        {
            var temporaryPath = path + ".new";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporaryPath, path);
        }

        private static void CleanupStaleDownloads(string directory, TimeSpan minimumAge)
        {
            var cutoff = DateTime.UtcNow.Subtract(minimumAge);
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                var known = file.Name.StartsWith("wechat_duokai-setup-v", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("SHA256SUMS-v", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("update-v", StringComparison.OrdinalIgnoreCase);
                if (!known || file.LastWriteTimeUtc >= cutoff)
                {
                    continue;
                }

                TryDeleteFile(file.FullName);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // A locked file is retried during a later application launch.
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", string.Empty) + "\"";
        }
    }
}
