using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    public sealed class ReleaseUpdateResult
    {
        public bool CheckSucceeded { get; set; }

        public bool IsUpdateAvailable { get; set; }

        public string LatestVersion { get; set; }

        public string ErrorMessage { get; set; }
    }

    public sealed class ReleaseUpdateChecker
    {
        public const string ReleasesUrl = "https://github.com/PascalePaF/wechat-wecom-duokai/releases";
        private const string LatestReleaseApi =
            "https://api.github.com/repos/PascalePaF/wechat-wecom-duokai/releases/latest";
        private static readonly Regex TagPattern = new Regex(
            "\\\"tag_name\\\"\\s*:\\s*\\\"v?(?<version>[0-9]+(?:\\.[0-9]+){1,3})\\\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public async Task<ReleaseUpdateResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi))
                {
                    request.Headers.UserAgent.ParseAdd("wechat-duokai/" + (currentVersion ?? "unknown"));
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken))
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
            var match = TagPattern.Match(body ?? string.Empty);
            Version latest;
            Version current;
            if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out latest) ||
                !Version.TryParse(currentVersion, out current))
            {
                return new ReleaseUpdateResult { ErrorMessage = "发布版本信息格式无法识别。" };
            }

            return new ReleaseUpdateResult
            {
                CheckSucceeded = true,
                LatestVersion = latest.ToString(),
                IsUpdateAvailable = latest > current
            };
        }
    }
}
