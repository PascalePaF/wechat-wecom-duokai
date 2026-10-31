using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using WechatDuokai.IO;

namespace WechatDuokai.Presentation
{
    internal enum AppTheme
    {
        Light,
        Dark,
        Halloween,
        HalloweenLight
    }

    internal enum AppThemePreference
    {
        System,
        Light,
        Dark,
        Halloween,
        HalloweenLight
    }

    internal static class ThemeManager
    {
        private const string MarkerName = ".wechat-duokai-user-data";
        private const string MarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";
        private const string ThemeFileName = "theme.ini";

        private static readonly ThemeColors Light = new ThemeColors
        {
            Window = "#F4F8F7",
            WindowTop = "#FAFCFC",
            Surface = "#FFFFFFFF",
            SurfaceRaised = "#FBFDFC",
            SurfaceHover = "#EFF6F4",
            Border = "#DCE9E5",
            BorderStrong = "#C4D8D2",
            Text = "#18221F",
            TextSecondary = "#5E6C67",
            TextTertiary = "#899791",
            Accent = "#168F55",
            AccentHover = "#117945",
            AccentPressed = "#0E663A",
            AccentText = "#FFFFFFFF",
            Green = "#168F55",
            GreenSurface = "#E7F7EF",
            Blue = "#2F78D5",
            Warning = "#B66A12",
            Danger = "#C54848",
            DangerHover = "#AE3A3A",
            DangerSurface = "#FFF1F1",
            Selection = "#E8F7F0",
            WeChatIconSurface = "#E4F8EE",
            WeComIconSurface = "#E7F2FE",
            QqIconSurface = "#E7F2FE",
            Shadow = "#24304B43"
        };

        private static readonly ThemeColors Dark = new ThemeColors
        {
            Window = "#101719",
            WindowTop = "#131B1E",
            Surface = "#172125",
            SurfaceRaised = "#1B282D",
            SurfaceHover = "#223238",
            Border = "#2A3C42",
            BorderStrong = "#385057",
            Text = "#DFE8E6",
            TextSecondary = "#98AAA7",
            TextTertiary = "#6F8581",
            Accent = "#2A7F5A",
            AccentHover = "#348B65",
            AccentPressed = "#226B4C",
            AccentText = "#EAF6F1",
            Green = "#3A8F68",
            GreenSurface = "#173128",
            Blue = "#4778AA",
            Warning = "#B3844D",
            Danger = "#B85E61",
            DangerHover = "#C86C70",
            DangerSurface = "#2D1D21",
            Selection = "#1B3029",
            WeChatIconSurface = "#19332A",
            WeComIconSurface = "#1A2D3C",
            QqIconSurface = "#1A2D3C",
            Shadow = "#7A000000"
        };

        private static readonly ThemeColors Halloween = new ThemeColors
        {
            Window = "#17131E", WindowTop = "#1C1725",
            Surface = "#211C2B", SurfaceRaised = "#292333", SurfaceHover = "#352B40",
            Border = "#40364B", BorderStrong = "#62516D",
            Text = "#EEE6DE", TextSecondary = "#BEB0C8", TextTertiary = "#A294AC",
            Accent = "#D39560", AccentHover = "#DDA573", AccentPressed = "#BA7D4B",
            AccentText = "#22170F", Green = "#80AA91", GreenSurface = "#203329",
            Blue = "#90A0CA", Warning = "#D39560",
            Danger = "#CB8389", DangerHover = "#D79196", DangerSurface = "#39232D",
            Selection = "#392B27", WeChatIconSurface = "#23332D",
            WeComIconSurface = "#262F40", QqIconSurface = "#262F40", Shadow = "#75000000"
        };

