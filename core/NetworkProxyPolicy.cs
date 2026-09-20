using System;
using System.Net;
using System.Net.Http;

namespace WechatDuokai.Core
{
    internal static class NetworkProxyPolicy
    {
        internal static HttpClientHandler CreateHandler(bool allowAutoRedirect)
        {
            // .NET Framework can otherwise negotiate an older protocol through some
            // environment-defined CONNECT proxies even when Windows itself supports TLS 1.2.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler { AllowAutoRedirect = allowAutoRedirect };
            var proxyValue = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            if (string.IsNullOrWhiteSpace(proxyValue))
            {
                proxyValue = Environment.GetEnvironmentVariable("HTTP_PROXY");
            }
            var proxy = TryCreateEnvironmentProxy(proxyValue);
            if (proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = proxy;
            }
            return handler;
        }

        internal static WebProxy TryCreateEnvironmentProxy(string value)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp,
                     StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                     StringComparison.OrdinalIgnoreCase)) ||
                string.IsNullOrWhiteSpace(uri.Host) || uri.Port <= 0 ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath)))
            {
                return null;
            }

            try
            {
                var builder = new UriBuilder(uri.Scheme, uri.Host, uri.Port);
                var proxy = new WebProxy(builder.Uri) { BypassProxyOnLocal = true };
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var separator = uri.UserInfo.IndexOf(':');
                    var user = separator < 0 ? uri.UserInfo : uri.UserInfo.Substring(0, separator);
                    var password = separator < 0 ? string.Empty : uri.UserInfo.Substring(separator + 1);
                    proxy.Credentials = new NetworkCredential(
                        Uri.UnescapeDataString(user), Uri.UnescapeDataString(password));
                }
                return proxy;
            }
            catch (Exception ex) when (ex is UriFormatException || ex is ArgumentException)
            {
                return null;
            }
        }
    }
}
