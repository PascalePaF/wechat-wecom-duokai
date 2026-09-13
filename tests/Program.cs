using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
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
            if (args.Length == 4 && args[0] == "--snapshot") return SaveUiSnapshot(args[1], args[2], args[3]);
            if (args.Length == 1 && args[0] == "--test-install") return TestInstall();
            if (args.Length == 1 && args[0] == "--cleanup-test-install") return CleanupTestInstall();

            try
            {
                BootstrapWpf(typeof(MainWindow), "Light");
                Run("Null application has zero instances", () =>
                    Assert(new InstanceManager().GetInstanceCount(null) == 0, "Expected zero."));
                Run("Target count cache survives a restart", TestPreferenceRoundTrip);
                Run("Cleanup refuses drive roots", () =>
                    Assert(!InstallerEngine.ValidateSourceRoot(Path.GetPathRoot(Environment.SystemDirectory)),
                        "A drive root must never be accepted as a source directory."));
                Run("Project source marker is recognized", () =>
                    Assert(InstallerEngine.ValidateSourceRoot(FindProjectRoot()), "Expected marked source root."));
                Run("Custom installation paths are validated safely", TestCustomInstallPathValidation);
                Run("All release windows use native movable title bars", TestNativeWindowChrome);
                Run("Install cannot launch before explicit confirmation", TestExplicitLaunchPolicy);
                Run("Installer embeds the exact application and core payload", TestEmbeddedPayloads);
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

        private static int SaveUiSnapshot(string windowName, string outputPath, string themeName)
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
                case "uninstaller": windowType = typeof(UninstallWindow); break;
                default: throw new ArgumentException("Unknown window: " + windowName);
            }

            BootstrapWpf(windowType, themeName);
            var window = (Window)Activator.CreateInstance(windowType);
            try
            {
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
                var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
                var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
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
                Assert(UserPreferences.HasValidMarker(testDirectory), "User-data marker is invalid.");
            }
            finally
            {
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void TestNativeWindowChrome()
        {
            var windows = new Window[] { new MainWindow(), new InstallWindow(), new UninstallWindow() };
            try
            {
                foreach (var window in windows)
                {
                    Assert(window.WindowStyle != WindowStyle.None, window.GetType().Name + " must use a native title bar.");
                    Assert(window.ResizeMode == ResizeMode.CanMinimize, window.GetType().Name + " must be movable and minimizable.");
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
    }
}