        private static readonly ThemeColors HalloweenLight = new ThemeColors
        {
            Window = "#FFF7EE", WindowTop = "#FFFBF6", Surface = "#FFFEFC",
            SurfaceRaised = "#FFF7EF", SurfaceHover = "#F8EBDD",
            Border = "#E9D6C3", BorderStrong = "#D5B99C",
            Text = "#322824", TextSecondary = "#746451", TextTertiary = "#826E60",
            Accent = "#A95723", AccentHover = "#934615", AccentPressed = "#7D3A0F", AccentText = "#FFFDF9",
            Green = "#35774E", GreenSurface = "#E9F3E8", Blue = "#7460A2", Warning = "#A95723",
            Danger = "#AD4952", DangerHover = "#963944", DangerSurface = "#FBECEE",
            Selection = "#FCEBDD", WeChatIconSurface = "#E9F4E9",
            WeComIconSurface = "#F0EDF8", QqIconSurface = "#F0EDF8", Shadow = "#203D2C1B"
        };

        internal static AppTheme Current { get; private set; }
        internal static bool IsDark => Current == AppTheme.Dark || Current == AppTheme.Halloween;
        internal static bool IsHalloween => Current == AppTheme.Halloween || Current == AppTheme.HalloweenLight;
        internal static string CommandLineName => Current.ToString().ToLowerInvariant();
        internal static string DisplayName => Current == AppTheme.HalloweenLight ? "万圣日间" : Current == AppTheme.Halloween ? "万圣夜" :
            Current == AppTheme.Dark ? "夜间" : "日间";

        internal static AppThemePreference Preference { get; private set; } = AppThemePreference.System;

        internal static void Initialize(AppTheme? forcedTheme = null)
        {
            if (forcedTheme.HasValue)
            {
                if (!Enum.IsDefined(typeof(AppTheme), forcedTheme.Value))
                    throw new ArgumentOutOfRangeException(nameof(forcedTheme));
                Preference = PreferenceFor(forcedTheme.Value);
                Current = forcedTheme.Value;
            }
            else
            {
                Preference = LoadSavedPreference();
                Current = ResolveTheme(Preference);
            }
            ApplyResources(Current);
        }

        internal static AppTheme Toggle(bool persist = true)
        {
            SetTheme(Current == AppTheme.Halloween ? AppTheme.HalloweenLight :
                Current == AppTheme.HalloweenLight ? AppTheme.Halloween : IsDark ? AppTheme.Light : AppTheme.Dark, persist);
            return Current;
        }

        internal static void SetTheme(AppTheme theme, bool persist = true)
        {
            if (!Enum.IsDefined(typeof(AppTheme), theme)) throw new ArgumentOutOfRangeException(nameof(theme));
            Preference = PreferenceFor(theme);
            Current = theme;
            ApplyResources(theme);
            if (persist)
            {
                SavePreference(Preference);
            }
        }

        internal static bool SetPreference(AppThemePreference preference, bool persist = true)
        {
            if (!Enum.IsDefined(typeof(AppThemePreference), preference))
                throw new ArgumentOutOfRangeException(nameof(preference));
            Preference = preference;
            Current = ResolveTheme(preference);
            ApplyResources(Current);
            return !persist || SavePreference(preference);
        }

        private static AppThemePreference PreferenceFor(AppTheme theme)
        {
            return theme == AppTheme.HalloweenLight ? AppThemePreference.HalloweenLight : theme == AppTheme.Halloween ? AppThemePreference.Halloween :
                theme == AppTheme.Dark ? AppThemePreference.Dark : AppThemePreference.Light;
        }

        internal static AppTheme? ParseForcedTheme(string[] arguments)
        {
            foreach (var argument in arguments ?? new string[0])
            {
                if (argument == null || !argument.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase)) continue;
                var name = argument.Substring("--theme=".Length);
                foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
                    if (string.Equals(name, theme.ToString(), StringComparison.OrdinalIgnoreCase)) return theme;
                return null;
            }
            return null;
        }

