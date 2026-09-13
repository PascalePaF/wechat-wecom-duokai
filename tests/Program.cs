using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WechatDuokai.App;
using WechatDuokai.Core;
using WechatDuokai.Installer;

namespace WechatDuokai.Tests
{
    internal static class Program
    {
        private const string MutexName = "_WeChat_App_Instance_Identity_Mutex_Name";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--hold-mutex") return HoldMutex(args[1]);
            if (args.Length == 2 && args[0] == "--hold-file") return HoldFile(args[1]);
            if (args.Length >= 4 && args[0] == "--snapshot")
                return SaveUiSnapshot(args[1], args[2], args[3], args.Length >= 6 ? args[4] : null, args.Length >= 6 ? args[5] : null);
            if (args.Length == 1 && args[0] == "--idle") { Thread.Sleep(30000); return 0; }
            if (args.Length == 1 && args[0] == "--test-install") return TestInstall();
            if (args.Length == 1 && args[0] == "--cleanup-test-install") return CleanupTestInstall();

            try
            {
                BootstrapWpf(typeof(MainWindow), "Light");
                Run("Null application has zero instances", () =>
                    Assert(new InstanceManager().GetInstanceCount(null) == 0, "Expected zero."));
                Run("Target count cache survives a restart", TestPreferenceRoundTrip);
                Run("Renamed executables cannot impersonate an official client", TestClientExecutableValidation);
                Run("Process environment groups roots and supports deterministic launch tests", TestProcessEnvironmentAbstraction);
                Run("Diagnostics stay inside the selected local application directory", TestLocalDiagnostics);
                Run("GitHub release metadata never performs an in-app update", TestReleaseMetadataParsing);
                Run("Cleanup refuses drive roots", () =>
                    Assert(!InstallerEngine.ValidateSourceRoot(Path.GetPathRoot(Environment.SystemDirectory)),
                        "A drive root must never be accepted as a source directory."));
                Run("Project source marker is recognized", () =>
                    Assert(InstallerEngine.ValidateSourceRoot(FindProjectRoot()), "Expected marked source root."));
                Run("Custom installation paths are validated safely", TestCustomInstallPathValidation);
                Run("Installer identifies only the exact running target before overwrite", TestRunningInstallDetection);
                Run("All release windows use native movable title bars", TestNativeWindowChrome);
                Run("Main window responds from minimum size through maximized layouts", TestResponsiveLayoutMatrix);
                Run("Install cannot launch before explicit confirmation", TestExplicitLaunchPolicy);
                Run("Installer embeds the exact application and core payload", TestEmbeddedPayloads);
                Run("Setup and cleanup are separate compiled identities", TestSeparateInstallerIdentities);
                Run("Known WeChat mutex can be released without terminating its process", TestMutexRelease);
                Run("Modern WeChat lock file can be released without terminating its process", TestFileLockRelease);
                Console.WriteLine("All smoke tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int HoldMutex(string readyFile)
        {
            var mutex = new Mutex(true, MutexName, out var created);
            File.WriteAllText(readyFile, created ? "ready" : "not-created");
            Thread.Sleep(30000);
            GC.KeepAlive(mutex);
            return 0;
        }

        private static int HoldFile(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            File.WriteAllText(filePath + ".ready", "ready");
            Thread.Sleep(30000);
            GC.KeepAlive(stream);
            return 0;
        }

        private static void BootstrapWpf(Type anchorType, string themeName)
        {
            if (Application.Current == null)
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/" + anchorType.Assembly.GetName().Name +
                                 ";component/Themes/ThemeResources.xaml", UriKind.Absolute)
            });
            SetAssemblyTheme(anchorType, themeName);
        }

        private static void SetAssemblyTheme(Type anchorType, string themeName)
        {
            var managerType = anchorType.Assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
            var themeType = anchorType.Assembly.GetType("WechatDuokai.Presentation.AppTheme", true);
            var themeValue = Enum.Parse(themeType, themeName);
            var method = managerType.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(method != null, "Theme manager entry point is missing.");
            method.Invoke(null, new[] { themeValue, (object)false });
        }

