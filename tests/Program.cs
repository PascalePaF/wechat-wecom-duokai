using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
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
            if (args.Length == 5 && args[0] == "--abandon-wecom-registry")
                return AbandonWeComRegistrySession(args[1], args[2], args[3], int.Parse(args[4]));
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
                Run("Shared target count migrates V1.0.6 preferences and survives a restart", TestPreferenceRoundTrip);
                Run("Footer counts and settings navigation stay live and visible", TestFooterCountsAndSettings);
                Run("Renamed executables cannot impersonate an official client", TestClientExecutableValidation);
                Run("Process environment groups roots and supports deterministic launch tests", TestProcessEnvironmentAbstraction);
                Run("WeCom extended launch policy is temporary and reaches three instances", TestWeComExtendedLaunchPolicy);
                Run("WeCom registry journal recovers crashes and preserves external changes", TestWeComCrashRecovery);
                Run("Diagnostics stay inside the selected local application directory", TestLocalDiagnostics);
                Run("All persistent runtime data stays below the application directory", TestApplicationStorageLayout);
                Run("Legacy AppData settings migrate into the application directory", TestLegacyStorageMigration);
                Run("GitHub release metadata exposes only verified one-click update assets", TestReleaseMetadataParsing);
                Run("Update mode detection accepts only marked install and portable roots", TestUpdateModeDetection);
                Run("Transactional update preserves data and rolls back interrupted replacement", TestUpdateTransaction);
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

        private static int AbandonWeComRegistrySession(string registryPath, string gateName,
            string dataDirectory, int targetCount)
        {
            var policy = new WindowsWeComLaunchPolicy(registryPath, gateName, dataDirectory);
            var scope = policy.BeginLaunchSession(targetCount);
            GC.KeepAlive(scope);
            // Deliberately do not dispose: process exit models a crash or sudden power loss.
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
            var showMainSettings = false;
            bool? previousAutoCheck = null;
            switch (windowName.ToLowerInvariant())
            {
                case "main": windowType = typeof(MainWindow); break;
                case "main-settings":
                    windowType = typeof(MainWindow);
                    showMainSettings = true;
                    break;
                case "installer": windowType = typeof(InstallWindow); break;
                case "installer-complete":
                    windowType = typeof(InstallWindow);
                    showInstallerCompletion = true;
                    break;
                case "uninstaller": windowType = LoadCleanupWindowType(); break;
                default: throw new ArgumentException("Unknown window: " + windowName);
            }

            BootstrapWpf(windowType, themeName);
            if (windowType == typeof(MainWindow))
            {
                previousAutoCheck = UserPreferences.LoadAutoCheckForUpdates();
                UserPreferences.SaveAutoCheckForUpdates(false);
            }
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
                if (showMainSettings)
                {
                    ((MainWindow)window).SetSettingsViewVisible(true);
                }
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Thread.Sleep(250);
                window.UpdateLayout();
                var visual = window.Content as FrameworkElement ?? window;
                var renderedWidth = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
                var renderedHeight = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
                var bitmap = new RenderTargetBitmap(renderedWidth, renderedHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
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
                if (previousAutoCheck.HasValue)
                {
                    UserPreferences.SaveAutoCheckForUpdates(previousAutoCheck.Value);
                }
                Application.Current.Shutdown();
            }
            return 0;
        }

        private static void TestPreferenceRoundTrip()
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "wechat-duokai-preferences-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert(UserPreferences.LoadTargetCount(testDirectory, AppKind.WeChat) == 2,
                    "Missing WeChat target should default to 2.");
                Assert(UserPreferences.LoadTargetCount(testDirectory, AppKind.WeCom) == 2,
                    "Missing WeCom target should default to 2.");
                Assert(UserPreferences.LoadAutoCheckForUpdates(testDirectory),
                    "Missing update preference should preserve the safe historical default.");

                Directory.CreateDirectory(testDirectory);
                File.WriteAllText(Path.Combine(testDirectory, UserPreferences.DataMarkerName),
                    UserPreferences.DataMarkerValue, Encoding.UTF8);
                File.WriteAllText(Path.Combine(testDirectory, "settings.ini"),
                    "WeChatTargetInstanceCount=7\r\nWeComTargetInstanceCount=4\r\n", Encoding.UTF8);
                Assert(UserPreferences.LoadTargetCount(testDirectory, AppKind.WeChat) == 7,
                    "The V1.0.6 WeChat target should take migration priority.");
                Assert(UserPreferences.LoadTargetCount(testDirectory, AppKind.WeCom) == 7,
                    "Both clients must use the same migrated target count.");

                UserPreferences.SaveTargetCount(testDirectory, 5);
                Assert(UserPreferences.LoadTargetCount(testDirectory, AppKind.WeChat) == 5 &&
                       UserPreferences.LoadTargetCount(testDirectory, AppKind.WeCom) == 5,
                    "The shared target count was not preserved for both clients.");
                UserPreferences.SaveAutoCheckForUpdates(testDirectory, false);
                Assert(!UserPreferences.LoadAutoCheckForUpdates(testDirectory),
                    "Automatic version-check preference was not preserved.");
                var custom = Path.Combine(testDirectory, "Weixin.exe");
                UserPreferences.SaveCustomClientPath(testDirectory, AppKind.WeChat, custom);
                Assert(UserPreferences.LoadCustomClientPath(testDirectory, AppKind.WeChat) == Path.GetFullPath(custom),
                    "Custom client path was not preserved.");
                Assert(UserPreferences.HasValidMarker(testDirectory), "User-data marker is invalid.");

                var settings = File.ReadAllText(Path.Combine(testDirectory, "settings.ini"));
                Assert(settings.Contains("TargetInstanceCount=5") &&
                       !settings.Contains("WeChatTargetInstanceCount") &&
                       !settings.Contains("WeComTargetInstanceCount"),
                    "V1.0.6's separate target keys were not canonicalized to one shared value.");
            }
            finally
            {
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void TestFooterCountsAndSettings()
        {
            var folder = Path.Combine(Path.GetTempPath(), "wechat-duokai-footer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var weChatPath = Path.Combine(folder, "Weixin.exe");
            var weComPath = Path.Combine(folder, "WXWork.exe");
            File.WriteAllText(weChatPath, string.Empty);
            File.WriteAllText(weComPath, string.Empty);
            var environment = new MultiClientFakeProcessEnvironment(weChatPath, 2, weComPath, 3);
            var window = new MainWindow(new InstanceManager(environment), new ReleaseUpdateChecker());
            try
            {
                SetPrivateField(window, "_weChat", new AppDefinition(AppKind.WeChat, "微信", weChatPath));
                SetPrivateField(window, "_weCom", new AppDefinition(AppKind.WeCom, "企业微信", weComPath));
                InvokePrivate(window, "RefreshClientStatus");

                var weChatCount = (TextBlock)window.FindName("FooterWeChatCount");
                var weComCount = (TextBlock)window.FindName("FooterWeComCount");
                var footerCountsPanel = (FrameworkElement)window.FindName("FooterCountsPanel");
                Assert(weChatCount.Text == "当前微信窗口 2 个", "WeChat footer count is not live.");
                Assert(weComCount.Text == "当前企业微信窗口 3 个", "WeCom footer count is not live.");
                Assert(weChatCount.TextAlignment == TextAlignment.Left &&
                       weComCount.TextAlignment == TextAlignment.Left,
                    "Footer counts must remain left-aligned on two rows.");
                Assert(footerCountsPanel != null && footerCountsPanel.MinWidth >= 112,
                    "Footer count labels must reserve enough width in every view.");

                var settingsButton = (Button)window.FindName("SettingsButton");
                Assert(Grid.GetColumn(settingsButton) == 5,
                    "Settings must remain the right-most footer action.");
                window.SetSettingsViewVisible(true);
                Assert(((FrameworkElement)window.FindName("SettingsWorkspace")).Visibility == Visibility.Visible &&
                       ((FrameworkElement)window.FindName("WorkspaceGrid")).Visibility == Visibility.Collapsed,
                    "Settings did not replace the main workspace.");
                Assert(window.FindName("AutoUpdateCheckBox") != null &&
                       window.FindName("CheckUpdateButton") != null &&
                       window.FindName("UpdateNowButton") != null &&
                       window.FindName("UpdateProgressBar") != null &&
                       window.FindName("SystemThemeRadio") != null &&
                       window.FindName("LightThemeRadio") != null &&
                       window.FindName("DarkThemeRadio") != null,
                    "Settings must expose one-click update and three-way theme preferences.");
                Assert(((FrameworkElement)window.FindName("UpdateNowButton")).Visibility == Visibility.Collapsed,
                    "One-click update must stay hidden until trusted update metadata is available.");
                var target = (TextBox)window.FindName("TargetCountBox");
                Assert(target != null &&
                       window.FindName("WeChatTargetCountBox") == null &&
                       window.FindName("WeComTargetCountBox") == null &&
                       window.FindName("DecreaseButton") != null &&
                       window.FindName("IncreaseButton") != null,
                    "The two clients must share one target-count control.");
            }
            finally
            {
                window.Close();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, "Missing test field: " + name);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "Missing test method: " + name);
            method.Invoke(target, null);
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
                Assert(Math.Abs(window.MinWidth - 901d) < 0.01 && Math.Abs(window.MinHeight - 513d) < 0.01,
                    "The reference 901x513 interface must be the exact minimum window size.");
                Assert(double.IsPositiveInfinity(window.MaxWidth) && double.IsPositiveInfinity(window.MaxHeight),
                    "Main window must not impose a fixed maximum size.");
                var panel = (FrameworkElement)window.FindName("CountPanel");
                var viewport = (FrameworkElement)window.FindName("ScaleViewport");
                var scaledRoot = (FrameworkElement)window.FindName("ScaledRoot");
                var workspace = (FrameworkElement)window.FindName("WorkspaceGrid");
                var weChatCard = (FrameworkElement)window.FindName("WeChatClientCard");
                var weComCard = (FrameworkElement)window.FindName("WeComClientCard");
                var transform = (ScaleTransform)window.FindName("InterfaceScaleTransform");
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.ShowInTaskbar = false;
                window.Show();
                foreach (var size in new[]
                {
                    new System.Windows.Size(901, 513), new System.Windows.Size(1280, 720),
                    new System.Windows.Size(1920, 1080)
                })
                {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    window.UpdateLayout();
                    Assert(panel.ActualWidth > 0 && panel.ActualHeight > 0,
                        "Count panel disappeared at " + size.Width + "x" + size.Height + ".");
                    Assert(weChatCard.ActualHeight >= 80d && weComCard.ActualHeight >= 80d,
                        "An application card was clipped at " + size.Width + "x" + size.Height + ".");
                    Assert(Math.Abs(transform.ScaleX - transform.ScaleY) < 0.001 && transform.ScaleX >= 1d,
                        "The interface must use one non-shrinking uniform scale.");
                    Assert(Math.Abs(scaledRoot.ActualWidth * transform.ScaleX - viewport.ActualWidth) < 2d &&
                           Math.Abs(scaledRoot.ActualHeight * transform.ScaleY - viewport.ActualHeight) < 2d,
                        "The scaled interface did not fill the available window.");
                }

                Assert(Grid.GetRow(panel) == 0 && Grid.GetColumn(panel) == 2 && Grid.GetColumnSpan(panel) == 1,
                    "The count panel must stay on the right at every supported size.");
                Assert(ReferenceEquals(VisualTreeHelper.GetParent(workspace), scaledRoot),
                    "The workspace must be placed directly in the scaled root without an outer ScrollViewer.");
                var ultraWideScale = MainWindow.CalculateInterfaceScale(3440d, 1392d);
                Assert(ultraWideScale > 2.7d && ultraWideScale < 2.72d,
                    "The 3440x1392 layout must proportionally enlarge all controls.");
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

        private static void TestWeComExtendedLaunchPolicy()
        {
            var registryPath = @"SOFTWARE\WechatDuokai\Tests\" + Guid.NewGuid().ToString("N");
            var gateName = @"Local\WechatDuokai.Tests." + Guid.NewGuid().ToString("N");
            var recoveryDirectory = Path.Combine(Path.GetTempPath(),
                "wechat-duokai-registry-policy-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    key.SetValue("multi_instances", "keep-original-kind", RegistryValueKind.String);
                }

                var policy = new WindowsWeComLaunchPolicy(registryPath, gateName, recoveryDirectory);
                using (policy.BeginLaunchSession(3))
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(key != null && !key.GetValueNames().Contains("multi_instances"),
                        "The two-instance registry hint must be absent only during an extended launch.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert((string)key.GetValue("multi_instances") == "keep-original-kind" &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.String,
                        "The original WeCom registry value and kind were not restored.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    key.DeleteValue("multi_instances", false);
                }

                using (policy.BeginLaunchSession(2))
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 2 &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.DWord,
                        "The official two-instance registry hint was not applied temporarily.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(key != null && !key.GetValueNames().Contains("multi_instances"),
                        "A registry value that did not exist before launch must not remain afterwards.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    key.SetValue("multi_instances", "owner-before-two", RegistryValueKind.String);
                }
                var twoWindowScope = policy.BeginLaunchSession(2);
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 2,
                        "The two-instance temporary state was not applied.");
                    key.SetValue("multi_instances", 7, RegistryValueKind.DWord);
                }
                twoWindowScope.Dispose();
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 7 &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.DWord,
                        "Restore overwrote a newer third-party value during two-instance mode.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    key.SetValue("multi_instances", "owner-before-extended", RegistryValueKind.String);
                }
                var extendedScope = policy.BeginLaunchSession(3);
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    Assert(!key.GetValueNames().Contains("multi_instances"),
                        "Extended mode must temporarily remove the registry hint.");
                    key.SetValue("multi_instances", "external-owner", RegistryValueKind.String);
                }
                extendedScope.Dispose();
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert((string)key.GetValue("multi_instances") == "external-owner",
                        "Restore overwrote a newer third-party value during extended mode.");
                }

                var environment = new LaunchingFakeProcessEnvironment(Process.GetCurrentProcess().MainModule.FileName, 1);
                var trackingPolicy = new TrackingWeComLaunchPolicy();
                var manager = new InstanceManager(environment, trackingPolicy);
                var application = new AppDefinition(AppKind.WeCom, "企业微信", environment.ExecutablePath);
                var result = manager.EnsureTargetCountAsync(application, 3, null, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert(result.Success && result.BeforeCount == 1 && result.AfterCount == 3 && result.StartedCount == 2,
                    "The deterministic WeCom launch loop did not reach three root instances.");
                Assert(trackingPolicy.RequestedTarget == 3 && trackingPolicy.DisposeCount == 1,
                    "The temporary WeCom launch session was not entered and disposed exactly once.");
                Assert(environment.Delays.Any(delay => delay == TimeSpan.FromMilliseconds(100)) &&
                       environment.Delays.Any(delay => delay == TimeSpan.FromMilliseconds(800)),
                    "The verified WeCom unlock and settle timings are missing.");
                Assert(!File.Exists(policy.JournalPath),
                    "A normally disposed registry session left a stale journal.");
                Assert(File.Exists(policy.AuditLogPath),
                    "Registry recovery audit evidence was not written locally.");
            }
            finally
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
                }
                catch (Exception)
                {
                }
                if (Directory.Exists(recoveryDirectory)) Directory.Delete(recoveryDirectory, true);
            }
        }

        private static void TestWeComCrashRecovery()
        {
            var registryPath = @"SOFTWARE\WechatDuokai\CrashTests\" + Guid.NewGuid().ToString("N");
            var gateName = @"Local\WechatDuokai.CrashTests." + Guid.NewGuid().ToString("N");
            var dataDirectory = Path.Combine(Path.GetTempPath(),
                "wechat-duokai-crash-recovery-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    key.SetValue("multi_instances", "before-power-loss", RegistryValueKind.String);
                }

                RunAbandonedRegistrySession(registryPath, gateName, dataDirectory, 2);
                var policy = new WindowsWeComLaunchPolicy(registryPath, gateName, dataDirectory);
                Assert(File.Exists(policy.JournalPath),
                    "Abrupt process exit did not leave a durable recovery journal.");
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 2,
                        "Crash fixture did not leave the expected temporary registry state.");
                }

                var restored = policy.RecoverPendingSession();
                Assert(restored.Outcome == WeComRegistryRecoveryOutcome.Restored,
                    "Startup recovery did not restore the interrupted registry transaction.");
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert((string)key.GetValue("multi_instances") == "before-power-loss" &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.String,
                        "Crash recovery did not preserve the original registry value and kind.");
                }
                Assert(!File.Exists(policy.JournalPath),
                    "Successful crash recovery did not clear its transaction journal.");

                using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    key.SetValue("multi_instances", "before-external-change", RegistryValueKind.String);
                }
                RunAbandonedRegistrySession(registryPath, gateName, dataDirectory, 2);
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    key.SetValue("multi_instances", 9, RegistryValueKind.DWord);
                }
                var conflict = policy.RecoverPendingSession();
                Assert(conflict.Outcome == WeComRegistryRecoveryOutcome.ExternalStatePreserved,
                    "Recovery did not report a newer external registry value.");
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 9 &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.DWord,
                        "Crash recovery overwrote a third-party registry change.");
                }
                Assert(!File.Exists(policy.JournalPath),
                    "Resolved external conflict left a stale recovery journal.");

                using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    key.SetValue("multi_instances", new[] { "one", "二" }, RegistryValueKind.MultiString);
                }
                RunAbandonedRegistrySession(registryPath, gateName, dataDirectory, 3);
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(!key.GetValueNames().Contains("multi_instances"),
                        "Extended crash fixture did not remove the temporary registry hint.");
                }
                var restoredMultiString = policy.RecoverPendingSession();
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    var value = (string[])key.GetValue("multi_instances");
                    Assert(restoredMultiString.Outcome == WeComRegistryRecoveryOutcome.Restored &&
                           value.SequenceEqual(new[] { "one", "二" }) &&
                           key.GetValueKind("multi_instances") == RegistryValueKind.MultiString,
                        "Crash recovery did not round-trip a multi-string registry value.");
                }

                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    key.SetValue("multi_instances", 11, RegistryValueKind.DWord);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(policy.JournalPath));
                File.WriteAllText(policy.JournalPath, "Format=untrusted\r\nRegistryPath=invalid");
                var invalid = policy.RecoverPendingSession();
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(invalid.Outcome == WeComRegistryRecoveryOutcome.InvalidJournal &&
                           Convert.ToInt32(key.GetValue("multi_instances")) == 11,
                        "An invalid recovery journal was allowed to modify the registry.");
                }
                Assert(Directory.EnumerateFiles(Path.GetDirectoryName(policy.JournalPath),
                           Path.GetFileName(policy.JournalPath) + ".invalid-*").Any(),
                    "Invalid recovery journal was not quarantined locally.");
                using (policy.BeginLaunchSession(2)) { }
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    Assert(Convert.ToInt32(key.GetValue("multi_instances")) == 11 &&
                           !File.Exists(policy.JournalPath),
                        "A quarantined recovery journal did not block a new temporary registry session.");
                }

                var audit = File.ReadAllText(policy.AuditLogPath, Encoding.UTF8);
                Assert(audit.Contains("REGISTRY_RESTORED") &&
                       audit.Contains("EXTERNAL_STATE_PRESERVED") &&
                       audit.Contains("RECOVERY_INVALID") &&
                       !audit.Contains("before-power-loss"),
                    "Recovery audit is incomplete or leaked the original registry value.");
            }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
                catch (Exception) { }
                if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, true);
            }
        }

        private static void RunAbandonedRegistrySession(string registryPath, string gateName,
            string dataDirectory, int targetCount)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--abandon-wecom-registry " + QuoteArgument(registryPath) + " " +
                            QuoteArgument(gateName) + " " + QuoteArgument(dataDirectory) + " " + targetCount,
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                Assert(process != null && process.WaitForExit(10000) && process.ExitCode == 0,
                    "Crash recovery helper did not complete.");
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", string.Empty) + "\"";
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
                var expectedRoot = Path.Combine(Path.GetFullPath(folder), ApplicationStorage.DataFolderName,
                    DiagnosticReportService.FolderName) + Path.DirectorySeparatorChar;
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

        private static void TestApplicationStorageLayout()
        {
            ApplicationStorage.EnsureReady();
            var applicationRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var dataRoot = Path.GetFullPath(ApplicationStorage.DataDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert(dataRoot.StartsWith(applicationRoot, StringComparison.OrdinalIgnoreCase),
                "Application data escaped the executable directory.");
            Assert(UserPreferences.DataDirectory == ApplicationStorage.DataDirectory &&
                   UserPreferences.HasValidMarker(),
                "Preferences are not using the marked in-directory data folder.");

            var policy = new WindowsWeComLaunchPolicy();
            Assert(Path.GetFullPath(policy.JournalPath).StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFullPath(policy.AuditLogPath).StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase),
                "Registry recovery evidence escaped the in-directory data folder.");

            var themeManager = typeof(MainWindow).Assembly.GetType(
                "WechatDuokai.Presentation.ThemeManager", true);
            var themeDataProperty = themeManager.GetProperty("DataDirectory",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert(themeDataProperty != null &&
                   string.Equals(Path.GetFullPath((string)themeDataProperty.GetValue(null)),
                       Path.GetFullPath(ApplicationStorage.DataDirectory), StringComparison.OrdinalIgnoreCase),
                "Theme preference is not stored with the application data.");

            var installSample = Path.Combine(Path.GetTempPath(), "sample-wechat-duokai-install");
            Assert(string.Equals(InstallerEngine.GetInstalledDataDirectory(installSample),
                    Path.Combine(installSample, "data"), StringComparison.OrdinalIgnoreCase),
                "Installer data path is not below the selected installation directory.");
        }

        private static void TestLegacyStorageMigration()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "wechat-duokai-storage-migration-" + Guid.NewGuid().ToString("N"));
            var legacy = Path.Combine(root, "legacy");
            var destination = Path.Combine(root, "application", "data");
            try
            {
                Directory.CreateDirectory(legacy);
                File.WriteAllText(Path.Combine(legacy, ApplicationStorage.DataMarkerName),
                    ApplicationStorage.DataMarkerValue, Encoding.UTF8);
                File.WriteAllText(Path.Combine(legacy, "settings.ini"),
                    "TargetInstanceCount=6", Encoding.UTF8);
                File.WriteAllText(Path.Combine(legacy, "theme.ini"), "Dark", Encoding.UTF8);

                ApplicationStorage.MigrateLegacyFiles(legacy, destination);
                Assert(File.Exists(Path.Combine(destination, "settings.ini")) &&
                       File.Exists(Path.Combine(destination, "theme.ini")) &&
                       ApplicationStorage.HasValidMarker(destination),
                    "Known legacy preferences were not migrated to the new data directory.");
                Assert(!Directory.Exists(legacy),
                    "Empty marked legacy directory was not removed after successful migration.");
                Assert(UserPreferences.LoadTargetCount(destination, AppKind.WeChat) == 6 &&
                       UserPreferences.LoadTargetCount(destination, AppKind.WeCom) == 6,
                    "Legacy single target was not preserved as the shared initial value.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void TestReleaseMetadataParsing()
        {
            var setupHash = new string('a', 64);
            var sumsHash = new string('b', 64);
            var newer = ReleaseUpdateChecker.ParseResponse(
                BuildReleaseJson("1.0.8", setupHash, sumsHash, null), "1.0.7");
            Assert(newer.CheckSucceeded && newer.IsUpdateAvailable && newer.LatestVersion == "1.0.8" &&
                   newer.CanInstallUpdate && newer.SetupAsset.Sha256 == setupHash &&
                   newer.ChecksumAsset.Sha256 == sumsHash,
                "Newer release and its update assets were not recognized.");

            var malicious = ReleaseUpdateChecker.ParseResponse(
                BuildReleaseJson("1.0.8", setupHash, sumsHash,
                    "https://example.com/PascalePaF/wechat-wecom-duokai/releases/download/v1.0.8/" +
                    "wechat_duokai-setup-v1.0.8.exe"), "1.0.7");
            Assert(malicious.CheckSucceeded && malicious.IsUpdateAvailable && !malicious.CanInstallUpdate &&
                   !string.IsNullOrWhiteSpace(malicious.OneClickUpdateError),
                "A non-GitHub update asset was accepted for execution.");

            var nestedPath = ReleaseUpdateChecker.ParseResponse(
                BuildReleaseJson("1.0.8", setupHash, sumsHash,
                    "https://github.com/PascalePaF/wechat-wecom-duokai/releases/download/v1.0.8/extra/" +
                    "wechat_duokai-setup-v1.0.8.exe"), "1.0.7");
            Assert(!nestedPath.CanInstallUpdate,
                "A non-canonical GitHub Release asset path was accepted for execution.");

            var duplicateAsset = ReleaseUpdateChecker.ParseResponse(
                BuildReleaseJson("1.0.8", setupHash, sumsHash, null, true), "1.0.7");
            Assert(!duplicateAsset.CanInstallUpdate &&
                   duplicateAsset.OneClickUpdateError.IndexOf("重名", StringComparison.Ordinal) >= 0,
                "Ambiguous duplicate GitHub Release assets were accepted.");

            var same = ReleaseUpdateChecker.ParseResponse(
                BuildReleaseJson("1.0.7", setupHash, sumsHash, null), "1.0.7");
            Assert(same.CheckSucceeded && !same.IsUpdateAvailable,
                "Current version must not be presented as an update.");
            var parsedHash = ApplicationUpdateService.ParseChecksum(
                setupHash + "  installer/wechat_duokai-setup-v1.0.8.exe\r\n" +
                sumsHash + "  portable/other.zip", "wechat_duokai-setup-v1.0.8.exe");
            Assert(parsedHash == setupHash, "The setup checksum was not selected exactly.");
            var duplicateRejected = false;
            try
            {
                ApplicationUpdateService.ParseChecksum(
                    setupHash + "  installer/wechat_duokai-setup-v1.0.8.exe\n" +
                    sumsHash + "  wechat_duokai-setup-v1.0.8.exe",
                    "wechat_duokai-setup-v1.0.8.exe");
            }
            catch (InvalidDataException)
            {
                duplicateRejected = true;
            }
            Assert(duplicateRejected, "Ambiguous duplicate setup hashes were accepted.");
            Assert(ReleaseUpdateChecker.ReleasesUrl.StartsWith("https://github.com/", StringComparison.Ordinal),
                "Release action must remain an HTTPS GitHub page.");
        }

        private static string BuildReleaseJson(string version, string setupHash,
            string sumsHash, string setupUrlOverride, bool duplicateSetup = false)
        {
            var setupName = "wechat_duokai-setup-v" + version + ".exe";
            var setupUrl = setupUrlOverride ??
                           "https://github.com/PascalePaF/wechat-wecom-duokai/releases/download/v" +
                           version + "/" + setupName;
            var setupAsset = "{\"name\":\"" + setupName + "\",\"state\":\"uploaded\"," +
                             "\"browser_download_url\":\"" + setupUrl + "\",\"size\":481792," +
                             "\"digest\":\"sha256:" + setupHash + "\"}";
            return "{\"tag_name\":\"v" + version + "\",\"html_url\":" +
                   "\"https://github.com/PascalePaF/wechat-wecom-duokai/releases/tag/v" + version + "\"," +
                   "\"draft\":false,\"prerelease\":false,\"assets\":[" +
                   setupAsset + "," + (duplicateSetup ? setupAsset + "," : string.Empty) +
                   "{\"name\":\"SHA256SUMS.txt\",\"state\":\"uploaded\"," +
                   "\"browser_download_url\":" +
                   "\"https://github.com/PascalePaF/wechat-wecom-duokai/releases/download/v" +
                   version + "/SHA256SUMS.txt\",\"size\":580," +
                   "\"digest\":\"sha256:" + sumsHash + "\"}]}";
        }

        private static void TestUpdateModeDetection()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "wechat-duokai-update-mode-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "wechat_duokai.exe"), "placeholder");
                Assert(ApplicationUpdateService.DetectMode(root) == ApplicationInstallMode.Unknown,
                    "An unmarked directory was accepted for automatic replacement.");

                File.WriteAllText(Path.Combine(root, ApplicationUpdateService.InstallMarkerName),
                    ApplicationUpdateService.InstallMarkerValue, Encoding.UTF8);
                Assert(ApplicationUpdateService.DetectMode(root) == ApplicationInstallMode.Installed,
                    "A marked installation was not recognized.");

                File.Delete(Path.Combine(root, ApplicationUpdateService.InstallMarkerName));
                File.WriteAllText(Path.Combine(root, ApplicationUpdateService.PortableMarkerName),
                    ApplicationUpdateService.PortableMarkerValue, Encoding.UTF8);
                Assert(ApplicationUpdateService.DetectMode(root) == ApplicationInstallMode.Portable,
                    "A marked portable directory was not recognized.");

                File.WriteAllText(Path.Combine(root, ApplicationUpdateService.InstallMarkerName),
                    ApplicationUpdateService.InstallMarkerValue, Encoding.UTF8);
                Assert(ApplicationUpdateService.DetectMode(root) == ApplicationInstallMode.Unknown,
                    "An ambiguous directory with two identities was accepted.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void TestUpdateTransaction()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "wechat-duokai-update-transaction-" + Guid.NewGuid().ToString("N"));
            var portableRoot = root + "-portable";
            var data = Path.Combine(root, "data");
            var originalFiles = new[]
            {
                "wechat_duokai.exe",
                "wechat_duokai.exe.config",
                "WechatDuokai.Core.dll",
                "LICENSE.txt",
                "wechat_duokai-uninstall.exe"
            };
            try
            {
                Directory.CreateDirectory(data);
                File.WriteAllText(Path.Combine(data, ApplicationStorage.DataMarkerName),
                    ApplicationStorage.DataMarkerValue, Encoding.UTF8);
                File.WriteAllText(Path.Combine(data, "settings.ini"), "sentinel-settings", Encoding.UTF8);
                foreach (var name in originalFiles)
                {
                    File.WriteAllText(Path.Combine(root, name), "old-" + name, Encoding.UTF8);
                }

                var interrupted = false;
                try
                {
                    InstallerEngine.ApplyPayloadTransactionForTests(root, false, 2);
                }
                catch (IOException)
                {
                    interrupted = true;
                }
                Assert(interrupted, "The simulated interrupted update did not fail.");
                foreach (var name in originalFiles)
                {
                    Assert(File.ReadAllText(Path.Combine(root, name), Encoding.UTF8) == "old-" + name,
                        "Rollback did not restore " + name + ".");
                }
                Assert(File.ReadAllText(Path.Combine(data, "settings.ini"), Encoding.UTF8) ==
                       "sentinel-settings", "Rollback changed persistent user data.");

                InstallerEngine.ApplyPayloadTransactionForTests(root, false, 0);
                Assert(FileVersionInfo.GetVersionInfo(Path.Combine(root, "wechat_duokai.exe"))
                           .FileVersion.StartsWith("1.0.7", StringComparison.Ordinal),
                    "Successful transaction did not install the V1.0.7 application payload.");
                Assert(File.ReadAllText(Path.Combine(data, "settings.ini"), Encoding.UTF8) ==
                       "sentinel-settings", "Successful update changed persistent user data.");

                var portableData = Path.Combine(portableRoot, "data");
                Directory.CreateDirectory(portableData);
                File.WriteAllText(Path.Combine(portableData, ApplicationStorage.DataMarkerName),
                    ApplicationStorage.DataMarkerValue, Encoding.UTF8);
                File.WriteAllText(Path.Combine(portableData, "settings.ini"),
                    "portable-sentinel-settings", Encoding.UTF8);
                File.WriteAllText(Path.Combine(portableRoot, "wechat_duokai.exe"),
                    "old-portable-application", Encoding.UTF8);
                File.WriteAllText(Path.Combine(portableRoot, "README.md"),
                    "old-portable-readme", Encoding.UTF8);

                InstallerEngine.ApplyPayloadTransactionForTests(portableRoot, true, 0);
                Assert(FileVersionInfo.GetVersionInfo(Path.Combine(portableRoot, "wechat_duokai.exe"))
                           .FileVersion.StartsWith("1.0.7", StringComparison.Ordinal),
                    "Portable transaction did not install the V1.0.7 application payload.");
                Assert(File.Exists(Path.Combine(portableRoot, "wechat_duokai-cleanup-v1.0.7.exe")) &&
                       File.Exists(Path.Combine(portableRoot, "README.md")) &&
                       File.Exists(Path.Combine(portableRoot, "版本说明.md")) &&
                       File.Exists(Path.Combine(portableRoot, "一键更新安全验证报告.md")),
                    "Portable transaction did not install its cleanup tool and release documents.");
                Assert(File.ReadAllText(Path.Combine(portableData, "settings.ini"), Encoding.UTF8) ==
                       "portable-sentinel-settings", "Portable update changed persistent user data.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (Directory.Exists(portableRoot)) Directory.Delete(portableRoot, true);
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
            AssertEmbeddedEquals("Payload.wechat_duokai-cleanup.exe",
                Path.Combine(root, "cleanup", "bin", "Release", "net48", "wechat_duokai-cleanup-v1.0.7.exe"));
        }

        private static void TestSeparateInstallerIdentities()
        {
            var root = FindProjectRoot();
            var setup = Path.Combine(root, "installer", "bin", "Release", "net48", "wechat_duokai-setup-v1.0.7.exe");
            var cleanup = Path.Combine(root, "cleanup", "bin", "Release", "net48", "wechat_duokai-cleanup-v1.0.7.exe");
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
                "wechat_duokai-cleanup-v1.0.7.exe");
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
            var dataDirectory = InstallerEngine.GetInstalledDataDirectory(result.InstallDirectory);
            Assert(Directory.Exists(dataDirectory) &&
                   File.Exists(Path.Combine(dataDirectory, InstallerEngine.UserDataMarkerName)),
                "Installer did not create the marked in-directory data folder.");
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

        private sealed class TrackingWeComLaunchPolicy : IWeComLaunchPolicy
        {
            internal int RequestedTarget { get; private set; }

            internal int DisposeCount { get; private set; }

            public IDisposable BeginLaunchSession(int targetCount)
            {
                RequestedTarget = targetCount;
                return new DelegateDisposable(() => DisposeCount++);
            }

            public WeComRegistryRecoveryResult RecoverPendingSession()
            {
                return WeComRegistryRecoveryResult.NoPendingRecovery;
            }
        }

        private sealed class DelegateDisposable : IDisposable
        {
            private Action _dispose;

            internal DelegateDisposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _dispose, null)?.Invoke();
            }
        }

        private sealed class LaunchingFakeProcessEnvironment : IProcessEnvironment
        {
            private int _count;

            internal LaunchingFakeProcessEnvironment(string executablePath, int initialCount)
            {
                ExecutablePath = executablePath;
                _count = initialCount;
            }

            internal string ExecutablePath { get; }

            internal List<TimeSpan> Delays { get; } = new List<TimeSpan>();

            public int CurrentSessionId => 7;

            public IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> processNames)
            {
                return Enumerable.Range(1, _count).Select(index => new ProcessSnapshot
                {
                    Id = 900000 + index,
                    SessionId = CurrentSessionId,
                    ExecutablePath = ExecutablePath
                }).ToArray();
            }

            public IReadOnlyDictionary<int, int> GetParentProcessMap()
            {
                return new Dictionary<int, int>();
            }

            public void StartApplication(string executablePath)
            {
                Assert(string.Equals(executablePath, ExecutablePath, StringComparison.OrdinalIgnoreCase),
                    "Unexpected executable path in deterministic launch test.");
                _count++;
            }

            public System.Threading.Tasks.Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                Delays.Add(delay);
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private sealed class MultiClientFakeProcessEnvironment : IProcessEnvironment
        {
            private readonly string _weChatPath;
            private readonly int _weChatCount;
            private readonly string _weComPath;
            private readonly int _weComCount;

            internal MultiClientFakeProcessEnvironment(string weChatPath, int weChatCount,
                string weComPath, int weComCount)
            {
                _weChatPath = weChatPath;
                _weChatCount = weChatCount;
                _weComPath = weComPath;
                _weComCount = weComCount;
            }

            public int CurrentSessionId => 7;

            public IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> processNames)
            {
                var isWeCom = processNames.Any(name =>
                    string.Equals(name, "WXWork", StringComparison.OrdinalIgnoreCase));
                var path = isWeCom ? _weComPath : _weChatPath;
                var count = isWeCom ? _weComCount : _weChatCount;
                var start = isWeCom ? 800000 : 700000;
                return Enumerable.Range(1, count).Select(index => new ProcessSnapshot
                {
                    Id = start + index,
                    SessionId = CurrentSessionId,
                    ExecutablePath = path
                }).ToArray();
            }

            public IReadOnlyDictionary<int, int> GetParentProcessMap()
            {
                return new Dictionary<int, int>();
            }

            public void StartApplication(string executablePath)
            {
            }

            public System.Threading.Tasks.Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
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
