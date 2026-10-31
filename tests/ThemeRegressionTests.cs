using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WechatDuokai.App;
using WechatDuokai.Core;
using WechatDuokai.Installer;

namespace WechatDuokai.Tests
{
    internal static class ThemeRegressionTests
    {
        private const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;

        internal static void SharedThemeRoundTrip()
        {
            var directory = Path.Combine(Path.GetTempPath(), "wechat-duokai-halloween-" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var anchor in new[] { typeof(MainWindow), typeof(InstallWindow), Program.LoadCleanupWindowType() })
                {
                    var manager = anchor.Assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
                    var preference = anchor.Assembly.GetType("WechatDuokai.Presentation.AppThemePreference", true);
                    foreach (var name in new[] { "System", "Light", "Dark", "Halloween" })
                    {
                        manager.GetMethod("SavePreferenceToDirectory", StaticInternal).Invoke(null,
                            new[] { (object)directory, Enum.Parse(preference, name) });
                        var saved = manager.GetMethod("LoadPreferenceFromDirectory", StaticInternal).Invoke(null, new object[] { directory });
                        Assert(saved.ToString() == name, "Saved theme did not survive a fresh read: " + name);
                    }
                    Program.BootstrapWpf(anchor, "Halloween");
                    Assert(manager.GetProperty("Current", StaticInternal).GetValue(null).ToString() == "Halloween" &&
                           manager.GetProperty("Preference", StaticInternal).GetValue(null).ToString() == "Halloween" &&
                           (bool)manager.GetProperty("IsDark", StaticInternal).GetValue(null), "Seasonal theme state lost its identity.");
                    Assert((Visibility)Application.Current.Resources["HalloweenDecorationVisibility"] == Visibility.Visible,
                        "Halloween decoration was not enabled.");
                    var parse = manager.GetMethod("ParseForcedTheme", StaticInternal);
                    Assert(parse.Invoke(null, new object[] { new[] { "--theme=HaLlOwEeN" } }).ToString() == "Halloween",
                        "Installer theme argument was not recognized.");
                    foreach (var argument in new[] { "--theme=notreallydark", "--theme=3", "--theme=unknown" })
                        Assert(parse.Invoke(null, new object[] { new[] { argument } }) == null, "Invalid theme argument was accepted.");
                    var text = Program.GetLuminance(Program.GetThemeColor("TextBrush"));
                    var surface = Program.GetLuminance(Program.GetThemeColor("SurfaceRaisedBrush"));
                    var accent = Program.GetLuminance(Program.GetThemeColor("AccentBrush"));
                    var accentText = Program.GetLuminance(Program.GetThemeColor("AccentTextBrush"));
                    Assert((text + 0.05) / (surface + 0.05) >= 7 && (accent + 0.05) / (accentText + 0.05) >= 4.5,
                        "Halloween foregrounds do not meet the readable contrast target.");
                    Assert(Program.GetLuminance(Program.GetThemeColor("WeChatIconSurfaceBrush")) < 0.05 &&
                           Program.GetLuminance(Program.GetThemeColor("WeComIconSurfaceBrush")) < 0.05,
                        "Halloween icon surfaces are too bright.");
                    Program.BootstrapWpf(anchor, "Light");
                    Assert((Visibility)Application.Current.Resources["HalloweenDecorationVisibility"] == Visibility.Collapsed,
                        "Seasonal decorations leaked into another theme.");
                }
                Assert(ApplicationUpdateService.NormalizeThemeName("HALLOWEEN") == "halloween" &&
                       ApplicationUpdateService.NormalizeThemeName("dark --unexpected") == "light",
                    "Updater theme handoff did not preserve the allowlisted seasonal identity.");
            }
            finally
            {
                Program.DeleteTestDirectoryWithRetries(directory);
                Program.BootstrapWpf(typeof(MainWindow), "Light");
            }
        }