        private static int SaveUiSnapshot(string windowName, string outputPath, string themeName, string requestedWidth, string requestedHeight)
        {
            Type windowType;
            var showInstallerCompletion = false;
            switch (windowName.ToLowerInvariant())
            {
                case "main": windowType = typeof(MainWindow); break;
                case "installer": windowType = typeof(InstallWindow); break;
                case "installer-complete":
                    windowType = typeof(InstallWindow);
                    showInstallerCompletion = true;
                    break;
                case "uninstaller": windowType = LoadCleanupWindowType(); break;
                default: throw new ArgumentException("Unknown window: " + windowName);
            }

            BootstrapWpf(windowType, themeName);
            var window = (Window)Activator.CreateInstance(windowType);
            try
            {
                double width;
                double height;
                if (double.TryParse(requestedWidth, out width)) window.Width = Math.Max(window.MinWidth, width);
                if (double.TryParse(requestedHeight, out height)) window.Height = Math.Max(window.MinHeight, height);
                if (showInstallerCompletion)
                {
                    var method = typeof(InstallWindow).GetMethod("EnterCompletedState",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert(method != null, "Installer completion state is missing.");
                    method.Invoke(window, null);
                }
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Thread.Sleep(250);
                window.UpdateLayout();
                var renderedWidth = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
                var renderedHeight = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
                var bitmap = new RenderTargetBitmap(renderedWidth, renderedHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    encoder.Save(output);
                }
            }
            finally
            {
                window.Close();
                Application.Current.Shutdown();
            }
            return 0;
        }

        private static void TestPreferenceRoundTrip()
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "wechat-duokai-preferences-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert(UserPreferences.LoadTargetCount(testDirectory) == 2, "Missing settings should default to 2.");
                UserPreferences.SaveTargetCount(testDirectory, 7);
                Assert(UserPreferences.LoadTargetCount(testDirectory) == 7, "Saved target count was not loaded.");
                var custom = Path.Combine(testDirectory, "Weixin.exe");
                UserPreferences.SaveCustomClientPath(testDirectory, AppKind.WeChat, custom);
                Assert(UserPreferences.LoadCustomClientPath(testDirectory, AppKind.WeChat) == Path.GetFullPath(custom),
                    "Custom client path was not preserved.");
                Assert(UserPreferences.HasValidMarker(testDirectory), "User-data marker is invalid.");
            }
            finally
            {
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void TestNativeWindowChrome()
        {
            var windows = new Window[]
            {
                new MainWindow(),
                new InstallWindow(),
                (Window)Activator.CreateInstance(LoadCleanupWindowType())
            };
            try
            {
                foreach (var window in windows)
                {
                    Assert(window.WindowStyle != WindowStyle.None, window.GetType().Name + " must use a native title bar.");
                    Assert(window.ResizeMode == ResizeMode.CanResize,
                        window.GetType().Name + " must support move, minimize, maximize and resize.");
                    var themeButton = window.FindName("ThemeButton") as DependencyObject;
                    Assert(themeButton != null &&
                           string.Equals(AutomationProperties.GetName(themeButton), "切换日间或夜间主题", StringComparison.Ordinal),
                        window.GetType().Name + " theme switch is missing.");
                }
            }
            finally
            {
                foreach (var window in windows) window.Close();
            }
        }

        private static void TestResponsiveLayoutMatrix()
        {
            var window = new MainWindow();
            try
            {
                Assert(window.MinWidth <= 680 && window.MinHeight <= 520, "Minimum main-window size is too large.");
                Assert(double.IsPositiveInfinity(window.MaxWidth) && double.IsPositiveInfinity(window.MaxHeight),
                    "Main window must not impose a fixed maximum size.");
                var method = typeof(MainWindow).GetMethod("ApplyResponsiveLayout",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(method != null, "Responsive layout method is missing.");
                var panel = (FrameworkElement)window.FindName("CountPanel");
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.ShowInTaskbar = false;
                window.Show();
                foreach (var size in new[]
                {
                    new System.Windows.Size(680, 520), new System.Windows.Size(790, 600),
                    new System.Windows.Size(960, 640), new System.Windows.Size(1280, 800),
                    new System.Windows.Size(1920, 1080)
                })
                {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    method.Invoke(window, new object[] { size.Width });
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    window.UpdateLayout();
                    Assert(panel.ActualWidth > 0 && panel.ActualHeight > 0,
                        "Count panel disappeared at " + size.Width + "x" + size.Height + ".");
                }

                method.Invoke(window, new object[] { 700d });
                Assert(Grid.GetRow(panel) == 1 && Grid.GetColumnSpan(panel) == 3,
                    "Compact layout must stack the count panel.");
                method.Invoke(window, new object[] { 1200d });
                Assert(Grid.GetRow(panel) == 0 && Grid.GetColumn(panel) == 2,
                    "Wide layout must keep the count panel on the right.");
            }
            finally
            {
                window.Close();
            }
        }

        private static void TestProcessEnvironmentAbstraction()
        {
            var path = Path.Combine(Path.GetTempPath(), "Weixin.exe");
            var environment = new FakeProcessEnvironment(path);
            var manager = new InstanceManager(environment);
            var app = new AppDefinition(AppKind.WeChat, "微信", path);
            Assert(manager.GetApplicationProcessIds(app).Count == 3, "Exact-path processes were not selected.");
            Assert(manager.GetInstanceCount(app) == 2, "Parent/child process roots were not grouped correctly.");
        }

        private static void TestClientExecutableValidation()
        {
            var folder = Path.Combine(Path.GetTempPath(), "wechat-duokai-client-validation-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var renamed = Path.Combine(folder, "Weixin.exe");
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, renamed);
                var validation = ClientExecutableValidator.Validate(renamed, AppKind.WeChat);
                Assert(!validation.IsValid, "A renamed unsigned test executable was accepted as WeChat.");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static void TestLocalDiagnostics()
        {
            var folder = Path.Combine(Path.GetTempPath(), "wechat-duokai-diagnostics-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var report = new DiagnosticReportService().CreateReport(folder, null, null, new InstanceManager());
                var expectedRoot = Path.Combine(Path.GetFullPath(folder), DiagnosticReportService.FolderName) + Path.DirectorySeparatorChar;
                Assert(Path.GetFullPath(report).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase),
                    "Diagnostic report escaped the application directory.");
                var text = File.ReadAllText(report);
                Assert(text.Contains("不会自动上传") && !text.Contains("UserName="),
                    "Diagnostic privacy statement is missing or a user name field was added.");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static void TestReleaseMetadataParsing()
        {
            var newer = ReleaseUpdateChecker.ParseResponse("{\"tag_name\":\"v1.1.0\"}", "1.0.3");
            Assert(newer.CheckSucceeded && newer.IsUpdateAvailable && newer.LatestVersion == "1.1.0",
                "Newer release metadata was not recognized.");
            var same = ReleaseUpdateChecker.ParseResponse("{\"tag_name\":\"v1.0.3\"}", "1.0.3");
            Assert(same.CheckSucceeded && !same.IsUpdateAvailable,
                "Current version must not be presented as an update.");
            Assert(ReleaseUpdateChecker.ReleasesUrl.StartsWith("https://github.com/", StringComparison.Ordinal),
                "Release action must remain an HTTPS GitHub page.");
        }

        private static void TestExplicitLaunchPolicy()
        {
            Assert(InstallFlowPolicy.RequiresExplicitLaunchConfirmation, "Explicit confirmation policy must be enabled.");
            Assert(!InstallFlowPolicy.CanLaunch(false, false), "Launch must be blocked before installation.");
            Assert(!InstallFlowPolicy.CanLaunch(true, false), "Launch must be blocked until confirmation.");
            Assert(InstallFlowPolicy.CanLaunch(true, true), "Launch should be allowed after confirmation.");
        }

        private static void TestEmbeddedPayloads()
        {
            var root = FindProjectRoot();
            AssertEmbeddedEquals("Payload.wechat_duokai.exe",
                Path.Combine(root, "duokai", "bin", "Release", "net48", "wechat_duokai.exe"));
            AssertEmbeddedEquals("Payload.WechatDuokai.Core.dll",
                Path.Combine(root, "duokai", "bin", "Release", "net48", "WechatDuokai.Core.dll"));
            AssertEmbeddedEquals("Payload.wechat_duokai-cleanup.exe",
                Path.Combine(root, "cleanup", "bin", "Release", "net48", "wechat_duokai-cleanup-v1.0.3.exe"));
        }

        private static void TestSeparateInstallerIdentities()
        {
            var root = FindProjectRoot();
            var setup = Path.Combine(root, "installer", "bin", "Release", "net48", "wechat_duokai-setup-v1.0.3.exe");
            var cleanup = Path.Combine(root, "cleanup", "bin", "Release", "net48", "wechat_duokai-cleanup-v1.0.3.exe");
            Assert(File.Exists(setup) && File.Exists(cleanup), "Setup or cleanup output is missing.");
            Assert(!File.ReadAllBytes(setup).SequenceEqual(File.ReadAllBytes(cleanup)),
                "Setup and cleanup must not be byte-identical copies.");
            Assert(FileVersionInfo.GetVersionInfo(setup).ProductName.IndexOf("安装", StringComparison.Ordinal) >= 0,
                "Setup product identity is incorrect.");
            Assert(FileVersionInfo.GetVersionInfo(cleanup).ProductName.IndexOf("清理", StringComparison.Ordinal) >= 0,
                "Cleanup product identity is incorrect.");
        }

        private static Type LoadCleanupWindowType()
        {
            var path = Path.Combine(FindProjectRoot(), "cleanup", "bin", "Release", "net48",
                "wechat_duokai-cleanup-v1.0.3.exe");
            var assembly = Assembly.LoadFrom(path);
            return assembly.GetType("WechatDuokai.Installer.UninstallWindow", true);
        }

        private static void AssertEmbeddedEquals(string resourceName, string builtPath)
        {
            using (var embedded = typeof(InstallerEngine).Assembly.GetManifestResourceStream(resourceName))
            using (var built = File.OpenRead(builtPath))
            using (var sha = SHA256.Create())
            {
                Assert(embedded != null, "Installer resource is missing: " + resourceName);
                var embeddedHash = Convert.ToBase64String(sha.ComputeHash(embedded));
                var builtHash = Convert.ToBase64String(sha.ComputeHash(built));
                Assert(embeddedHash == builtHash, resourceName + " differs from the Release output.");
            }
        }

        private static void TestCustomInstallPathValidation()
        {
            Assert(!InstallerEngine.ValidateInstallTarget(Path.GetPathRoot(Environment.SystemDirectory), out _),
                "A drive root must never be accepted.");
            var testRoot = Path.Combine(Path.GetTempPath(), "wechat-duokai-install-target-" + Guid.NewGuid().ToString("N"));
            var target = Path.Combine(testRoot, "custom-app");
            try
            {
                Directory.CreateDirectory(target);
                Assert(InstallerEngine.ValidateInstallTarget(target, out var error), "Empty custom folder rejected: " + error);
                File.WriteAllText(Path.Combine(target, "unrelated.txt"), "belongs to the user");
                Assert(!InstallerEngine.ValidateInstallTarget(target, out _), "Unmarked non-empty folder must be rejected.");
                File.WriteAllText(Path.Combine(target, InstallerEngine.InstallMarkerName), InstallerEngine.InstallMarkerValue);
                File.WriteAllText(Path.Combine(target, "wechat_duokai.exe"), string.Empty);
                Assert(InstallerEngine.ValidateInstallTarget(target, out _), "Marked installation should be updateable.");
                Assert(InstallerEngine.ValidateInstallDirectory(target), "Marked install directory should validate.");
            }
            finally
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }
        }

        private static void TestRunningInstallDetection()
        {
            var root = Path.Combine(Path.GetTempPath(), "wechat-duokai-running-install-" + Guid.NewGuid().ToString("N"));
            Process child = null;
            try
            {
                Directory.CreateDirectory(root);
                foreach (var file in Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory))
                {
                    var extension = Path.GetExtension(file);
                    if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(extension, ".config", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, Path.Combine(root, Path.GetFileName(file)), true);
                    }
                }

                var target = InstallerEngine.GetInstalledExecutable(root);
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, target, true);
                var sourceConfig = Process.GetCurrentProcess().MainModule.FileName + ".config";
                if (File.Exists(sourceConfig)) File.Copy(sourceConfig, target + ".config", true);
                File.WriteAllText(Path.Combine(root, InstallerEngine.InstallMarkerName),
                    InstallerEngine.InstallMarkerValue);
                child = Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = "--idle",
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Assert(child != null, "Idle helper process did not start.");

                IReadOnlyList<RunningApplicationInfo> running = null;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    running = InstallerEngine.FindRunningApplications(root);
                    if (running.Count > 0) break;
                    Thread.Sleep(50);
                }
                Assert(running != null && running.Count == 1 && running[0].ProcessId == child.Id,
                    "Installer did not identify the exact target process.");

                var unrelatedRoot = Path.Combine(Path.GetTempPath(), "wechat-duokai-unrelated-" + Guid.NewGuid().ToString("N"));
                Assert(InstallerEngine.FindRunningApplications(unrelatedRoot).Count == 0,
                    "Installer must not target a helper running from another directory.");

                var remaining = InstallerEngine.CloseRunningApplications(root, running, true);
                Assert(remaining.Count == 0, "Confirmed force-close did not release the exact target.");
                child.WaitForExit(3000);
            }
            finally
            {
                if (child != null && !child.HasExited) child.Kill();
                child?.Dispose();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static int TestInstall()
        {
            if (Directory.Exists(InstallerEngine.InstallDirectory))
                throw new InvalidOperationException("Install test requires an unused default installation directory.");
            var result = InstallerEngine.Install(false);
            Assert(File.Exists(result.ExecutablePath), "Installed application is missing.");
            Assert(File.Exists(Path.Combine(result.InstallDirectory, "WechatDuokai.Core.dll")), "Installed core DLL is missing.");
            Assert(File.Exists(result.UninstallerPath), "Installed uninstaller is missing.");
            Console.WriteLine("PASS: Installer writes the complete application without launching it");
            return 0;
        }

        private static int CleanupTestInstall()
        {
            var locations = InstallerEngine.GetCleanupLocations();
            Assert(InstallerEngine.ValidateInstallDirectory(locations.InstallDirectory), "No marked test installation found.");
            Assert(string.Equals(Path.GetFullPath(locations.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(InstallerEngine.DefaultInstallDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase), "Cleanup test accepts only the exact default directory.");
            var planPath = Path.Combine(Path.GetTempPath(), "wechat-duokai-cleanup-test-" + Guid.NewGuid().ToString("N") + ".plan");
            var plan = new CleanupPlan
            {
                ParentProcessId = 0,
                DeleteSource = false,
                InstallDirectory = locations.InstallDirectory,
                SourceRoot = null,
                ArtifactRoot = null,
                PackageRoot = null,
                DesktopShortcut = InstallerEngine.DesktopShortcut,
                StartMenuDirectory = InstallerEngine.StartMenuDirectory,
                UserDataDirectory = null,
                WorkerPath = null,
                PlanPath = planPath
            };
            plan.Write(planPath);
            CleanupWorker.ExecuteForTests(planPath);
            Assert(!Directory.Exists(InstallerEngine.DefaultInstallDirectory), "Test installation was not removed.");
            Console.WriteLine("PASS: Cleanup removes the verified test installation");
            return 0;
        }

        private static void TestMutexRelease()
        {
            var readyFile = Path.Combine(Path.GetTempPath(), "wechat-duokai-test-" + Guid.NewGuid().ToString("N") + ".ready");
            var child = StartHelper("--hold-mutex", readyFile);
            try
            {
                WaitForFile(readyFile);
                Assert(File.ReadAllText(readyFile) == "ready", "Test mutex was already present.");
                var unlock = WindowsHandleUnlocker.ReleaseSingleInstanceLocks(AppKind.WeChat, new[] { child.Id });
                Assert(unlock.ClosedHandleCount > 0, "No matching mutex handle was closed.");
                var stillExists = Mutex.TryOpenExisting(MutexName, out var opened);
                opened?.Dispose();
                Assert(!stillExists, "Named mutex still exists after release.");
                Assert(!child.HasExited, "Holder process was terminated.");
            }
            finally
            {
                StopHelper(child);
                if (File.Exists(readyFile)) File.Delete(readyFile);
            }
        }

        private static void TestFileLockRelease()
        {
            var lockFile = Path.Combine(Path.GetTempPath(), "wechat-duokai-lock-" + Guid.NewGuid().ToString("N") + ".ini");
            var readyFile = lockFile + ".ready";
            var child = StartHelper("--hold-file", lockFile);
            try
            {
                WaitForFile(readyFile);
                var unlock = WindowsHandleUnlocker.ReleaseSingleInstanceLocks(AppKind.WeChat, new[] { child.Id }, lockFile);
                Assert(unlock.ClosedHandleCount > 0, "No matching file handle was closed.");
                Assert(unlock.RemovedLockFile && !File.Exists(lockFile), "Lock file was not removed.");
                Assert(!child.HasExited, "Holder process was terminated.");
            }
            finally
            {
                StopHelper(child);
                if (File.Exists(lockFile)) File.Delete(lockFile);
                if (File.Exists(readyFile)) File.Delete(readyFile);
            }
        }

        private static Process StartHelper(string option, string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = option + " \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static void WaitForFile(string path)
        {
            for (var attempt = 0; attempt < 100 && !File.Exists(path); attempt++) Thread.Sleep(50);
            Assert(File.Exists(path), "Helper did not become ready.");
        }

        private static void StopHelper(Process child)
        {
            if (child != null && !child.HasExited)
            {
                child.Kill();
                child.WaitForExit(3000);
            }
            child?.Dispose();
        }

        private static string FindProjectRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var depth = 0; directory != null && depth < 9; depth++, directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, InstallerEngine.SourceMarkerName))) return directory.FullName;
            }
            throw new DirectoryNotFoundException("Project root not found.");
        }

        private static void Run(string name, Action test)
        {
            test();
            Console.WriteLine("PASS: " + name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FakeProcessEnvironment : IProcessEnvironment
        {
            private readonly string _path;

            internal FakeProcessEnvironment(string path) { _path = path; }

            public int CurrentSessionId => 7;

            public System.Collections.Generic.IReadOnlyList<ProcessSnapshot> FindProcesses(
                System.Collections.Generic.IEnumerable<string> processNames)
            {
                return new[]
                {
                    new ProcessSnapshot { Id = 10, SessionId = 7, ExecutablePath = _path },
                    new ProcessSnapshot { Id = 11, SessionId = 7, ExecutablePath = _path },
                    new ProcessSnapshot { Id = 20, SessionId = 7, ExecutablePath = _path },
                    new ProcessSnapshot { Id = 99, SessionId = 8, ExecutablePath = _path }
                };
            }

            public System.Collections.Generic.IReadOnlyDictionary<int, int> GetParentProcessMap()
            {
                return new System.Collections.Generic.Dictionary<int, int> { [11] = 10 };
            }

            public void StartApplication(string executablePath) { }

            public System.Threading.Tasks.Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
    }
}