        internal static bool RefreshSystemTheme()
        {
            if (Preference != AppThemePreference.System)
            {
                return false;
            }

            var resolved = ReadWindowsTheme();
            if (resolved == Current)
            {
                return false;
            }

            Current = resolved;
            ApplyResources(Current);
            return true;
        }

        private static void ApplyResources(AppTheme theme)
        {
            var application = Application.Current;
            if (application == null)
            {
                return;
            }

            var colors = theme == AppTheme.HalloweenLight ? HalloweenLight : theme == AppTheme.Halloween ? Halloween : theme == AppTheme.Dark ? Dark : Light;
            application.Resources["HalloweenDecorationVisibility"] = IsHalloween
                ? Visibility.Visible : Visibility.Collapsed;
            SetBrush(application, "WindowBackgroundBrush", colors.Window);
            SetBrush(application, "WindowTopBrush", colors.WindowTop);
            SetBrush(application, "SurfaceBrush", colors.Surface);
            SetBrush(application, "SurfaceRaisedBrush", colors.SurfaceRaised);
            SetBrush(application, "SurfaceHoverBrush", colors.SurfaceHover);
            SetBrush(application, "BorderBrush", colors.Border);
            SetBrush(application, "BorderStrongBrush", colors.BorderStrong);
            SetBrush(application, "TextBrush", colors.Text);
            SetBrush(application, "TextSecondaryBrush", colors.TextSecondary);
            SetBrush(application, "TextTertiaryBrush", colors.TextTertiary);
            SetBrush(application, "AccentBrush", colors.Accent);
            SetBrush(application, "AccentHoverBrush", colors.AccentHover);
            SetBrush(application, "AccentPressedBrush", colors.AccentPressed);
            SetBrush(application, "AccentTextBrush", colors.AccentText);
            SetBrush(application, "SuccessBrush", colors.Green);
            SetBrush(application, "SuccessSurfaceBrush", colors.GreenSurface);
            SetBrush(application, "InfoBrush", colors.Blue);
            SetBrush(application, "WarningBrush", colors.Warning);
            SetBrush(application, "DangerBrush", colors.Danger);
            SetBrush(application, "DangerHoverBrush", colors.DangerHover);
            SetBrush(application, "DangerSurfaceBrush", colors.DangerSurface);
            SetBrush(application, "SelectionBrush", colors.Selection);
            SetBrush(application, "WeChatIconSurfaceBrush", colors.WeChatIconSurface);
            SetBrush(application, "WeComIconSurfaceBrush", colors.WeComIconSurface);
            SetBrush(application, "QqIconSurfaceBrush", colors.QqIconSurface);
            application.Resources["ShadowColor"] = ParseColor(colors.Shadow);
        }

        private static void SetBrush(Application application, string key, string value)
        {
            var brush = new SolidColorBrush(ParseColor(value));
            brush.Freeze();
            application.Resources[key] = brush;
        }

