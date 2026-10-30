using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WechatDuokai.Core;

namespace WechatDuokai.Tests
{
    internal static class SecurityRegressionTests
    {
        internal static int VerifyPublishedUpdate(string currentVersion, string expectedVersion)
        {
            var marker = Path.Combine(ApplicationStorage.ApplicationDirectory, ApplicationUpdateService.PortableMarkerName);
            var markerCreated = false;
            try
            {
                Assert(!File.Exists(marker) && !File.Exists(Path.Combine(ApplicationStorage.ApplicationDirectory,
                    ApplicationUpdateService.InstallMarkerName)), "Live verification must run only in an unmarked test directory.");
                var release = new ReleaseUpdateChecker().CheckAsync(currentVersion, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert(release.CheckSucceeded && release.LatestVersion == expectedVersion && release.CanInstallUpdate,
                    "Published update discovery failed: " + release.ErrorMessage + " " + release.OneClickUpdateError);
                File.WriteAllText(marker, ApplicationUpdateService.PortableMarkerValue);
                markerCreated = true;
                var package = new ApplicationUpdateService().DownloadAndVerifyAsync(release, null, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert(package.Version == expectedVersion && package.Size == new FileInfo(package.PackagePath).Length,
                    "Verified package version or size mismatch.");
                Console.WriteLine("PASS: published update " + currentVersion + " -> " + expectedVersion +
                    "; downloaded setup SHA256=" + package.Sha256 + "; installer was not executed.");
                return 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
            finally { if (markerCreated && File.Exists(marker)) File.Delete(marker); }
        }

        internal static void PublisherIdentity()
        {
            Assert(ClientExecutableValidator.IsTencentPublisher(new X500DistinguishedName(
                "CN=Client, O=Tencent Technology (Shenzhen) Company Limited")), "Official organization rejected.");
            Assert(ClientExecutableValidator.IsTencentPublisher(new X500DistinguishedName(
                "CN=Client, O=\"Tencent Technology (Shenzhen) Co., Ltd.\"")), "Official punctuation variant rejected.");
            foreach (var subject in new[] {
                "CN=Tencent, O=Unrelated Company", "CN=Tencent Technology, O=NotTencent Technology",
                "CN=Client, O=Tencent Technology (Shenzhen) Company Limited Evil",
                "CN=Tencent Technology (Shenzhen) Company Limited", "CN=Client, OU=Tencent Technology, O=Unrelated"
                , "CN=\"Unrelated\nO=Tencent Technology (Shenzhen) Company Limited\""
            }) Assert(!ClientExecutableValidator.IsTencentPublisher(new X500DistinguishedName(subject)),
                "A lookalike or missing legal organization was accepted.");
        }

        internal static void ApiResponseLimit()
        {
            var result = new ReleaseUpdateChecker(_ => new OversizedApiHandler())
                .CheckAsync("1.0.0", CancellationToken.None).GetAwaiter().GetResult();
            Assert(!result.CheckSucceeded && result.ErrorMessage.Contains("大小"),
                "Oversized API metadata was not rejected before buffering.");
        }

        internal static void StreamingTimeouts()
        {
            var checker = new ReleaseUpdateChecker(_ => new StalledResponseHandler(), TimeSpan.FromMilliseconds(100));
            var watch = Stopwatch.StartNew();
            var result = checker.CheckAsync("1.0.0", CancellationToken.None).GetAwaiter().GetResult();
            Assert(!result.CheckSucceeded && result.ErrorMessage.Contains("超时") && watch.Elapsed < TimeSpan.FromSeconds(5),
                "A stalled response body escaped the check deadline.");
            using (var cancellation = new CancellationTokenSource(50))
            {
                try
                {
                    new ReleaseUpdateChecker(_ => new StalledResponseHandler(), TimeSpan.FromSeconds(5))
                        .CheckAsync("1.0.0", cancellation.Token).GetAwaiter().GetResult();
                    throw new Exception("Caller cancellation was swallowed.");
                }
                catch (OperationCanceledException) { }
            }

            var marker = Path.Combine(ApplicationStorage.ApplicationDirectory, ApplicationUpdateService.PortableMarkerName);
            Assert(!File.Exists(marker) && !File.Exists(Path.Combine(ApplicationStorage.ApplicationDirectory,
                ApplicationUpdateService.InstallMarkerName)), "Test directory unexpectedly contains a real install marker.");
            try
            {
                File.WriteAllText(marker, ApplicationUpdateService.PortableMarkerValue);
                var updater = new ApplicationUpdateService(() => new StalledResponseHandler(), TimeSpan.FromMilliseconds(100));
                try
                {
                    updater.DownloadAndVerifyAsync(new ReleaseUpdateResult {
                        IsUpdateAvailable = true, LatestVersion = "99.0.0",
                        SetupAsset = new ReleaseAssetInfo { Name = "wechat_duokai-setup-v99.0.0.exe", Size = 16,
                            DownloadUrl = ReleaseUpdateChecker.ReleasesUrl + "/download/v99.0.0/wechat_duokai-setup-v99.0.0.exe" },
                        ChecksumAsset = new ReleaseAssetInfo { Name = "SHA256SUMS.txt", Size = 16,
                            DownloadUrl = ReleaseUpdateChecker.ReleasesUrl + "/download/v99.0.0/SHA256SUMS.txt" }
                    }, null, CancellationToken.None).GetAwaiter().GetResult();
                    throw new Exception("A stalled download body escaped the deadline.");
                }
                catch (TimeoutException) { }
            }
            finally { if (File.Exists(marker)) File.Delete(marker); }
        }

        internal static void LocalStorageBoundary()
        {
            var root = Path.Combine(Path.GetTempPath(), "wechat-duokai-boundary-" + Guid.NewGuid().ToString("N"));
            var application = Path.Combine(root, "application");
            var outside = Path.Combine(root, "outside");
            var linkedData = Path.Combine(application, "data");
            var fileLink = Path.Combine(root, "plain", "settings.ini");
            try
            {
                Directory.CreateDirectory(application);
                Directory.CreateDirectory(outside);
                var sentinel = Path.Combine(outside, "sentinel.txt");
                File.WriteAllText(sentinel, "preserve-me");
                if (!CreateSymbolicLink(linkedData, outside, 3) && !CreateSymbolicLink(linkedData, outside, 1))
                    throw new Exception("Cannot create directory-link regression fixture: " + Marshal.GetLastWin32Error());
                ExpectIOException(() => new DiagnosticReportService().CreateReport(application, null, null, new InstanceManager()));
                ExpectIOException(() => UserPreferences.SaveTargetCount(linkedData, 4));
                Assert(!Directory.Exists(Path.Combine(outside, "diagnostics")) &&
                       Directory.GetFiles(outside).Length == 1, "Storage wrote outside the application boundary.");

                var plain = Path.GetDirectoryName(fileLink);
                Directory.CreateDirectory(plain);
                File.WriteAllText(Path.Combine(plain, ApplicationStorage.DataMarkerName), ApplicationStorage.DataMarkerValue);
                if (!CreateSymbolicLink(fileLink, sentinel, 2) && !CreateSymbolicLink(fileLink, sentinel, 0))
                    throw new Exception("Cannot create file-link regression fixture.");
                ExpectIOException(() => UserPreferences.SaveTargetCount(plain, 4));
                ExpectIOException(() => LocalFileSafety.WriteTextAtomic(fileLink, "overwritten"));
                Assert(File.ReadAllText(sentinel) == "preserve-me", "A linked file was overwritten.");

                File.Delete(fileLink);
                Assert(CreateHardLink(fileLink, sentinel, IntPtr.Zero), "Cannot create hard-link fixture.");
                LocalFileSafety.WriteTextAtomic(fileLink, "new-data");
                Assert(File.ReadAllText(sentinel) == "preserve-me" && File.ReadAllText(fileLink) == "new-data",
                    "Atomic replacement modified the hard-linked external file.");
            }
            finally
            {
                if (Directory.Exists(linkedData)) Directory.Delete(linkedData, false);
                if (File.Exists(fileLink)) File.Delete(fileLink);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ExpectIOException(Action action)
        {
            try { action(); }
            catch (IOException) { return; }
            throw new Exception("An unsafe redirected path was accepted.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class StalledResponseHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = request.Method == HttpMethod.Head
                    ? new HttpResponseMessage(HttpStatusCode.Found)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) };
                response.RequestMessage = request;
                if (request.Method == HttpMethod.Head)
                    response.Headers.Location = new Uri(ReleaseUpdateChecker.ReleasesUrl + "/download/v99.0.0/update-manifest.json");
                else response.Content.Headers.ContentLength = 16;
                return Task.FromResult(response);
            }
        }

        private sealed class OversizedApiHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
                if (request.RequestUri.AbsoluteUri == ReleaseUpdateChecker.LatestReleasePage)
                {
                    response.StatusCode = HttpStatusCode.Found;
                    response.Headers.Location = new Uri(ReleaseUpdateChecker.ReleasesUrl + "/tag/v99.0.0");
                }
                else if (request.RequestUri.Host == "api.github.com")
                {
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new ByteArrayContent(new byte[0]);
                    response.Content.Headers.ContentLength = ReleaseUpdateChecker.MaximumApiResponseBytes + 1;
                }
                return Task.FromResult(response);
            }
        }

        private sealed class StalledStream : Stream
        {
            private readonly TaskCompletionSource<int> _pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                // Deliberately ignore the token to model older Framework socket streams.
                return _pending.Task;
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _pending.TrySetException(new IOException("Simulated socket closed."));
                base.Dispose(disposing);
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CreateSymbolicLink(string link, string target, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string link, string target, IntPtr securityAttributes);
    }
}
