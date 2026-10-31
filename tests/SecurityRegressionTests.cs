using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
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
                var policy = new WindowsWeComLaunchPolicy(@"Software\WechatDuokaiTests", "unused-test-gate", plain);
                Directory.CreateDirectory(Path.GetDirectoryName(policy.AuditLogPath));
                Assert(CreateHardLink(policy.AuditLogPath, sentinel, IntPtr.Zero), "Cannot create hard-linked audit log.");
                Invoke(typeof(WindowsWeComLaunchPolicy).GetMethod("AppendAudit", BindingFlags.Instance | BindingFlags.NonPublic),
                    policy, "BOUNDARY_TEST");
                Assert(File.ReadAllText(sentinel) == "preserve-me" && File.ReadAllText(policy.AuditLogPath).Contains("BOUNDARY_TEST"),
                    "Audit append modified a hard-linked external file.");
            }
            finally
            {
                if (Directory.Exists(linkedData)) Directory.Delete(linkedData, false);
                if (File.Exists(fileLink)) File.Delete(fileLink);
                Program.DeleteTestDirectoryWithRetries(root);
            }
        }

        private static void ExpectIOException(Action action)
        {
            try { action(); }
            catch (IOException) { return; }
            throw new Exception("An unsafe redirected path was accepted.");
        }

        internal static void SharedPersistenceBoundary()
        {
            var root = Path.Combine(Path.GetTempPath(), "wechat-duokai-shared-storage-" + Guid.NewGuid().ToString("N"));
            var outside = Path.Combine(root, "outside");
            var data = Path.Combine(root, "data");
            var linkedData = Path.Combine(root, "linked-data");
            var marker = Path.Combine(data, ApplicationStorage.DataMarkerName);
            var theme = Path.Combine(data, "theme.ini");
            var planPath = Path.Combine(data, "update.plan");
            try
            {
                Directory.CreateDirectory(outside);
                Directory.CreateDirectory(data);
                var sentinel = Path.Combine(outside, "sentinel.txt");
                File.WriteAllText(sentinel, "preserve-me");
                Assert(CreateSymbolicLink(linkedData, outside, 3) || CreateSymbolicLink(linkedData, outside, 1),
                    "Cannot create shared storage directory-link fixture.");
                foreach (var assembly in new[] { typeof(WechatDuokai.App.MainWindow).Assembly,
                             typeof(WechatDuokai.Installer.InstallerEngine).Assembly })
                {
                    var manager = assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
                    var preference = assembly.GetType("WechatDuokai.Presentation.AppThemePreference", true);
                    var dark = Enum.Parse(preference, "Dark");
                    var save = manager.GetMethod("SavePreferenceToDirectory", BindingFlags.Static | BindingFlags.NonPublic);
                    var load = manager.GetMethod("LoadPreferenceFromDirectory", BindingFlags.Static | BindingFlags.NonPublic);
                    ExpectIOException(() => Invoke(save, null, linkedData, dark));
                    Assert(CreateSymbolicLink(marker, sentinel, 2) || CreateSymbolicLink(marker, sentinel, 0),
                        "Cannot create redirected theme marker.");
                    ExpectIOException(() => Invoke(save, null, data, dark));
                    File.Delete(marker);
                    Invoke(save, null, data, dark);
                    Assert(Invoke(load, null, data).ToString() == "Dark", "Normal theme persistence failed.");
                    File.Delete(theme);
                    Assert(CreateSymbolicLink(theme, sentinel, 2) || CreateSymbolicLink(theme, sentinel, 0),
                        "Cannot create redirected theme preference.");
                    ExpectIOException(() => Invoke(save, null, data, dark));
                    Assert(Invoke(load, null, data).ToString() == "System", "Redirected theme preference was read.");
                    File.Delete(theme);
                    File.Delete(marker);
                    Assert(CreateHardLink(marker, sentinel, IntPtr.Zero), "Cannot create hard-linked theme marker.");
                    Invoke(save, null, data, dark);
                    Assert(File.ReadAllText(sentinel) == "preserve-me", "Theme persistence followed a hard link.");
                    File.Delete(theme);
                    File.Delete(marker);
                }

                foreach (var assembly in new[] { typeof(ApplicationUpdateService).Assembly,
                             typeof(WechatDuokai.Installer.InstallerEngine).Assembly })
                {
                    var type = assembly.GetType("WechatDuokai.Update.UpdatePlan", true);
                    var mode = assembly.GetType("WechatDuokai.Update.UpdateTargetMode", true);
                    var plan = Invoke(type.GetMethod("Create"), null, 123, DateTime.UtcNow.Ticks,
                        Enum.Parse(mode, "Portable"), "1.1.7", "1.1.8", root,
                        Path.Combine(root, "setup.exe"), new string('a', 64));
                    var write = type.GetMethod("WriteDurable");
                    var read = type.GetMethod("Read");
                    Assert(CreateSymbolicLink(planPath + ".new", sentinel, 2) ||
                           CreateSymbolicLink(planPath + ".new", sentinel, 0), "Cannot create old temporary-file trap.");
                    Invoke(write, plan, planPath);
                    Assert(Invoke(read, null, planPath) != null && File.ReadAllText(sentinel) == "preserve-me",
                        "Plan persistence followed the predictable temporary-file link.");
                    File.Delete(planPath);
                    Assert(CreateSymbolicLink(planPath, sentinel, 2) || CreateSymbolicLink(planPath, sentinel, 0),
                        "Cannot create redirected update plan.");
                    ExpectIOException(() => Invoke(write, plan, planPath));
                    ExpectIOException(() => Invoke(read, null, planPath));
                    File.Delete(planPath);
                    File.Delete(planPath + ".new");
                }
                Assert(Directory.GetFiles(outside).Length == 1 && File.ReadAllText(sentinel) == "preserve-me",
                    "Shared persistence escaped its data directory.");
            }
            finally
            {
                if (Directory.Exists(linkedData)) Directory.Delete(linkedData, false);
                foreach (var path in new[] { marker, theme, planPath, planPath + ".new" })
                    if (File.Exists(path)) File.Delete(path);
                Program.DeleteTestDirectoryWithRetries(root);
            }
        }

        private static object Invoke(MethodInfo method, object instance, params object[] arguments)
        {
            try { return method.Invoke(instance, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { throw exception.InnerException; }
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
