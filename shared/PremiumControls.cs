using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WechatDuokai.UI
{
    internal enum AppTheme
    {
        Light,
        Dark
    }

    internal sealed class UiThemeColors
    {
        internal Color Canvas { get; set; }
        internal Color CanvasTop { get; set; }
        internal Color Surface { get; set; }
        internal Color SurfaceRaised { get; set; }
        internal Color SurfaceHover { get; set; }
        internal Color Border { get; set; }
        internal Color BorderSoft { get; set; }
        internal Color Ivory { get; set; }
        internal Color Muted { get; set; }
        internal Color MutedDark { get; set; }
        internal Color Gold { get; set; }
        internal Color GoldHover { get; set; }
        internal Color GoldPressed { get; set; }
        internal Color Green { get; set; }
        internal Color Blue { get; set; }
        internal Color Warning { get; set; }
        internal Color Danger { get; set; }
        internal Color DangerHover { get; set; }
        internal Color DangerPressed { get; set; }
        internal Color GoldSurface { get; set; }
        internal Color GoldBorder { get; set; }
        internal Color DangerSurface { get; set; }
        internal Color DangerBorder { get; set; }
        internal Color SuccessSurface { get; set; }
        internal Color SuccessBorder { get; set; }
        internal Color WechatSurface { get; set; }
        internal Color WechatBorder { get; set; }
        internal Color WecomSurface { get; set; }
        internal Color WecomBorder { get; set; }
        internal Color ActionSurface { get; set; }
    }

    internal static class ThemeManager
    {
        private const string MarkerName = ".wechat-duokai-user-data";
        private const string MarkerValue = "wechat-duokai-user-data:b492a149-7644-42ca-b815-a0c11b69d07b";
        private const string ThemeFileName = "theme.ini";
        private static bool _initialized;

        internal static AppTheme Current { get; private set; } = AppTheme.Dark;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Current = LoadSavedTheme() ?? ReadWindowsTheme();
        }

        internal static void SetTheme(AppTheme theme, bool persist = true)
        {
            Current = theme;
            _initialized = true;
            if (persist)
            {
                SaveTheme(theme);
            }
        }

        private static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatDuokai");

        private static AppTheme? LoadSavedTheme()
        {
            try
            {
                var markerPath = Path.Combine(DataDirectory, MarkerName);
                var themePath = Path.Combine(DataDirectory, ThemeFileName);
                if (!File.Exists(markerPath) || !File.Exists(themePath) ||
                    !string.Equals(File.ReadAllText(markerPath, Encoding.UTF8).Trim(), MarkerValue,
                        StringComparison.Ordinal))
                {
                    return null;
                }

                var value = File.ReadAllText(themePath, Encoding.UTF8).Trim();
                if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase))
                {
                    return AppTheme.Light;
                }
                if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    return AppTheme.Dark;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static AppTheme ReadWindowsTheme()
        {
            try
            {
                var value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    0);
                return Convert.ToInt32(value) == 0 ? AppTheme.Dark : AppTheme.Light;
            }
            catch (Exception)
            {
                return AppTheme.Dark;
            }
        }

        private static void SaveTheme(AppTheme theme)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(Path.Combine(DataDirectory, MarkerName), MarkerValue, Encoding.UTF8);
                var path = Path.Combine(DataDirectory, ThemeFileName);
                var temporaryPath = path + ".new";
                File.WriteAllText(temporaryPath, theme.ToString(), Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temporaryPath, path);
            }
            catch (Exception)
            {
                // Theme persistence is optional; the live theme switch still succeeds.
            }
        }
    }

    internal static class UiPalette
    {
        private static readonly UiThemeColors Dark = new UiThemeColors
        {
            Canvas = Color.FromArgb(13, 15, 18),
            CanvasTop = Color.FromArgb(22, 24, 29),
            Surface = Color.FromArgb(24, 26, 31),
            SurfaceRaised = Color.FromArgb(31, 34, 40),
            SurfaceHover = Color.FromArgb(41, 44, 52),
            Border = Color.FromArgb(52, 55, 64),
            BorderSoft = Color.FromArgb(40, 43, 50),
            Ivory = Color.FromArgb(246, 243, 235),
            Muted = Color.FromArgb(164, 167, 175),
            MutedDark = Color.FromArgb(112, 116, 126),
            Gold = Color.FromArgb(211, 183, 126),
            GoldHover = Color.FromArgb(228, 203, 151),
            GoldPressed = Color.FromArgb(190, 157, 97),
            Green = Color.FromArgb(111, 211, 149),
            Blue = Color.FromArgb(119, 171, 247),
            Warning = Color.FromArgb(234, 174, 107),
            Danger = Color.FromArgb(230, 112, 112),
            DangerHover = Color.FromArgb(240, 135, 135),
            DangerPressed = Color.FromArgb(195, 78, 78),
            GoldSurface = Color.FromArgb(40, 37, 31),
            GoldBorder = Color.FromArgb(88, 75, 51),
            DangerSurface = Color.FromArgb(42, 31, 33),
            DangerBorder = Color.FromArgb(91, 54, 59),
            SuccessSurface = Color.FromArgb(27, 38, 32),
            SuccessBorder = Color.FromArgb(48, 77, 60),
            WechatSurface = Color.FromArgb(29, 38, 34),
            WechatBorder = Color.FromArgb(50, 73, 61),
            WecomSurface = Color.FromArgb(27, 34, 43),
            WecomBorder = Color.FromArgb(44, 64, 85),
            ActionSurface = Color.FromArgb(31, 33, 38)
        };

        private static readonly UiThemeColors Light = new UiThemeColors
        {
            Canvas = Color.FromArgb(244, 242, 237),
            CanvasTop = Color.FromArgb(255, 255, 253),
            Surface = Color.FromArgb(255, 255, 253),
            SurfaceRaised = Color.FromArgb(247, 245, 240),
            SurfaceHover = Color.FromArgb(237, 233, 224),
            Border = Color.FromArgb(205, 201, 191),
            BorderSoft = Color.FromArgb(225, 221, 212),
            Ivory = Color.FromArgb(34, 34, 32),
            Muted = Color.FromArgb(94, 96, 101),
            MutedDark = Color.FromArgb(126, 128, 132),
            Gold = Color.FromArgb(151, 112, 48),
            GoldHover = Color.FromArgb(176, 134, 62),
            GoldPressed = Color.FromArgb(127, 91, 35),
            Green = Color.FromArgb(35, 137, 77),
            Blue = Color.FromArgb(46, 104, 184),
            Warning = Color.FromArgb(176, 104, 34),
            Danger = Color.FromArgb(184, 61, 67),
            DangerHover = Color.FromArgb(205, 77, 82),
            DangerPressed = Color.FromArgb(151, 43, 49),
            GoldSurface = Color.FromArgb(250, 245, 233),
            GoldBorder = Color.FromArgb(216, 198, 159),
            DangerSurface = Color.FromArgb(253, 241, 241),
            DangerBorder = Color.FromArgb(229, 190, 192),
            SuccessSurface = Color.FromArgb(237, 249, 241),
            SuccessBorder = Color.FromArgb(181, 220, 194),
            WechatSurface = Color.FromArgb(238, 249, 242),
            WechatBorder = Color.FromArgb(183, 220, 196),
            WecomSurface = Color.FromArgb(239, 246, 253),
            WecomBorder = Color.FromArgb(181, 207, 234),
            ActionSurface = Color.FromArgb(252, 250, 246)
        };

        internal static UiThemeColors For(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;
        private static UiThemeColors Current => For(ThemeManager.Current);

        internal static Color Canvas => Current.Canvas;
        internal static Color CanvasTop => Current.CanvasTop;
        internal static Color Surface => Current.Surface;
        internal static Color SurfaceRaised => Current.SurfaceRaised;
        internal static Color SurfaceHover => Current.SurfaceHover;
        internal static Color Border => Current.Border;
        internal static Color BorderSoft => Current.BorderSoft;
        internal static Color Ivory => Current.Ivory;
        internal static Color Muted => Current.Muted;
        internal static Color MutedDark => Current.MutedDark;
        internal static Color Gold => Current.Gold;
        internal static Color GoldHover => Current.GoldHover;
        internal static Color GoldPressed => Current.GoldPressed;
        internal static Color Green => Current.Green;
        internal static Color Blue => Current.Blue;
        internal static Color Warning => Current.Warning;
        internal static Color Danger => Current.Danger;
        internal static Color DangerHover => Current.DangerHover;
        internal static Color DangerPressed => Current.DangerPressed;
        internal static Color GoldSurface => Current.GoldSurface;
        internal static Color GoldBorder => Current.GoldBorder;
        internal static Color DangerSurface => Current.DangerSurface;
        internal static Color DangerBorder => Current.DangerBorder;
        internal static Color SuccessSurface => Current.SuccessSurface;
        internal static Color SuccessBorder => Current.SuccessBorder;
        internal static Color WechatSurface => Current.WechatSurface;
        internal static Color WechatBorder => Current.WechatBorder;
        internal static Color WecomSurface => Current.WecomSurface;
        internal static Color WecomBorder => Current.WecomBorder;
        internal static Color ActionSurface => Current.ActionSurface;
    }

    internal static class RoundedGeometry
    {
        internal static GraphicsPath Create(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return path;
            }

            var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
            if (diameter <= 2)
            {
                path.AddRectangle(rectangle);
                path.CloseFigure();
                return path;
            }

            var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class GradientPanel : Panel
    {
        internal GradientPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            TopColor = UiPalette.CanvasTop;
            BottomColor = UiPalette.Canvas;
            GradientAngle = 105F;
        }

        internal Color TopColor { get; set; }
        internal Color BottomColor { get; set; }
        internal float GradientAngle { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
            {
                return;
            }

            using (var brush = new LinearGradientBrush(ClientRectangle, TopColor, BottomColor, GradientAngle))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }

    internal class RoundedPanel : Panel
    {
        private int _cornerRadius = 16;
        private Color _borderColor = UiPalette.Border;
        private int _borderThickness = 1;

        internal RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = UiPalette.Surface;
        }

        internal int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                UpdateRoundedRegion();
                Invalidate();
            }
        }

        internal Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                Invalidate();
            }
        }

        internal int BorderThickness
        {
            get => _borderThickness;
            set
            {
                _borderThickness = Math.Max(0, value);
                Invalidate();
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRoundedRegion();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rectangle = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (var path = RoundedGeometry.Create(rectangle, CornerRadius))
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderThickness <= 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var inset = BorderThickness / 2F;
            var rectangle = new RectangleF(
                inset,
                inset,
                Math.Max(0, Width - BorderThickness - 1),
                Math.Max(0, Height - BorderThickness - 1));
            using (var path = RoundedGeometry.Create(Rectangle.Round(rectangle), CornerRadius))
            using (var pen = new Pen(BorderColor, BorderThickness))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using (var path = RoundedGeometry.Create(new Rectangle(0, 0, Width, Height), CornerRadius))
            {
                var previous = Region;
                Region = new Region(path);
                previous?.Dispose();
            }
        }
    }

    internal class PremiumButton : Button
    {
        private bool _hovered;
        private bool _pressed;

        internal PremiumButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            CornerRadius = 11;
            BackColor = UiPalette.Gold;
            HoverBackColor = UiPalette.GoldHover;
            PressedBackColor = UiPalette.GoldPressed;
            DisabledBackColor = UiPalette.SurfaceRaised;
            ForeColor = UiPalette.Canvas;
            HoverForeColor = UiPalette.Canvas;
            PressedForeColor = UiPalette.Canvas;
            DisabledForeColor = UiPalette.MutedDark;
            BorderColor = Color.Transparent;
            BorderThickness = 0;
        }

        internal int CornerRadius { get; set; }
        internal Color HoverBackColor { get; set; }
        internal Color PressedBackColor { get; set; }
        internal Color DisabledBackColor { get; set; }
        internal Color HoverForeColor { get; set; }
        internal Color PressedForeColor { get; set; }
        internal Color DisabledForeColor { get; set; }
        internal Color BorderColor { get; set; }
        internal int BorderThickness { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fillColor = !Enabled
                ? DisabledBackColor
                : _pressed
                    ? PressedBackColor
                    : _hovered ? HoverBackColor : BackColor;
            var textColor = !Enabled
                ? DisabledForeColor
                : _pressed
                    ? PressedForeColor
                    : _hovered ? HoverForeColor : ForeColor;
            var rectangle = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (var path = RoundedGeometry.Create(rectangle, CornerRadius))
            using (var brush = new SolidBrush(fillColor))
            {
                pevent.Graphics.FillPath(brush, path);
                if (BorderThickness > 0)
                {
                    using (var pen = new Pen(BorderColor, BorderThickness))
                    {
                        pevent.Graphics.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class PremiumCheckBox : CheckBox
    {
        private bool _hovered;

        internal PremiumCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            AutoSize = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            ForeColor = UiPalette.Ivory;
            AccentColor = UiPalette.Gold;
            BoxBorderColor = UiPalette.Border;
            Height = 25;
        }

        internal Color AccentColor { get; set; }
        internal Color BoxBorderColor { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var boxSize = 18;
            var box = new Rectangle(1, Math.Max(1, (Height - boxSize) / 2), boxSize, boxSize);
            var border = Enabled && _hovered ? AccentColor : BoxBorderColor;
            var fill = Checked ? AccentColor : UiPalette.SurfaceRaised;
            if (!Enabled)
            {
                border = UiPalette.BorderSoft;
                fill = UiPalette.Surface;
            }

            using (var path = RoundedGeometry.Create(box, 5))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (Checked)
            {
                using (var pen = new Pen(UiPalette.Canvas, 1.8F)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    e.Graphics.DrawLines(pen, new[]
                    {
                        new Point(box.Left + 4, box.Top + 9),
                        new Point(box.Left + 8, box.Top + 13),
                        new Point(box.Left + 15, box.Top + 5)
                    });
                }
            }

            var textRectangle = new Rectangle(29, 0, Math.Max(0, Width - 29), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRectangle,
                Enabled ? ForeColor : UiPalette.MutedDark,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class PremiumRadioButton : RadioButton
    {
        private bool _hovered;

        internal PremiumRadioButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            AutoSize = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            ForeColor = UiPalette.Ivory;
            AccentColor = UiPalette.Gold;
            RingColor = UiPalette.Border;
            Height = 25;
        }

        internal Color AccentColor { get; set; }
        internal Color RingColor { get; set; }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var diameter = 18;
            var circle = new Rectangle(1, Math.Max(1, (Height - diameter) / 2), diameter, diameter);
            var ring = Enabled && (_hovered || Checked) ? AccentColor : RingColor;
            if (!Enabled)
            {
                ring = UiPalette.BorderSoft;
            }

            using (var brush = new SolidBrush(UiPalette.SurfaceRaised))
            using (var pen = new Pen(ring, Checked ? 1.5F : 1F))
            {
                e.Graphics.FillEllipse(brush, circle);
                e.Graphics.DrawEllipse(pen, circle);
            }

            if (Checked)
            {
                var dot = new Rectangle(circle.Left + 5, circle.Top + 5, circle.Width - 10, circle.Height - 10);
                using (var brush = new SolidBrush(AccentColor))
                {
                    e.Graphics.FillEllipse(brush, dot);
                }
            }

            var textRectangle = new Rectangle(29, 0, Math.Max(0, Width - 29), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRectangle,
                Enabled ? ForeColor : UiPalette.MutedDark,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal static class ThemeStyler
    {
        internal static void Apply(Control root, AppTheme previousTheme, AppTheme nextTheme)
        {
            if (root == null || previousTheme == nextTheme)
            {
                return;
            }

            var previous = UiPalette.For(previousTheme);
            var next = UiPalette.For(nextTheme);
            ApplyControl(root, previous, next);

            var form = root as PremiumForm ?? root.FindForm() as PremiumForm;
            form?.RefreshNativeWindowStyle();
            root.Invalidate(true);
            root.Update();
        }

        private static void ApplyControl(Control control, UiThemeColors previous, UiThemeColors next)
        {
            control.BackColor = Map(control.BackColor, previous, next);
            control.ForeColor = Map(control.ForeColor, previous, next);

            var gradientPanel = control as GradientPanel;
            if (gradientPanel != null)
            {
                gradientPanel.TopColor = Map(gradientPanel.TopColor, previous, next);
                gradientPanel.BottomColor = Map(gradientPanel.BottomColor, previous, next);
            }

            var roundedPanel = control as RoundedPanel;
            if (roundedPanel != null)
            {
                roundedPanel.BorderColor = Map(roundedPanel.BorderColor, previous, next);
            }

            var button = control as PremiumButton;
            if (button != null)
            {
                button.HoverBackColor = Map(button.HoverBackColor, previous, next);
                button.PressedBackColor = Map(button.PressedBackColor, previous, next);
                button.DisabledBackColor = Map(button.DisabledBackColor, previous, next);
                button.HoverForeColor = Map(button.HoverForeColor, previous, next);
                button.PressedForeColor = Map(button.PressedForeColor, previous, next);
                button.DisabledForeColor = Map(button.DisabledForeColor, previous, next);
                button.BorderColor = Map(button.BorderColor, previous, next);
            }

            var checkBox = control as PremiumCheckBox;
            if (checkBox != null)
            {
                checkBox.AccentColor = Map(checkBox.AccentColor, previous, next);
                checkBox.BoxBorderColor = Map(checkBox.BoxBorderColor, previous, next);
            }

            var radioButton = control as PremiumRadioButton;
            if (radioButton != null)
            {
                radioButton.AccentColor = Map(radioButton.AccentColor, previous, next);
                radioButton.RingColor = Map(radioButton.RingColor, previous, next);
            }

            var linkLabel = control as LinkLabel;
            if (linkLabel != null)
            {
                linkLabel.LinkColor = Map(linkLabel.LinkColor, previous, next);
                linkLabel.ActiveLinkColor = Map(linkLabel.ActiveLinkColor, previous, next);
                linkLabel.VisitedLinkColor = Map(linkLabel.VisitedLinkColor, previous, next);
            }

            foreach (Control child in control.Controls)
            {
                ApplyControl(child, previous, next);
            }
        }

        private static Color Map(Color color, UiThemeColors previous, UiThemeColors next)
        {
            if (Same(color, previous.Canvas)) return next.Canvas;
            if (Same(color, previous.CanvasTop)) return next.CanvasTop;
            if (Same(color, previous.Surface)) return next.Surface;
            if (Same(color, previous.SurfaceRaised)) return next.SurfaceRaised;
            if (Same(color, previous.SurfaceHover)) return next.SurfaceHover;
            if (Same(color, previous.Border)) return next.Border;
            if (Same(color, previous.BorderSoft)) return next.BorderSoft;
            if (Same(color, previous.Ivory)) return next.Ivory;
            if (Same(color, previous.Muted)) return next.Muted;
            if (Same(color, previous.MutedDark)) return next.MutedDark;
            if (Same(color, previous.Gold)) return next.Gold;
            if (Same(color, previous.GoldHover)) return next.GoldHover;
            if (Same(color, previous.GoldPressed)) return next.GoldPressed;
            if (Same(color, previous.Green)) return next.Green;
            if (Same(color, previous.Blue)) return next.Blue;
            if (Same(color, previous.Warning)) return next.Warning;
            if (Same(color, previous.Danger)) return next.Danger;
            if (Same(color, previous.DangerHover)) return next.DangerHover;
            if (Same(color, previous.DangerPressed)) return next.DangerPressed;
            if (Same(color, previous.GoldSurface)) return next.GoldSurface;
            if (Same(color, previous.GoldBorder)) return next.GoldBorder;
            if (Same(color, previous.DangerSurface)) return next.DangerSurface;
            if (Same(color, previous.DangerBorder)) return next.DangerBorder;
            if (Same(color, previous.SuccessSurface)) return next.SuccessSurface;
            if (Same(color, previous.SuccessBorder)) return next.SuccessBorder;
            if (Same(color, previous.WechatSurface)) return next.WechatSurface;
            if (Same(color, previous.WechatBorder)) return next.WechatBorder;
            if (Same(color, previous.WecomSurface)) return next.WecomSurface;
            if (Same(color, previous.WecomBorder)) return next.WecomBorder;
            if (Same(color, previous.ActionSurface)) return next.ActionSurface;
            return color;
        }

        private static bool Same(Color left, Color right)
        {
            return left.ToArgb() == right.ToArgb();
        }
    }

    internal sealed class ThemeToggleButton : PremiumButton
    {
        internal ThemeToggleButton()
        {
            BackColor = UiPalette.SurfaceRaised;
            HoverBackColor = UiPalette.SurfaceHover;
            PressedBackColor = UiPalette.Border;
            DisabledBackColor = UiPalette.Surface;
            BorderColor = UiPalette.Border;
            BorderThickness = 1;
            CornerRadius = 13;
            Font = new Font("Microsoft YaHei UI", 8F);
            ForeColor = UiPalette.Muted;
            HoverForeColor = UiPalette.Ivory;
            PressedForeColor = UiPalette.Ivory;
            AccessibleName = "切换日间或夜间主题";
            UpdateLabel();
        }

        protected override void OnClick(EventArgs e)
        {
            var previous = ThemeManager.Current;
            var next = previous == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            ThemeManager.SetTheme(next);
            var form = FindForm();
            if (form != null)
            {
                ThemeStyler.Apply(form, previous, next);
            }
            UpdateLabel();
            base.OnClick(e);
        }

        private void UpdateLabel()
        {
            Text = ThemeManager.Current == AppTheme.Dark ? "☾  夜间" : "☀  日间";
        }
    }

    public class PremiumForm : Form
    {
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmWindowCornerPreference = 33;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;
        private const int DwmTextColor = 36;

        internal PremiumForm()
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;
            BackColor = UiPalette.Canvas;
            Font = new Font("Microsoft YaHei UI", 9F);
            Padding = Padding.Empty;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MaximizeBox = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshNativeWindowStyle();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshNativeWindowStyle();
        }

        internal void RefreshNativeWindowStyle()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || Handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var enabled = ThemeManager.Current == AppTheme.Dark ? 1 : 0;
                var result = DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
                }

                var roundedCorners = 2;
                DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref roundedCorners, sizeof(int));

                var borderColor = ToColorRef(UiPalette.Border);
                var captionColor = ToColorRef(UiPalette.Canvas);
                var textColor = ToColorRef(UiPalette.Ivory);
                DwmSetWindowAttribute(Handle, DwmBorderColor, ref borderColor, sizeof(int));
                DwmSetWindowAttribute(Handle, DwmCaptionColor, ref captionColor, sizeof(int));
                DwmSetWindowAttribute(Handle, DwmTextColor, ref textColor, sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
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
