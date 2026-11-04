using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
                    foreach (var name in new[] { "System", "Light", "Dark", "Halloween", "HalloweenLight" })
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

        internal static void HalloweenDayPalette()
        {
            try
            {
                foreach (var anchor in new[] { typeof(MainWindow), typeof(InstallWindow), Program.LoadCleanupWindowType() })
                {
                    Program.BootstrapWpf(anchor, "HalloweenLight");
                    var manager = anchor.Assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
                    Assert(!(bool)manager.GetProperty("IsDark", StaticInternal).GetValue(null) &&
                           (bool)manager.GetProperty("IsHalloween", StaticInternal).GetValue(null),
                        "Halloween day mode must retain its seasonal identity and use light window chrome.");
                    var surface = Program.GetLuminance(Program.GetThemeColor("SurfaceRaisedBrush"));
                    var text = Program.GetLuminance(Program.GetThemeColor("TextBrush"));
                    var accent = Program.GetLuminance(Program.GetThemeColor("AccentBrush"));
                    var accentText = Program.GetLuminance(Program.GetThemeColor("AccentTextBrush"));
                    Assert(surface >= 0.85 && (surface + 0.05) / (text + 0.05) >= 7 &&
                           (accentText + 0.05) / (accent + 0.05) >= 4.5,
                        "Halloween day palette is not light or its primary text lacks contrast.");
                    Assert((Visibility)Application.Current.Resources["HalloweenDecorationVisibility"] == Visibility.Visible,
                        "Halloween day decorations disappeared.");
                    var parsed = manager.GetMethod("ParseForcedTheme", StaticInternal).Invoke(null,
                        new object[] { new[] { "--theme=HALLOWEENLIGHT" } });
                    Assert(parsed.ToString() == "HalloweenLight", "Halloween day argument was lost.");
                    var toggle = manager.GetMethod("Toggle", StaticInternal);
                    Assert(toggle.Invoke(null, new object[] { false }).ToString() == "Halloween" &&
                           toggle.Invoke(null, new object[] { false }).ToString() == "HalloweenLight",
                        "Seasonal day/night toggling switched to an unrelated theme.");
                }
                Assert(ApplicationUpdateService.NormalizeThemeName("HALLOWEENLIGHT") == "halloweenlight" &&
                       ApplicationUpdateService.NormalizeThemeName("halloweenlight --unexpected") == "light",
                    "Updater does not preserve the exact allowlisted day identity.");
                Program.BootstrapWpf(typeof(MainWindow), "HalloweenLight");
                var window = new MainWindow();
                try
                {
                    Assert(((RadioButton)window.FindName("HalloweenLightThemeRadio")).IsChecked == true &&
                           ((RadioButton)window.FindName("HalloweenThemeRadio")).IsChecked != true &&
                           ((TextBlock)window.FindName("ThemeLabel")).Text == "万圣日间",
                        "Halloween day selection or header identity is incorrect.");
                }
                finally { window.Close(); }
            }
            finally { Program.BootstrapWpf(typeof(MainWindow), "Light"); }
        }

        internal static void SeasonalArtworkRoundTrip()
        {
            try
            {
                foreach (var anchor in new[] { typeof(MainWindow), typeof(InstallWindow), Program.LoadCleanupWindowType() })
                {
                    Program.BootstrapWpf(anchor, "Light");
                    var classicLogo = Application.Current.Resources["ProductLogoImage"];
                    var classicIcon = Application.Current.Resources["ProductWindowIcon"];
                    var window = (Window)Activator.CreateInstance(anchor);
                    try
                    {
                        ShowOffscreen(window);
                        foreach (var theme in new[] { "Halloween", "HalloweenLight", "Dark", "Light", "Halloween", "Light" })
                        {
                            Program.BootstrapWpf(anchor, theme);
                            FlushLayout(window);
                            var holiday = theme.StartsWith("Halloween", StringComparison.Ordinal);
                            var resources = Application.Current.Resources;
                            var logo = (Image)window.FindName(window is MainWindow ? "BrandLogo" : "HeaderLogo");
                            var scene = (FrameworkElement)window.FindName("HolidayScene");
                            Assert(ReferenceEquals(logo.Source, resources["ProductLogoImage"]) &&
                                   ReferenceEquals(window.Icon, resources["ProductWindowIcon"]),
                                "Header and native window icon diverged from the active theme: " + anchor.Name + "/" + theme);
                            Assert(scene != null && !scene.IsHitTestVisible && !scene.Focusable &&
                                   scene.Visibility == (holiday ? Visibility.Visible : Visibility.Collapsed),
                                "Seasonal scene intercepted input or leaked into another theme.");
                            if (holiday)
                            {
                                Assert(logo.Source is DrawingImage && logo.Source.IsFrozen &&
                                       window.Icon is BitmapSource && window.Icon.IsFrozen,
                                    "Seasonal artwork must be embedded, immutable vector/bitmap resources.");
                                foreach (var key in new[] { "WindowCanvasBrush", "CardBackgroundBrush", "RaisedCardBackgroundBrush", "CardBorderBrush", "PrimaryFillBrush" })
                                    Assert(resources[key] is LinearGradientBrush brush && brush.IsFrozen,
                                        "Seasonal material is missing or mutable: " + key);
                                Assert(resources["SeasonalTraceBrush"] is DrawingBrush trace && trace.IsFrozen &&
                                       resources["ThemePreviewHalloween"] is DrawingImage &&
                                       resources["ThemePreviewHalloweenLight"] is DrawingImage,
                                    "Seasonal borders or preview cards are not using the embedded artwork.");
                                var foreground = Program.GetLuminance(Program.GetThemeColor("AccentTextBrush"));
                                foreach (var stop in ((LinearGradientBrush)resources["PrimaryFillBrush"]).GradientStops)
                                {
                                    var background = Program.GetLuminance(stop.Color);
                                    Assert((Math.Max(background, foreground) + 0.05) / (Math.Min(background, foreground) + 0.05) >= 4.5,
                                        "A seasonal primary gradient endpoint makes its label unreadable: " + theme);
                                }
                            }
                            else
                            {
                                Assert(ReferenceEquals(logo.Source, classicLogo) && ReferenceEquals(window.Icon, classicIcon) &&
                                       ReferenceEquals(resources["PrimaryFillBrush"], resources["AccentBrush"]) &&
                                       ReferenceEquals(resources["WindowCanvasBrush"], resources["WindowBackgroundBrush"]) &&
                                       ReferenceEquals(resources["CardBorderBrush"], resources["BorderBrush"]) &&
                                       ((Thickness)resources["SeasonalCardThickness"]).Left == 1 &&
                                       (double)resources["ProductLogoSize"] == 58,
                                    "Leaving Halloween did not restore the original product identity and materials.");
                            }
                            CheckNativeIcon(window, holiday);
                        }
                    }
                    finally { window.Close(); }
                }
            }
            finally { Program.BootstrapWpf(typeof(MainWindow), "Light"); }
        }

        internal static void SeasonalLayoutMatrix()
        {
            try
            {
                foreach (var theme in new[] { "Halloween", "HalloweenLight" })
                {
                    Program.BootstrapWpf(typeof(MainWindow), theme);
                    var window = new MainWindow();
                    try
                    {
                        ShowOffscreen(window);
                        foreach (var size in new[] { new Size(901, 513), new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1392) })
                        {
                            window.Width = size.Width; window.Height = size.Height;
                            foreach (var section in new[] { -1, 0, 1, 2 })
                            {
                                window.SetSettingsViewVisible(section >= 0);
                                if (section >= 0) window.SetSettingsSection((MainWindow.SettingsSection)section);
                                FlushLayout(window);
                                CheckRenderedButtonLabels(window);
                                CheckButtonBounds(window);
                                var scene = (FrameworkElement)window.FindName("HolidayScene");
                                var root = (FrameworkElement)window.FindName("ScaledRoot");
                                Assert(Math.Abs(scene.ActualWidth - root.ActualWidth) < 0.1 &&
                                       Math.Abs(scene.ActualHeight - root.ActualHeight) < 0.1 && !scene.IsHitTestVisible && !scene.Focusable,
                                    "Decoration does not follow the scaled surface or can steal input.");
                            }
                            window.SetSettingsViewVisible(false);
                            FlushLayout(window);
                            foreach (var name in new[] { "QQButton", "WeChatButton", "WeComButton" })
                            {
                                var button = (Button)window.FindName(name);
                                var glyph = button.Template.FindName("SeasonalActionGlyph", button) as Image;
                                Assert(glyph != null && glyph.IsVisible && !glyph.IsHitTestVisible,
                                    "A seasonal launch action lost its passive ghost glyph.");
                            }
                        }
                    }
                    finally { window.Close(); }
                }
            }
            finally { Program.BootstrapWpf(typeof(MainWindow), "Light"); }
        }

        private static void ShowOffscreen(Window window)
        {
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000; window.Top = -10000;
            window.Show();
            FlushLayout(window);
        }

        private static void FlushLayout(Window window)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            window.UpdateLayout();
        }

        internal static void CheckButtonBounds(DependencyObject root)
        {
            if (root is Button button && button.IsVisible && button.Content is string content)
            {
                var label = FindLabel(button, content);
                Assert(label != null, "A visible action has no rendered label: " + content);
                var bounds = label.TransformToAncestor(button).TransformBounds(new Rect(label.RenderSize));
                var available = new Rect(button.RenderSize);
                available.Inflate(0.5, 0.5);
                Assert(available.Contains(bounds), "Button text escapes its control: " + content);
                var expectedText = new FormattedText(content, CultureInfo.CurrentCulture, label.FlowDirection,
                    new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch),
                    label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                Assert(label.ActualWidth + 1 >= expectedText.WidthIncludingTrailingWhitespace,
                    "Button label is clipped by its padding: " + content + "; rendered=" + label.ActualWidth +
                    "; required=" + expectedText.WidthIncludingTrailingWhitespace);
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                CheckButtonBounds(VisualTreeHelper.GetChild(root, index));
        }

        private static void CheckNativeIcon(Window window, bool holiday)
        {
            // Read the actual HICON supplied to the native window, not just Window.Icon.
            var handle = SendMessage(new WindowInteropHelper(window).Handle, 0x007F, new IntPtr(1), IntPtr.Zero);
            Assert(handle != IntPtr.Zero, "The active native window has no large icon.");
            using (var borrowed = System.Drawing.Icon.FromHandle(handle))
            using (var icon = (System.Drawing.Icon)borrowed.Clone())
            using (var bitmap = icon.ToBitmap())
            {
                var first = 0; var second = 0;
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.A < 32) continue;
                        if (holiday)
                        {
                            if (pixel.R >= pixel.G + 16 && pixel.G >= pixel.B + 10) first++;
                            if (pixel.B >= pixel.R + 8 && pixel.R >= pixel.G + 5) second++;
                        }
                        else
                        {
                            if (pixel.G >= pixel.R + 18 && pixel.G >= pixel.B + 10) first++;
                            if (pixel.B >= pixel.R + 18 && pixel.B >= pixel.G + 18) second++;
                        }
                    }
                Assert(first >= 8 && second >= 8, "The active native window icon retained colors from another theme.");
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

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
                window.SetSettingsViewVisible(true);
                window.SetSettingsSection(MainWindow.SettingsSection.Themes);
                Assert(((FrameworkElement)window.FindName("ThemeSettingsPanel")).Visibility == Visibility.Visible &&
                       ((FrameworkElement)window.FindName("GeneralSettingsPanel")).Visibility == Visibility.Collapsed &&
                       ((FrameworkElement)window.FindName("AboutSettingsPanel")).Visibility == Visibility.Collapsed,
                    "The dedicated theme tab did not open through settings navigation.");
                foreach (var size in new[] { new Size(901, 513), new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1392) })
                {
                    window.Width = size.Width; window.Height = size.Height;
                    window.UpdateLayout();
                    var panel = (FrameworkElement)window.FindName("ThemeSettingsPanel");
                    var panelBounds = new Rect(new Point(), panel.RenderSize);
                    foreach (var name in new[] { "SystemThemeRadio", "LightThemeRadio", "DarkThemeRadio", "HalloweenLightThemeRadio", "HalloweenThemeRadio" })
                    {
                        var radio = (RadioButton)window.FindName(name);
                        Assert(AutomationProperties.GetName(radio).Length > 0 &&
                               (name == "SystemThemeRadio" ? radio.ActualHeight >= 18 && radio.ActualWidth >= 140 :
                                   radio.ActualHeight >= 68 && radio.ActualWidth >= 300),
                            "Theme choice is not accessible or is clipped at a supported size: " + name +
                            " " + radio.ActualWidth + "x" + radio.ActualHeight);
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

        internal static void HeaderThemeCycle()
        {
            Program.BootstrapWpf(typeof(MainWindow), "Light");
            var window = new MainWindow();
            try
            {
                ShowOffscreen(window);
                typeof(MainWindow).GetMethod("SetBusy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { false });
                var manager = typeof(MainWindow).Assembly.GetType("WechatDuokai.Presentation.ThemeManager", true);
                var button = (Button)window.FindName("ThemeButton");
                foreach (var expected in new[] { "Dark", "HalloweenLight", "Halloween", "Light", "Dark" })
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    FlushLayout(window);
                    Assert(manager.GetProperty("Current", StaticInternal).GetValue(null).ToString() == expected,
                        "Header cycle skipped or repeated a theme: " + expected);
                    Assert(((FrameworkElement)window.FindName("SettingsWorkspace")).Visibility == Visibility.Collapsed,
                        "Cycling a theme navigated away from the main view.");
                    Assert(Convert.ToString(button.ToolTip).Contains("下一主题") &&
                           AutomationProperties.GetName(button) == "切换到下一个主题",
                        "Theme cycle lost its accessible action or next-theme hint.");
                }
                window.SetSettingsViewVisible(true);
                window.SetSettingsSection(MainWindow.SettingsSection.General);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(((FrameworkElement)window.FindName("GeneralSettingsPanel")).Visibility == Visibility.Visible &&
                       ((RadioButton)window.FindName("HalloweenLightThemeRadio")).IsChecked == true,
                    "Theme cycle changed the active settings page or left stale selection controls.");
                var preference = manager.GetProperty("Preference", StaticInternal).GetValue(null);
                Assert(preference.ToString() == "HalloweenLight", "A direct theme cycle must leave follow-system mode.");
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
            if (root is Button button && button.IsVisible && button.Content is string content)
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