        internal static void BoundedLocalState()
        {
            var directory = Path.Combine(Path.GetTempPath(), "wechat-duokai-bounded-state-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var marker = Path.Combine(directory, ApplicationStorage.DataMarkerName);
            var themeFile = Path.Combine(directory, "theme.ini");
            try
            {
                File.WriteAllText(marker, ApplicationStorage.DataMarkerValue);
                var settings = Path.Combine(directory, "settings.ini");
                File.WriteAllText(settings, new string('x', 65537));
                File.WriteAllText(settings + ".bak", "TargetInstanceCount=4");
                Assert(UserPreferences.LoadTargetCount(directory) == 4, "Oversized settings did not recover from a bounded backup.");
                File.WriteAllText(Path.Combine(directory, UpdateCheckSchedule.StateFileName), new string('x', 65537));
                Assert(UpdateCheckSchedule.Load(directory).ConsecutiveFailures == 0, "Oversized update state did not fail safely.");
                foreach (var anchor in new[] { typeof(MainWindow), typeof(InstallWindow), Program.LoadCleanupWindowType() })
                {
                    var manager = anchor.Assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
                    var preference = anchor.Assembly.GetType("WechatDuokai.Presentation.AppThemePreference", true);
                    File.WriteAllText(themeFile, new string('x', 65));
                    Assert(manager.GetMethod("LoadPreferenceFromDirectory", StaticInternal).Invoke(null, new object[] { directory }).ToString() == "System",
                        "Oversized theme preference did not fall back to the system.");
                    File.WriteAllText(themeFile, "Dark");
                    using (var locked = new FileStream(themeFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        var saved = (bool)manager.GetMethod("TrySavePreferenceToDirectory", StaticInternal).Invoke(null,
                            new[] { (object)directory, Enum.Parse(preference, "Halloween") });
                        Assert(!saved, "Theme write failure was reported as a successful save.");
                    }
                    Assert(File.ReadAllText(themeFile) == "Dark", "Failed theme save destroyed the previous preference.");
                }
                File.WriteAllText(marker, new string('x', 129));
                Assert(!ApplicationStorage.HasValidMarker(directory), "Oversized data marker was accepted.");
                var cleanupPlan = typeof(InstallerEngine).Assembly.GetType("WechatDuokai.Installer.CleanupPlan", true);
                try
                {
                    cleanupPlan.GetMethod("Read").Invoke(null, new object[] { settings });
                    throw new Exception("Oversized cleanup plan was accepted.");
                }
                catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException) { }
            }
            finally { Program.DeleteTestDirectoryWithRetries(directory); }
        }

        internal static void ThemePageNavigation()
        {
            Program.BootstrapWpf(typeof(MainWindow), "Halloween");
            var window = new MainWindow { ShowInTaskbar = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show();
                ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(((FrameworkElement)window.FindName("ThemeSettingsPanel")).Visibility == Visibility.Visible &&
                       ((FrameworkElement)window.FindName("GeneralSettingsPanel")).Visibility == Visibility.Collapsed &&
                       ((FrameworkElement)window.FindName("AboutSettingsPanel")).Visibility == Visibility.Collapsed,
                    "The header shortcut did not open the dedicated theme tab.");
                foreach (var size in new[] { new Size(901, 513), new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1392) })
                {
                    window.Width = size.Width; window.Height = size.Height;
                    window.UpdateLayout();
                    var panel = (FrameworkElement)window.FindName("ThemeSettingsPanel");
                    var panelBounds = new Rect(new Point(), panel.RenderSize);
                    foreach (var name in new[] { "SystemThemeRadio", "LightThemeRadio", "DarkThemeRadio", "HalloweenThemeRadio" })
                    {
                        var radio = (RadioButton)window.FindName(name);
                        Assert(AutomationProperties.GetName(radio).Length > 0 && radio.ActualHeight >= 80 && radio.ActualWidth >= 300,
                            "Theme choice is not accessible or is clipped at a supported size.");
                        var bounds = radio.TransformToAncestor(panel).TransformBounds(new Rect(radio.RenderSize));
                        Assert(panelBounds.Contains(bounds), "Theme preview escaped its settings panel.");
                    }
                }
                window.SetSettingsSection(MainWindow.SettingsSection.General);
                Assert(((FrameworkElement)window.FindName("ThemeSettingsPanel")).Visibility == Visibility.Collapsed,
                    "Theme controls remained visible in general settings.");
                window.SetSettingsViewVisible(false);
                window.UpdateLayout();
                var launch = (Button)window.FindName("QQButton");
                var label = FindLabel(launch, Convert.ToString(launch.Content));
                Assert(label != null && ((SolidColorBrush)label.Foreground).Color == ((SolidColorBrush)launch.Foreground).Color,
                    "Rendered button label ignored its theme-specific foreground: label=" + (label == null ? "missing" : label.Foreground.ToString()) +
                    "; button=" + launch.Foreground + "; content=" + launch.Content);
            }
            finally { window.Close(); Program.BootstrapWpf(typeof(MainWindow), "Light"); }
        }

        internal static void CloseDuringStartupRecovery()
        {
            using (var policy = new BlockingRecoveryPolicy())
            {
                var window = new MainWindow(new InstanceManager(new WindowsProcessEnvironment(), policy), new ReleaseUpdateChecker())
                    { ShowInTaskbar = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
                try
                {
                    window.Show();
                    Assert(policy.Started.Wait(3000), "Startup recovery did not enter the controlled fixture.");
                    window.Close();
                    policy.Release.Set();
                    Assert(policy.Completed.Wait(3000), "Startup recovery did not finish.");
                    var frame = new DispatcherFrame();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                    Dispatcher.PushFrame(frame);
                    foreach (var name in new[] { "_refreshTimer", "_automaticUpdateTimer" })
                        Assert(!((DispatcherTimer)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(window)).IsEnabled, "Startup continuation revived a closed window's timers.");
                }
                finally { policy.Release.Set(); window.Close(); }
            }
        }

        private sealed class BlockingRecoveryPolicy : IWeComLaunchPolicy, IDisposable
        {
            internal readonly ManualResetEventSlim Started = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Completed = new ManualResetEventSlim();
            public IDisposable BeginLaunchSession(int count) => throw new NotSupportedException();
            public WeComRegistryRecoveryResult RecoverPendingSession()
            {
                Started.Set();
                Release.Wait();
                Completed.Set();
                return WeComRegistryRecoveryResult.NoPendingRecovery;
            }
            public void Dispose() { Started.Dispose(); Release.Dispose(); Completed.Dispose(); }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static TextBlock FindLabel(DependencyObject root, string content)
        {
            if (root is TextBlock text && text.Text == content) return text;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var label = FindLabel(VisualTreeHelper.GetChild(root, index), content);
                if (label != null) return label;
            }
            return null;
        }

        internal static void CheckRenderedButtonLabels(DependencyObject root)
        {
            if (root is Button button && button.Content is string content)
            {
                var label = FindLabel(button, content);
                Assert(label != null && ((SolidColorBrush)label.Foreground).Color == ((SolidColorBrush)button.Foreground).Color,
                    "Rendered secondary-window label differs from its button: " + content + "; actual=" +
                    (label == null ? "missing" : label.Foreground.ToString()) + "; expected=" + button.Foreground);
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                CheckRenderedButtonLabels(VisualTreeHelper.GetChild(root, index));
        }
    }
}
