using System;
using System.IO;
using System.Linq;
using System.Net;
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

        public bool HasGitHubAssetDigests { get; set; }

        internal string ReleaseTag { get; set; }

        public bool CanInstallUpdate => IsUpdateAvailable && SetupAsset != null &&
                                        ChecksumAsset != null &&
                                        string.IsNullOrWhiteSpace(OneClickUpdateError);
    }

    public sealed class ReleaseUpdateChecker
    {
        public const string ReleasesUrl = "https://github.com/PascalePaF/wechat-wecom-duokai/releases";
        internal const long MaximumSetupBytes = 20L * 1024L * 1024L;
        internal const long MaximumChecksumBytes = 256L * 1024L;
        internal const string LatestReleasePage = ReleasesUrl + "/latest";
        private const string LatestReleaseApi =
            "https://api.github.com/repos/PascalePaF/wechat-wecom-duokai/releases/latest";
        private const string ReleaseTagPrefix =
            "/PascalePaF/wechat-wecom-duokai/releases/tag/";
        private const string ReleaseDownloadPrefix =
            "/PascalePaF/wechat-wecom-duokai/releases/download/";
        private static readonly Regex VersionPattern = new Regex(
            "^v?(?<version>[0-9]+(?:\\.[0-9]+){1,3})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private readonly Func<bool, HttpMessageHandler> _handlerFactory;

        public ReleaseUpdateChecker()
            : this(CreateDefaultHandler)
        {
        }

        internal ReleaseUpdateChecker(Func<bool, HttpMessageHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        }

        public async Task<ReleaseUpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
        {
            try
            {
                ReleaseUpdateResult latest;
                using (var client = CreateClient(false))
                {
                    latest = await ReadLatestReleasePageAsync(client, currentVersion, cancellationToken);
                    if (!latest.CheckSucceeded || !latest.IsUpdateAvailable)
                    {
                        return latest;
                    }

                    var apiResult = await TryReadApiReleaseAsync(client, currentVersion, cancellationToken);
                    if (apiResult != null)
                    {
                        if (apiResult.CheckSucceeded &&
                            string.Equals(apiResult.LatestVersion, latest.LatestVersion,
                                StringComparison.Ordinal))
                        {
                            return apiResult;
                        }

                        latest.OneClickUpdateError =
                            "GitHub Release 页面与 API 元数据不一致；为安全起见请从发布页手动更新。";
                        return latest;
                    }
                }

                using (var assetClient = CreateClient(true))
                {
                    await PopulateFallbackAssetMetadataAsync(assetClient, latest, cancellationToken);
                }
                return latest;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return CreateError("连接 GitHub 超时，请检查网络或代理后重试。");
            }
            catch (HttpRequestException)
            {
                return CreateError("无法连接 GitHub，请检查网络、代理或安全软件的 HTTPS 扫描后重试。");
            }
            catch (Exception ex)
            {
                return CreateError("检查更新时发生错误：" + ex.Message);
            }
        }

        private HttpClient CreateClient(bool allowAutoRedirect)
        {
            return new HttpClient(_handlerFactory(allowAutoRedirect), true)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
        }

        private static HttpMessageHandler CreateDefaultHandler(bool allowAutoRedirect)
        {
            return new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect };
        }

        private static async Task<ReleaseUpdateResult> ReadLatestReleasePageAsync(HttpClient client,
            string currentVersion, CancellationToken cancellationToken)
        {
            using (var request = CreateRequest(HttpMethod.Head, LatestReleasePage, currentVersion,
                       "text/html"))
            using (var response = await client.SendAsync(request,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    return await ReadLatestReleasePageWithGetAsync(client, currentVersion,
                        cancellationToken);
                }

                return ParseLatestReleaseResponse(response, currentVersion);
            }
        }

        private static async Task<ReleaseUpdateResult> ReadLatestReleasePageWithGetAsync(
            HttpClient client, string currentVersion, CancellationToken cancellationToken)
        {
            using (var request = CreateRequest(HttpMethod.Get, LatestReleasePage, currentVersion,
                       "text/html"))
            using (var response = await client.SendAsync(request,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                return ParseLatestReleaseResponse(response, currentVersion);
            }
        }

        private static ReleaseUpdateResult ParseLatestReleaseResponse(HttpResponseMessage response,
            string currentVersion)
        {
            if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
            {
                return CreateError("GitHub 未返回可识别的最新正式版本（HTTP " +
                                   (int)response.StatusCode + "）。");
            }

            var location = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(new Uri(LatestReleasePage), response.Headers.Location);
            return ParseLatestReleaseLocation(location.AbsoluteUri, currentVersion);
        }

        internal static ReleaseUpdateResult ParseLatestReleaseLocation(string location,
            string currentVersion)
        {
            Uri releaseUri;
            if (!Uri.TryCreate(location, UriKind.Absolute, out releaseUri) ||
                !string.Equals(releaseUri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(releaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(releaseUri.Query) || !string.IsNullOrEmpty(releaseUri.Fragment))
            {
                return CreateError("GitHub 返回的发布地址不可信，已停止检查。");
            }

            var path = Uri.UnescapeDataString(releaseUri.AbsolutePath);
            if (!path.StartsWith(ReleaseTagPrefix, StringComparison.Ordinal) ||
                path.Length <= ReleaseTagPrefix.Length)
            {
                return CreateError("GitHub 返回的正式版本地址无法识别。");
            }

            var tag = path.Substring(ReleaseTagPrefix.Length);
            if (tag.IndexOf('/') >= 0)
            {
                return CreateError("GitHub 返回的正式版本标签无法识别。");
            }

            var tagMatch = VersionPattern.Match(tag);
            Version latest;
            Version current;
            if (!tagMatch.Success ||
                !Version.TryParse(tagMatch.Groups["version"].Value, out latest) ||
                !Version.TryParse(currentVersion, out current))
            {
                return CreateError("GitHub 返回的版本号格式无法识别。");
            }

            return new ReleaseUpdateResult
            {
                CheckSucceeded = true,
                LatestVersion = latest.ToString(),
                IsUpdateAvailable = latest > current,
                ReleasePageUrl = releaseUri.AbsoluteUri,
                ReleaseTag = tag
            };
        }

        private static async Task<ReleaseUpdateResult> TryReadApiReleaseAsync(HttpClient client,
            string currentVersion, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = CreateRequest(HttpMethod.Get, LatestReleaseApi, currentVersion,
                           "application/vnd.github+json"))
                {
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                    using (var response = await client.SendAsync(request,
                               HttpCompletionOption.ResponseContentRead, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return null;
                        }
                        return ParseResponse(await response.Content.ReadAsStringAsync(), currentVersion);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return null;
            }
        }

        private static async Task PopulateFallbackAssetMetadataAsync(HttpClient client,
            ReleaseUpdateResult result, CancellationToken cancellationToken)
        {
            var setupName = "wechat_duokai-setup-v" + result.LatestVersion + ".exe";
            var setupUrl = BuildAssetUrl(result.ReleaseTag, setupName);
            var checksumUrl = BuildAssetUrl(result.ReleaseTag, "SHA256SUMS.txt");
            try
            {
                var setupSizeTask = ReadAssetSizeAsync(client, setupUrl, setupName,
                    MaximumSetupBytes, cancellationToken);
                var checksumSizeTask = ReadAssetSizeAsync(client, checksumUrl, "SHA256SUMS.txt",
                    MaximumChecksumBytes, cancellationToken);
                await Task.WhenAll(setupSizeTask, checksumSizeTask);

                result.SetupAsset = new ReleaseAssetInfo
                {
                    Name = setupName,
                    DownloadUrl = setupUrl,
                    Size = setupSizeTask.Result
                };
                result.ChecksumAsset = new ReleaseAssetInfo
                {
                    Name = "SHA256SUMS.txt",
                    DownloadUrl = checksumUrl,
                    Size = checksumSizeTask.Result
                };
                result.HasGitHubAssetDigests = false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.OneClickUpdateError =
                    "GitHub API 暂时不可用，且无法安全确认一键更新附件：" +
                    DescribeAssetError(ex);
            }
        }

        private static async Task<long> ReadAssetSizeAsync(HttpClient client, string url,
            string assetName, long maximumBytes, CancellationToken cancellationToken)
        {
            using (var request = CreateRequest(HttpMethod.Head, url, "metadata",
                       "application/octet-stream"))
            using (var response = await client.SendAsync(request,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidDataException(assetName + " 返回 HTTP " +
                                                   (int)response.StatusCode);
                }

                var finalUri = response.RequestMessage?.RequestUri;
                if (!IsTrustedAssetDeliveryUri(finalUri))
                {
                    throw new InvalidDataException(assetName + " 被重定向到非 GitHub 地址");
                }

                var size = response.Content.Headers.ContentLength;
                if (!size.HasValue || size.Value <= 0 || size.Value > maximumBytes)
                {
                    throw new InvalidDataException(assetName + " 未提供符合安全限制的文件大小");
                }
                return size.Value;
            }
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url,
            string currentVersion, string accept)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.UserAgent.ParseAdd("wechat-duokai/" +
                                               (currentVersion ?? "unknown"));
            request.Headers.Accept.ParseAdd(accept);
            return request;
        }

        private static string BuildAssetUrl(string tag, string assetName)
        {
            return "https://github.com" + ReleaseDownloadPrefix +
                   Uri.EscapeDataString(tag ?? string.Empty) + "/" +
                   Uri.EscapeDataString(assetName ?? string.Empty);
        }

        internal static bool IsTrustedAssetDeliveryUri(Uri value)
        {
            if (value == null ||
                !string.Equals(value.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(value.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value.Host, "release-assets.githubusercontent.com",
                       StringComparison.OrdinalIgnoreCase) ||
                   value.Host.EndsWith(".githubusercontent.com",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRedirect(HttpStatusCode statusCode)
        {
            var numeric = (int)statusCode;
            return numeric == 301 || numeric == 302 || numeric == 303 ||
                   numeric == 307 || numeric == 308;
        }

        private static string DescribeAssetError(Exception exception)
        {
            if (exception is TaskCanceledException)
            {
                return "连接 GitHub 超时";
            }
            if (exception is HttpRequestException)
            {
                return "无法连接 GitHub 附件服务";
            }
            return exception.Message;
        }

        private static ReleaseUpdateResult CreateError(string message)
        {
            return new ReleaseUpdateResult { ErrorMessage = message };
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
                ReleaseTag = release.TagName,
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
            result.HasGitHubAssetDigests = true;
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
