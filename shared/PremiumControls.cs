using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WechatDuokai.UI
{
    internal static class UiPalette
    {
        internal static readonly Color Canvas = Color.FromArgb(14, 15, 17);
        internal static readonly Color Surface = Color.FromArgb(24, 26, 30);
        internal static readonly Color SurfaceRaised = Color.FromArgb(30, 32, 37);
        internal static readonly Color SurfaceHover = Color.FromArgb(38, 40, 46);
        internal static readonly Color Border = Color.FromArgb(48, 50, 56);
        internal static readonly Color BorderSoft = Color.FromArgb(38, 40, 45);
        internal static readonly Color Ivory = Color.FromArgb(246, 242, 233);
        internal static readonly Color Muted = Color.FromArgb(150, 153, 160);
        internal static readonly Color MutedDark = Color.FromArgb(105, 108, 115);
        internal static readonly Color Gold = Color.FromArgb(211, 181, 121);
        internal static readonly Color GoldHover = Color.FromArgb(225, 198, 143);
        internal static readonly Color GoldPressed = Color.FromArgb(190, 156, 94);
        internal static readonly Color Green = Color.FromArgb(105, 206, 139);
        internal static readonly Color Blue = Color.FromArgb(111, 164, 244);
        internal static readonly Color Warning = Color.FromArgb(232, 166, 99);
        internal static readonly Color Danger = Color.FromArgb(225, 104, 104);
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
                     ControlStyles.UserPaint, true);
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
            DisabledForeColor = UiPalette.MutedDark;
            BorderColor = Color.Transparent;
            BorderThickness = 0;
        }

        internal int CornerRadius { get; set; }
        internal Color HoverBackColor { get; set; }
        internal Color PressedBackColor { get; set; }
        internal Color DisabledBackColor { get; set; }
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
                Enabled ? ForeColor : DisabledForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class PremiumForm : Form
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtCaption = 2;

        internal PremiumForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UiPalette.Canvas;
            Font = new Font("Microsoft YaHei UI", 9F);
            Padding = new Padding(1);
            HeaderDragHeight = 72;
            DragExclusionRight = 104;
            WindowCornerRadius = 18;
        }

        internal int HeaderDragHeight { get; set; }
        internal int DragExclusionRight { get; set; }
        internal int WindowCornerRadius { get; set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                return parameters;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRoundedWindow();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg != WmNcHitTest || (int)message.Result != HtClient || WindowState != FormWindowState.Normal)
            {
                return;
            }

            var packed = message.LParam.ToInt64();
            var screenPoint = new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff)));
            var clientPoint = PointToClient(screenPoint);
            if (clientPoint.Y >= 0 && clientPoint.Y < HeaderDragHeight &&
                clientPoint.X >= 0 && clientPoint.X < ClientSize.Width - DragExclusionRight)
            {
                message.Result = (IntPtr)HtCaption;
            }
        }

        private void ApplyRoundedWindow()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            var radius = Math.Max(2, WindowCornerRadius * 2);
            var handle = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius, radius);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var previous = Region;
            Region = Region.FromHrgn(handle);
            previous?.Dispose();
            DeleteObject(handle);
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }
}