        private static Color ParseColor(string value)
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }

        private static string DataDirectory => Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "data");

        private static AppThemePreference LoadSavedPreference()
        {
            return LoadPreferenceFromDirectory(DataDirectory);
        }

        internal static AppThemePreference LoadPreferenceFromDirectory(string dataDirectory)
        {
            try
            {
                var marker = Path.Combine(dataDirectory, MarkerName);
                var themeFile = Path.Combine(dataDirectory, ThemeFileName);
                SafeLocalFile.RejectReparsePoints(marker);
                SafeLocalFile.RejectReparsePoints(themeFile);
                if (!File.Exists(marker) || !File.Exists(themeFile) ||
                    !string.Equals(SafeLocalFile.ReadTextBounded(marker, 128).Trim(), MarkerValue, StringComparison.Ordinal))
                {
                    return AppThemePreference.System;
                }

                var saved = SafeLocalFile.ReadTextBounded(themeFile, 64).Trim();
                AppThemePreference preference;
                if (Enum.TryParse(saved, true, out preference) &&
                    Enum.IsDefined(typeof(AppThemePreference), preference)) return preference;
            }
            catch (Exception)
            {
            }

            return AppThemePreference.System;
        }

        private static bool SavePreference(AppThemePreference preference)
        {
            return TrySavePreferenceToDirectory(DataDirectory, preference);
        }

        internal static bool TrySavePreferenceToDirectory(string directory, AppThemePreference preference)
        {
            try
            {
                SavePreferenceToDirectory(directory, preference);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void SavePreferenceToDirectory(string dataDirectory, AppThemePreference preference)
        {
            if (!Enum.IsDefined(typeof(AppThemePreference), preference))
                throw new ArgumentOutOfRangeException(nameof(preference));
            SafeLocalFile.EnsureDirectory(dataDirectory);
            var marker = Path.Combine(dataDirectory, MarkerName);
            var target = Path.Combine(dataDirectory, ThemeFileName);
            // Validate both before committing either, so a redirected preference cannot alter the marker.
            SafeLocalFile.RejectReparsePoints(marker);
            SafeLocalFile.RejectReparsePoints(target);
            SafeLocalFile.WriteTextAtomic(marker, MarkerValue);
            SafeLocalFile.WriteTextAtomic(target, preference.ToString());
        }

        private static AppTheme ResolveTheme(AppThemePreference preference)
        {
            switch (preference)
            {
                case AppThemePreference.Halloween:
                    return AppTheme.Halloween;
                case AppThemePreference.HalloweenLight:
                    return AppTheme.HalloweenLight;
                case AppThemePreference.Dark:
                    return AppTheme.Dark;
                case AppThemePreference.Light:
                    return AppTheme.Light;
                default:
                    return ReadWindowsTheme();
            }
        }

        private static AppTheme ReadWindowsTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int number)
                    {
                        return number == 0 ? AppTheme.Dark : AppTheme.Light;
                    }
                }
            }
            catch (Exception)
            {
            }

            return AppTheme.Light;
        }

        private sealed class ThemeColors
        {
            internal string Window { get; set; }
            internal string WindowTop { get; set; }
            internal string Surface { get; set; }
            internal string SurfaceRaised { get; set; }
            internal string SurfaceHover { get; set; }
            internal string Border { get; set; }
            internal string BorderStrong { get; set; }
            internal string Text { get; set; }
            internal string TextSecondary { get; set; }
            internal string TextTertiary { get; set; }
            internal string Accent { get; set; }
            internal string AccentHover { get; set; }
            internal string AccentPressed { get; set; }
            internal string AccentText { get; set; }
            internal string Green { get; set; }
            internal string GreenSurface { get; set; }
            internal string Blue { get; set; }
            internal string Warning { get; set; }
            internal string Danger { get; set; }
            internal string DangerHover { get; set; }
            internal string DangerSurface { get; set; }
            internal string Selection { get; set; }
            internal string WeChatIconSurface { get; set; }
            internal string WeComIconSurface { get; set; }
            internal string QqIconSurface { get; set; }
            internal string Shadow { get; set; }
        }
    }

    internal static class WindowChromeHelper
    {
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmWindowCornerPreference = 33;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;

        internal static void Apply(Window window)
        {
            if (window == null || Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var dark = ThemeManager.IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
                }

                var roundedCorners = 2;
                DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref roundedCorners, sizeof(int));

                var caption = ToColorRef(GetColor("WindowTopBrush"));
                var border = ToColorRef(GetColor("BorderBrush"));
                var text = ToColorRef(GetColor("TextBrush"));
                DwmSetWindowAttribute(handle, DwmCaptionColor, ref caption, sizeof(int));
                DwmSetWindowAttribute(handle, DwmBorderColor, ref border, sizeof(int));
                DwmSetWindowAttribute(handle, DwmTextColor, ref text, sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static Color GetColor(string resourceKey)
        {
            var brush = Application.Current?.Resources[resourceKey] as SolidColorBrush;
            return brush?.Color ?? Colors.Black;
        }

        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
