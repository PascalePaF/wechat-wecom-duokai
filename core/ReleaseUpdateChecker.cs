using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WechatDuokai.Update;

namespace WechatDuokai.Core
{
    public sealed class ReleaseAssetInfo
    {
        public string Name { get; set; }

        public string DownloadUrl { get; set; }

        public long Size { get; set; }

        public string Sha256 { get; set; }
    }

    public sealed class ReleaseUpdateResult
    {
        public bool CheckSucceeded { get; set; }

        public bool IsUpdateAvailable { get; set; }

        public string LatestVersion { get; set; }

        public string ReleasePageUrl { get; set; }

        public ReleaseAssetInfo SetupAsset { get; set; }

        public ReleaseAssetInfo ChecksumAsset { get; set; }

        public string OneClickUpdateError { get; set; }

        public string ErrorMessage { get; set; }

        public bool CanInstallUpdate => IsUpdateAvailable && SetupAsset != null &&
                                        ChecksumAsset != null &&
                                        string.IsNullOrWhiteSpace(OneClickUpdateError);
    }

    public sealed class ReleaseUpdateChecker
    {
        public const string ReleasesUrl = "https://github.com/PascalePaF/wechat-wecom-duokai/releases";
        internal const long MaximumSetupBytes = 20L * 1024L * 1024L;
        internal const long MaximumChecksumBytes = 256L * 1024L;
        private const string LatestReleaseApi =
            "https://api.github.com/repos/PascalePaF/wechat-wecom-duokai/releases/latest";
        private const string ReleaseDownloadPrefix =
            "/PascalePaF/wechat-wecom-duokai/releases/download/";
        private static readonly Regex VersionPattern = new Regex(
            "^v?(?<version>[0-9]+(?:\\.[0-9]+){1,3})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public async Task<ReleaseUpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) })
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi))
                {
                    request.Headers.UserAgent.ParseAdd("wechat-duokai/" + (currentVersion ?? "unknown"));
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                    using (var response = await client.SendAsync(request,
                               HttpCompletionOption.ResponseContentRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        return ParseResponse(await response.Content.ReadAsStringAsync(), currentVersion);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return new ReleaseUpdateResult { ErrorMessage = ex.Message };
            }
        }

        internal static ReleaseUpdateResult ParseResponse(string body, string currentVersion)
        {
            GitHubRelease release;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(GitHubRelease));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body ?? string.Empty)))
                {
                    release = serializer.ReadObject(stream) as GitHubRelease;
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is ArgumentException)
            {
                return new ReleaseUpdateResult { ErrorMessage = "发布版本信息格式无法识别。" };
            }

            var tagMatch = VersionPattern.Match(release?.TagName ?? string.Empty);
            Version latest;
            Version current;
            if (release == null || release.Draft || release.Prerelease || !tagMatch.Success ||
                !Version.TryParse(tagMatch.Groups["version"].Value, out latest) ||
                !Version.TryParse(currentVersion, out current))
            {
                return new ReleaseUpdateResult { ErrorMessage = "发布版本信息格式无法识别。" };
            }

            var result = new ReleaseUpdateResult
            {
                CheckSucceeded = true,
                LatestVersion = latest.ToString(),
                IsUpdateAvailable = latest > current,
                ReleasePageUrl = IsTrustedReleasePage(release.HtmlUrl, release.TagName)
                    ? release.HtmlUrl
                    : ReleasesUrl
            };

            if (!result.IsUpdateAvailable)
            {
                return result;
            }

            var expectedSetupName = "wechat_duokai-setup-v" + result.LatestVersion + ".exe";
            var assets = release.Assets ?? new GitHubReleaseAsset[0];
            var setupCandidates = assets.Where(asset => string.Equals(asset.Name, expectedSetupName,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            var checksumCandidates = assets.Where(asset => string.Equals(asset.Name, "SHA256SUMS.txt",
                    StringComparison.OrdinalIgnoreCase)).ToArray();

            if (setupCandidates.Length != 1)
            {
                result.OneClickUpdateError = setupCandidates.Length == 0
                    ? "正式安装包不可用于一键更新：Release 中缺少 " + expectedSetupName
                    : "正式安装包不可用于一键更新：Release 中存在重名附件";
                return result;
            }
            if (checksumCandidates.Length != 1)
            {
                result.OneClickUpdateError = checksumCandidates.Length == 0
                    ? "校验文件不可用于一键更新：Release 中缺少 SHA256SUMS.txt"
                    : "校验文件不可用于一键更新：Release 中存在重名附件";
                return result;
            }

            string assetError;
            ReleaseAssetInfo setupAsset;
            ReleaseAssetInfo checksumAsset;
            if (!TryCreateAsset(setupCandidates[0], release.TagName, expectedSetupName, MaximumSetupBytes,
                    out setupAsset, out assetError))
            {
                result.OneClickUpdateError = "正式安装包不可用于一键更新：" + assetError;
                return result;
            }

            if (!TryCreateAsset(checksumCandidates[0], release.TagName, "SHA256SUMS.txt", MaximumChecksumBytes,
                    out checksumAsset, out assetError))
            {
                result.OneClickUpdateError = "校验文件不可用于一键更新：" + assetError;
                return result;
            }

            result.SetupAsset = setupAsset;
            result.ChecksumAsset = checksumAsset;
            return result;
        }

        private static bool TryCreateAsset(GitHubReleaseAsset source, string expectedTag,
            string expectedName,
            long maximumBytes, out ReleaseAssetInfo asset, out string error)
        {
            asset = null;
            error = null;
            if (source == null)
            {
                error = "Release 中缺少 " + expectedName;
                return false;
            }

            if (!string.Equals(source.State, "uploaded", StringComparison.OrdinalIgnoreCase) ||
                source.Size <= 0 || source.Size > maximumBytes)
            {
                error = "附件状态或大小不符合安全策略";
                return false;
            }

            Uri downloadUri;
            var expectedPath = ReleaseDownloadPrefix + expectedTag + "/" + expectedName;
            if (!Uri.TryCreate(source.BrowserDownloadUrl, UriKind.Absolute, out downloadUri) ||
                !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(downloadUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Uri.UnescapeDataString(downloadUri.AbsolutePath), expectedPath,
                    StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(downloadUri.Query) || !string.IsNullOrEmpty(downloadUri.Fragment))
            {
                error = "附件下载地址不是本项目的 GitHub Release";
                return false;
            }

            var digest = UpdatePlan.NormalizeSha256(source.Digest);
            if (!Regex.IsMatch(digest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                error = "GitHub 未提供有效的 SHA-256 摘要";
                return false;
            }

            asset = new ReleaseAssetInfo
            {
                Name = expectedName,
                DownloadUrl = downloadUri.AbsoluteUri,
                Size = source.Size,
                Sha256 = digest
            };
            return true;
        }

        private static bool IsTrustedReleasePage(string value, string expectedTag)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Uri.UnescapeDataString(uri.AbsolutePath),
                       "/PascalePaF/wechat-wecom-duokai/releases/tag/" + expectedTag,
                       StringComparison.Ordinal) &&
                   string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }

        [DataContract]
        private sealed class GitHubRelease
        {
            [DataMember(Name = "tag_name")]
            public string TagName { get; set; }

            [DataMember(Name = "html_url")]
            public string HtmlUrl { get; set; }

            [DataMember(Name = "draft")]
            public bool Draft { get; set; }

            [DataMember(Name = "prerelease")]
            public bool Prerelease { get; set; }

            [DataMember(Name = "assets")]
            public GitHubReleaseAsset[] Assets { get; set; }
        }

        [DataContract]
        private sealed class GitHubReleaseAsset
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "state")]
            public string State { get; set; }

            [DataMember(Name = "browser_download_url")]
            public string BrowserDownloadUrl { get; set; }

            [DataMember(Name = "size")]
            public long Size { get; set; }

            [DataMember(Name = "digest")]
            public string Digest { get; set; }
        }
    }
}
