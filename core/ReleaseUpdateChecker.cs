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

        public bool HasReleaseManifestHashes { get; set; }

        public DateTimeOffset? RetryAfterUtc { get; set; }

        internal string ReleaseTag { get; set; }

        internal bool LegacyFallbackAllowed { get; set; }

        public bool CanInstallUpdate => IsUpdateAvailable && SetupAsset != null &&
                                        ChecksumAsset != null &&
                                        string.IsNullOrWhiteSpace(OneClickUpdateError);
    }

    public sealed class ReleaseUpdateChecker
    {
        public const string ReleasesUrl = "https://github.com/PascalePaF/wechat-wecom-duokai/releases";
        internal const long MaximumSetupBytes = 20L * 1024L * 1024L;
        internal const long MaximumChecksumBytes = 256L * 1024L;
        internal const long MaximumManifestBytes = 64L * 1024L;
        internal const string LatestReleasePage = ReleasesUrl + "/latest";
        internal const string LatestUpdateManifest = ReleasesUrl + "/latest/download/update-manifest.json";
        private const string LatestReleaseApi =
            "https://api.github.com/repos/PascalePaF/wechat-wecom-duokai/releases/latest";
        private const string ReleaseTagPrefix =
            "/PascalePaF/wechat-wecom-duokai/releases/tag/";
        private const string ReleaseDownloadPrefix =
            "/PascalePaF/wechat-wecom-duokai/releases/download/";
        private const string ProductId = "wechat-duokai";
        private static readonly Regex VersionPattern = new Regex(
            "^v?(?<version>[0-9]+(?:\\.[0-9]+){1,3})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex Sha256Pattern = new Regex(
            "^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly Func<bool, HttpMessageHandler> _handlerFactory;

        public ReleaseUpdateChecker()
            : this(CreateDefaultHandler)
        {
        }

        internal ReleaseUpdateChecker(Func<bool, HttpMessageHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        }

        public async Task<ReleaseUpdateResult> CheckAsync(string currentVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                ReleaseUpdateResult manifestResult;
                using (var discoveryClient = CreateClient(false))
                using (var deliveryClient = CreateClient(true))
                {
                    manifestResult = await TryReadUpdateManifestAsync(discoveryClient,
                        deliveryClient, currentVersion, cancellationToken);
                    if (manifestResult != null && manifestResult.CheckSucceeded)
                    {
                        if (!manifestResult.IsUpdateAvailable || !manifestResult.CanInstallUpdate)
                        {
                            return manifestResult;
                        }

                        var apiResult = await TryReadApiReleaseAsync(discoveryClient,
                            currentVersion, cancellationToken);
                        if (apiResult != null && apiResult.CheckSucceeded &&
                            apiResult.IsUpdateAvailable)
                        {
                            if (!string.Equals(apiResult.LatestVersion,
                                    manifestResult.LatestVersion, StringComparison.Ordinal))
                            {
                                manifestResult.OneClickUpdateError =
                                    "静态更新清单与 GitHub API 的版本不一致；为安全起见请从发布页手动更新。";
                                return manifestResult;
                            }

                            if (apiResult.CanInstallUpdate)
                            {
                                if (!AssetsMatch(manifestResult.SetupAsset, apiResult.SetupAsset) ||
                                    !AssetsMatch(manifestResult.ChecksumAsset,
                                        apiResult.ChecksumAsset))
                                {
                                    manifestResult.OneClickUpdateError =
                                        "静态更新清单与 GitHub 服务端附件摘要不一致；已停止一键更新。";
                                    return manifestResult;
                                }
                                manifestResult.HasGitHubAssetDigests = true;
                            }
                        }
                        return manifestResult;
                    }
                    if (manifestResult != null && !manifestResult.LegacyFallbackAllowed)
                    {
                        // A Release that publishes a malformed or conflicting manifest must
                        // fail closed. The legacy path is only for Releases with no manifest.
                        return manifestResult;
                    }
                }

                // V1.1.1 and older Releases have no update-manifest.json. Keep the
                // quota-free Release redirect as a compatibility bridge.
                ReleaseUpdateResult latest;
                using (var client = CreateClient(false))
                {
                    latest = await ReadLatestReleasePageAsync(client, currentVersion,
                        cancellationToken);
                    if (!latest.CheckSucceeded)
                    {
                        return PreferRetryInformation(latest, manifestResult);
                    }
                    if (!latest.IsUpdateAvailable)
                    {
                        return latest;
                    }

                    var apiResult = await TryReadApiReleaseAsync(client, currentVersion,
                        cancellationToken);
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
                    await PopulateFallbackAssetMetadataAsync(assetClient, latest,
                        cancellationToken);
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
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        private static HttpMessageHandler CreateDefaultHandler(bool allowAutoRedirect)
        {
            return NetworkProxyPolicy.CreateHandler(allowAutoRedirect);
        }

        private static async Task<ReleaseUpdateResult> TryReadUpdateManifestAsync(
            HttpClient discoveryClient, HttpClient deliveryClient, string currentVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var request = CreateRequest(HttpMethod.Head, LatestUpdateManifest,
                           currentVersion, "application/json"))
                using (var response = await discoveryClient.SendAsync(request,
                           HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
                    {
                        var missing = response.StatusCode == HttpStatusCode.NotFound;
                        var error = CreateHttpError(
                            missing
                                ? "当前正式版本尚未提供静态更新清单。"
                                : "GitHub 未返回可识别的静态更新清单（HTTP " +
                                  (int)response.StatusCode + "）。",
                            response);
                        error.LegacyFallbackAllowed = missing;
                        return error;
                    }

                    var location = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(new Uri(LatestUpdateManifest), response.Headers.Location);
                    var discovery = ParseManifestLocation(location.AbsoluteUri, currentVersion);
                    if (!discovery.CheckSucceeded || !discovery.IsUpdateAvailable)
                    {
                        return discovery;
                    }

                    using (var manifestRequest = CreateRequest(HttpMethod.Get,
                               location.AbsoluteUri, currentVersion, "application/json"))
                    using (var manifestResponse = await deliveryClient.SendAsync(manifestRequest,
                               HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        if (!manifestResponse.IsSuccessStatusCode)
                        {
                            var error = CreateHttpError("静态更新清单下载失败（HTTP " +
                                                       (int)manifestResponse.StatusCode + "）。",
                                manifestResponse);
                            error.LegacyFallbackAllowed =
                                manifestResponse.StatusCode == HttpStatusCode.NotFound;
                            return error;
                        }
                        if (!IsTrustedAssetDeliveryUri(
                                manifestResponse.RequestMessage?.RequestUri))
                        {
                            return CreateError("静态更新清单被重定向到非 GitHub 地址，已停止检查。");
                        }

                        var bytes = await ReadBoundedContentAsync(manifestResponse,
                            MaximumManifestBytes, cancellationToken);
                        return ParseUpdateManifest(bytes, discovery.ReleaseTag, currentVersion);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return CreateError("读取静态更新清单超时。");
            }
            catch (HttpRequestException)
            {
                return CreateError("无法连接 GitHub 静态更新清单。");
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
            {
                return CreateError("静态更新清单无法安全读取：" + ex.Message);
            }
        }

        private static async Task<byte[]> ReadBoundedContentAsync(HttpResponseMessage response,
            long maximumBytes, CancellationToken cancellationToken)
        {
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue &&
                (contentLength.Value <= 0 || contentLength.Value > maximumBytes))
            {
                throw new InvalidDataException("文件大小不符合安全限制");
            }

            using (var input = await response.Content.ReadAsStreamAsync())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var read = await input.ReadAsync(buffer, 0, buffer.Length,
                        cancellationToken);
                    if (read == 0) break;
                    if (output.Length + read > maximumBytes)
                    {
                        throw new InvalidDataException("文件大小超过安全限制");
                    }
                    output.Write(buffer, 0, read);
                }
                if (output.Length == 0 ||
                    (contentLength.HasValue && contentLength.Value != output.Length))
                {
                    throw new InvalidDataException("文件大小与响应元数据不一致");
                }
                return output.ToArray();
            }
        }

        internal static ReleaseUpdateResult ParseManifestLocation(string location,
            string currentVersion)
        {
            Uri manifestUri;
            if (!Uri.TryCreate(location, UriKind.Absolute, out manifestUri) ||
                !string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifestUri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(manifestUri.Query) ||
                !string.IsNullOrEmpty(manifestUri.Fragment))
            {
                return CreateError("GitHub 返回的静态更新清单地址不可信，已停止检查。");
            }

            var path = Uri.UnescapeDataString(manifestUri.AbsolutePath);
            if (!path.StartsWith(ReleaseDownloadPrefix, StringComparison.Ordinal) ||
                !path.EndsWith("/update-manifest.json", StringComparison.Ordinal))
            {
                return CreateError("GitHub 返回的静态更新清单地址无法识别。");
            }

            var remainder = path.Substring(ReleaseDownloadPrefix.Length);
            var separator = remainder.IndexOf('/');
            if (separator <= 0 ||
                !string.Equals(remainder.Substring(separator + 1),
                    "update-manifest.json", StringComparison.Ordinal))
            {
                return CreateError("GitHub 返回的静态更新清单标签无法识别。");
            }

            var tag = remainder.Substring(0, separator);
            return CreateVersionResult(tag, currentVersion,
                ReleasesUrl + "/tag/" + Uri.EscapeDataString(tag));
        }

        internal static ReleaseUpdateResult ParseUpdateManifest(byte[] bytes,
            string expectedTag, string currentVersion)
        {
            UpdateManifest manifest;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(UpdateManifest));
                using (var stream = new MemoryStream(bytes ?? new byte[0]))
                {
                    manifest = serializer.ReadObject(stream) as UpdateManifest;
                    if (stream.Position != stream.Length)
                    {
                        return CreateError("静态更新清单包含无法识别的尾随内容。");
                    }
                }
            }
            catch (Exception ex) when (ex is SerializationException ||
                                       ex is ArgumentException || ex is InvalidDataException)
            {
                return CreateError("静态更新清单格式无法识别。");
            }

            var tagMatch = VersionPattern.Match(expectedTag ?? string.Empty);
            Version latest;
            Version current;
            Version minimumUpdater;
            if (manifest == null || manifest.SchemaVersion != 1 ||
                !string.Equals(manifest.Product, ProductId, StringComparison.Ordinal) ||
                !string.Equals(manifest.Tag, expectedTag, StringComparison.Ordinal) ||
                !tagMatch.Success ||
                !string.Equals(manifest.Version, tagMatch.Groups["version"].Value,
                    StringComparison.Ordinal) ||
                !Version.TryParse(manifest.Version, out latest) ||
                !Version.TryParse(currentVersion, out current) ||
                !Version.TryParse(manifest.MinimumUpdaterVersion, out minimumUpdater))
            {
                return CreateError("静态更新清单的产品、版本或格式无法识别。");
            }

            var expectedReleasePage = ReleasesUrl + "/tag/" + expectedTag;
            if (!string.Equals(manifest.ReleasePage, expectedReleasePage,
                    StringComparison.Ordinal))
            {
                return CreateError("静态更新清单的发布页地址不可信。");
            }

            var result = new ReleaseUpdateResult
            {
                CheckSucceeded = true,
                LatestVersion = latest.ToString(),
                IsUpdateAvailable = latest > current,
                ReleasePageUrl = expectedReleasePage,
                ReleaseTag = expectedTag,
                HasReleaseManifestHashes = true
            };
            if (!result.IsUpdateAvailable)
            {
                return result;
            }

            if (current < minimumUpdater)
            {
                result.OneClickUpdateError = "当前更新器版本低于此 Release 的最低要求，请从发布页手动更新一次。";
                return result;
            }

            var setupName = "wechat_duokai-setup-v" + result.LatestVersion + ".exe";
            string assetError;
            ReleaseAssetInfo setup;
            ReleaseAssetInfo checksums;
            if (!TryCreateManifestAsset(manifest.Setup, expectedTag, setupName,
                    MaximumSetupBytes, out setup, out assetError))
            {
                result.OneClickUpdateError = "静态清单中的正式安装包不可用：" + assetError;
                return result;
            }
            if (!TryCreateManifestAsset(manifest.Checksums, expectedTag,
                    "SHA256SUMS.txt", MaximumChecksumBytes, out checksums, out assetError))
            {
                result.OneClickUpdateError = "静态清单中的校验文件不可用：" + assetError;
                return result;
            }

            result.SetupAsset = setup;
            result.ChecksumAsset = checksums;
            return result;
        }

        private static bool TryCreateManifestAsset(UpdateManifestAsset source,
            string expectedTag, string expectedName, long maximumBytes,
            out ReleaseAssetInfo asset, out string error)
        {
            asset = null;
            error = null;
            if (source == null ||
                !string.Equals(source.Name, expectedName, StringComparison.Ordinal) ||
                source.Size <= 0 || source.Size > maximumBytes)
            {
                error = "附件名称或大小不符合安全策略";
                return false;
            }

            var expectedUrl = BuildAssetUrl(expectedTag, expectedName);
            if (!string.Equals(source.DownloadUrl, expectedUrl, StringComparison.Ordinal))
            {
                error = "附件下载地址不是本项目的规范 GitHub Release 地址";
                return false;
            }

            var digest = UpdatePlan.NormalizeSha256(source.Sha256);
            if (!Sha256Pattern.IsMatch(digest))
            {
                error = "附件未提供有效的 SHA-256 摘要";
                return false;
            }

            asset = new ReleaseAssetInfo
            {
                Name = expectedName,
                DownloadUrl = expectedUrl,
                Size = source.Size,
                Sha256 = digest
            };
            return true;
        }

        private static bool AssetsMatch(ReleaseAssetInfo left, ReleaseAssetInfo right)
        {
            return left != null && right != null &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.DownloadUrl, right.DownloadUrl, StringComparison.Ordinal) &&
                   left.Size == right.Size &&
                   FixedTimeEquals(left.Sha256, right.Sha256);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftHash = UpdatePlan.NormalizeSha256(left);
            var rightHash = UpdatePlan.NormalizeSha256(right);
            if (leftHash.Length != rightHash.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < leftHash.Length; index++)
            {
                difference |= leftHash[index] ^ rightHash[index];
            }
            return difference == 0;
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
                return CreateHttpError("GitHub 未返回可识别的最新正式版本（HTTP " +
                                       (int)response.StatusCode + "）。", response);
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

            return CreateVersionResult(tag, currentVersion, releaseUri.AbsoluteUri);
        }

        private static ReleaseUpdateResult CreateVersionResult(string tag,
            string currentVersion, string releasePage)
        {
            var tagMatch = VersionPattern.Match(tag ?? string.Empty);
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
                ReleasePageUrl = releasePage,
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
                result.HasReleaseManifestHashes = false;
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

        private static ReleaseUpdateResult PreferRetryInformation(ReleaseUpdateResult primary,
            ReleaseUpdateResult manifest)
        {
            if (primary == null)
            {
                return manifest ?? CreateError("暂时无法读取 GitHub 版本信息。");
            }
            if (!primary.RetryAfterUtc.HasValue && manifest?.RetryAfterUtc != null)
            {
                primary.RetryAfterUtc = manifest.RetryAfterUtc;
            }
            return primary;
        }

        private static ReleaseUpdateResult CreateHttpError(string message,
            HttpResponseMessage response)
        {
            return new ReleaseUpdateResult
            {
                ErrorMessage = message,
                RetryAfterUtc = ParseRetryAfter(response, DateTimeOffset.UtcNow)
            };
        }

        internal static DateTimeOffset? ParseRetryAfter(HttpResponseMessage response,
            DateTimeOffset now)
        {
            var retry = response?.Headers?.RetryAfter;
            if (retry?.Date != null && retry.Date.Value > now)
            {
                return retry.Date.Value;
            }
            if (retry?.Delta != null && retry.Delta.Value > TimeSpan.Zero)
            {
                return now.Add(retry.Delta.Value);
            }

            if (response != null && response.Headers.TryGetValues("X-RateLimit-Reset",
                    out var values))
            {
                long seconds;
                var text = values.FirstOrDefault();
                if (long.TryParse(text, out seconds))
                {
                    try
                    {
                        var value = DateTimeOffset.FromUnixTimeSeconds(seconds);
                        if (value > now) return value;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }
            }
            return null;
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
            if (!TryCreateAsset(setupCandidates[0], release.TagName, expectedSetupName,
                    MaximumSetupBytes, out setupAsset, out assetError))
            {
                result.OneClickUpdateError = "正式安装包不可用于一键更新：" + assetError;
                return result;
            }

            if (!TryCreateAsset(checksumCandidates[0], release.TagName, "SHA256SUMS.txt",
                    MaximumChecksumBytes, out checksumAsset, out assetError))
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
            string expectedName, long maximumBytes, out ReleaseAssetInfo asset, out string error)
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
            if (!Sha256Pattern.IsMatch(digest))
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
        private sealed class UpdateManifest
        {
            [DataMember(Name = "schemaVersion")]
            public int SchemaVersion { get; set; }

            [DataMember(Name = "product")]
            public string Product { get; set; }

            [DataMember(Name = "version")]
            public string Version { get; set; }

            [DataMember(Name = "tag")]
            public string Tag { get; set; }

            [DataMember(Name = "releasePage")]
            public string ReleasePage { get; set; }

            [DataMember(Name = "minimumUpdaterVersion")]
            public string MinimumUpdaterVersion { get; set; }

            [DataMember(Name = "setup")]
            public UpdateManifestAsset Setup { get; set; }

            [DataMember(Name = "checksums")]
            public UpdateManifestAsset Checksums { get; set; }
        }

        [DataContract]
        private sealed class UpdateManifestAsset
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "downloadUrl")]
            public string DownloadUrl { get; set; }

            [DataMember(Name = "size")]
            public long Size { get; set; }

            [DataMember(Name = "sha256")]
            public string Sha256 { get; set; }
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
