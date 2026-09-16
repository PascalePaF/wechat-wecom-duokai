using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace WechatDuokai.Presentation
{
    internal enum AppTheme
    {
        Light,
        Dark
    }

    internal enum AppThemePreference
    {
        System,
        Light,
        Dark
    }

    internal static class ThemeManager
    {
        private const string MarkerName = ".wechat-duokai-user-data";
        private const string MarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";
        private const string ThemeFileName = "theme.ini";

        private static readonly ThemeColors Light = new ThemeColors
        {
            Window = "#F7F6F2",
            WindowTop = "#FBFAF7",
            Surface = "#FFFFFFFF",
            SurfaceRaised = "#FFFDF9",
            SurfaceHover = "#F5F1E9",
            Border = "#E5E0D7",
            BorderStrong = "#D8CDBB",
            Text = "#1D2025",
            TextSecondary = "#656B75",
            TextTertiary = "#969BA4",
            Accent = "#A47A43",
            AccentHover = "#8C6637",
            AccentPressed = "#76552E",
            AccentText = "#FFFFFFFF",
            Green = "#16845D",
            GreenSurface = "#EAF7F1",
            Blue = "#2379C8",
            Warning = "#B66A12",
            Danger = "#C54848",
            DangerHover = "#AE3A3A",
            DangerSurface = "#FFF1F1",
            Selection = "#F2E9DA",
            Shadow = "#2A2B211B"
        };

        private static readonly ThemeColors Dark = new ThemeColors
        {
            Window = "#101216",
            WindowTop = "#14171C",
            Surface = "#181B21",
            SurfaceRaised = "#1E222A",
            SurfaceHover = "#272C35",
            Border = "#2D323C",
            BorderStrong = "#514736",
            Text = "#F4F1E9",
            TextSecondary = "#A7ADB8",
            TextTertiary = "#767D89",
            Accent = "#D5B476",
            AccentHover = "#E3C78F",
            AccentPressed = "#B99A61",
            AccentText = "#17130D",
            Green = "#5FD3A1",
            GreenSurface = "#183127",
            Blue = "#72B8F4",
            Warning = "#E7A958",
            Danger = "#F08080",
            DangerHover = "#FF9A9A",
            DangerSurface = "#351F23",
            Selection = "#2B271F",
            Shadow = "#72000000"
        };

        internal static AppTheme Current { get; private set; }

        internal static AppThemePreference Preference { get; private set; } = AppThemePreference.System;

        internal static void Initialize(AppTheme? forcedTheme = null)
        {
            if (forcedTheme.HasValue)
            {
                Preference = forcedTheme.Value == AppTheme.Dark
                    ? AppThemePreference.Dark
                    : AppThemePreference.Light;
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
            SetTheme(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark, persist);
            return Current;
        }

        internal static void SetTheme(AppTheme theme, bool persist = true)
        {
            Preference = theme == AppTheme.Dark ? AppThemePreference.Dark : AppThemePreference.Light;
            Current = theme;
            ApplyResources(theme);
            if (persist)
            {
                SavePreference(Preference);
            }
        }

        internal static void SetPreference(AppThemePreference preference, bool persist = true)
        {
            Preference = preference;
            Current = ResolveTheme(preference);
            ApplyResources(Current);
            if (persist)
            {
                SavePreference(preference);
            }
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

            var colors = theme == AppTheme.Dark ? Dark : Light;
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
            try
            {
                var marker = Path.Combine(DataDirectory, MarkerName);
                var themeFile = Path.Combine(DataDirectory, ThemeFileName);
                if (!File.Exists(marker) || !File.Exists(themeFile) ||
                    !string.Equals(File.ReadAllText(marker, Encoding.UTF8).Trim(), MarkerValue, StringComparison.Ordinal))
                {
                    return AppThemePreference.System;
                }

                var saved = File.ReadAllText(themeFile, Encoding.UTF8).Trim();
                AppThemePreference preference;
                if (Enum.TryParse(saved, true, out preference) &&
                    Enum.IsDefined(typeof(AppThemePreference), preference)) return preference;
            }
            catch (Exception)
            {
            }

            return AppThemePreference.System;
        }

        private static void SavePreference(AppThemePreference preference)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(Path.Combine(DataDirectory, MarkerName), MarkerValue, Encoding.UTF8);
                var target = Path.Combine(DataDirectory, ThemeFileName);
                var temporary = target + ".new";
                File.WriteAllText(temporary, preference.ToString(), Encoding.UTF8);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
            }
            catch (Exception)
            {
            }
        }

        private static AppTheme ResolveTheme(AppThemePreference preference)
        {
            switch (preference)
            {
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
                var dark = ThemeManager.Current == AppTheme.Dark ? 1 : 0;
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
