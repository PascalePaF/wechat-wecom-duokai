using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WechatDuokai.Presentation
{
    // Embedded vector artwork only: no downloads, timers, scripts or additional persistent files.
    internal static class SeasonalPresentation
    {
        private static ImageSource _classicLogo;
        private static ImageSource _classicIcon;
        private static readonly DrawingImage DayLogo = CreateLogo(false);
        private static readonly DrawingImage NightLogo = CreateLogo(true);
        private static readonly BitmapSource DayIcon = Rasterize(DayLogo);
        private static readonly BitmapSource NightIcon = Rasterize(NightLogo);
        private static readonly DrawingImage DayPreview = CreatePreview(false);
        private static readonly DrawingImage NightPreview = CreatePreview(true);

        internal static void ApplyResources(Application app)
        {
            var holiday = ThemeManager.IsHalloween;
            var night = ThemeManager.IsDark;
            if (_classicLogo == null)
            {
                var assembly = typeof(SeasonalPresentation).Assembly.GetName().Name;
                _classicLogo = new BitmapImage(new Uri("pack://application:,,,/" + assembly + ";component/Resources/WeichuangLogo.png"));
                _classicLogo.Freeze();
                _classicIcon = BitmapFrame.Create(new Uri("pack://application:,,,/" + assembly + ";component/Resources/AppIcon.ico"));
                _classicIcon.Freeze();
            }
            app.Resources["ProductLogoImage"] = holiday ? (night ? NightLogo : DayLogo) : _classicLogo;
            app.Resources["ProductWindowIcon"] = holiday ? (night ? NightIcon : DayIcon) : _classicIcon;
            app.Resources["ProductLogoSize"] = holiday ? 64d : 58d;
            app.Resources["HalloweenIsNight"] = night;
            app.Resources["ThemePreviewHalloween"] = NightPreview;
            app.Resources["ThemePreviewHalloweenLight"] = DayPreview;
            app.Resources["SeasonalButtonGlyph"] = CreateGhostGlyph(ColorOf(app, "AccentTextBrush"));
            app.Resources["SeasonalGhostImage"] = CreateGhostGlyph(night ? C("#E7DDEB") : C("#8E70A9"));
            app.Resources["HalloweenHarvestImage"] = CreateHarvest(night, (ImageSource)app.FindResource("HalloweenPumpkinImage"));
            app.Resources["SeasonalPrimaryPadding"] = new Thickness(holiday ? 10 : 18, 0, holiday ? 10 : 18, 0);
            app.Resources["SeasonalCardThickness"] = new Thickness(holiday ? 1.25 : 1);
            app.Resources["SeasonalIconThickness"] = new Thickness(holiday ? 1 : 0);
            app.Resources["SeasonalIconBorderBrush"] = holiday ? B(night ? "#776080" : "#D8B797") : Brushes.Transparent;
            app.Resources["SeasonalControlRadius"] = new CornerRadius(holiday ? 10 : 12);
            app.Resources["SeasonalCardRadius"] = new CornerRadius(holiday ? 20 : 18);
            app.Resources["SeasonalRaisedRadius"] = new CornerRadius(holiday ? 16 : 14);

            if (!holiday)
            {
                app.Resources["WindowCanvasBrush"] = app.Resources["WindowBackgroundBrush"];
                app.Resources["CardBackgroundBrush"] = app.Resources["SurfaceBrush"];
                app.Resources["RaisedCardBackgroundBrush"] = app.Resources["SurfaceRaisedBrush"];
                app.Resources["CardBorderBrush"] = app.Resources["BorderBrush"];
                app.Resources["PrimaryFillBrush"] = app.Resources["AccentBrush"];
                app.Resources["SeasonalTraceBrush"] = Brushes.Transparent;
                return;
            }

            app.Resources["WindowCanvasBrush"] = Gradient(night ? "#21172E" : "#FFF1DE", night ? "#15121E" : "#FFF9F0", true);
            app.Resources["CardBackgroundBrush"] = Gradient(night ? "#292033" : "#FFFEF9", night ? "#211B2A" : "#FFF5E8", true);
            app.Resources["RaisedCardBackgroundBrush"] = Gradient(night ? "#32273C" : "#FFF9EF", night ? "#292230" : "#FFF5EA", true);
            var rim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            rim.GradientStops.Add(new GradientStop(C(night ? "#A77A58" : "#C88F54"), 0));
            rim.GradientStops.Add(new GradientStop(C(night ? "#564562" : "#E5CFAE"), 0.38));
            rim.GradientStops.Add(new GradientStop(C(night ? "#75618C" : "#B59AC2"), 0.78));
            rim.GradientStops.Add(new GradientStop(C(night ? "#98704F" : "#D4AB75"), 1));
            rim.Freeze();
            app.Resources["CardBorderBrush"] = rim;
            app.Resources["PrimaryFillBrush"] = Gradient(night ? "#E1AB73" : "#AC5D26", night ? "#CD8E53" : "#9D4D1D", true);
            var candy = new DrawingGroup();
            using (var dc = candy.Open())
            {
                dc.DrawRectangle(B(night ? "#B78B61" : "#D3AD78"), null, new Rect(0, 0, 8, 8));
                dc.DrawGeometry(B(night ? "#766080" : "#B098C1"), null, G("M 0,0 L 4,0 L 8,4 L 8,8 Z"));
            }
            candy.Freeze();
            var stripes = new DrawingBrush(candy) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 8, 8), Stretch = Stretch.Fill };
            stripes.Freeze();
            app.Resources["SeasonalTraceBrush"] = stripes;
        }

        internal static DrawingImage CreateLogo(bool night)
        {
            return Draw(dc =>
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 72, 72));
                dc.DrawRoundedRectangle(B(night ? "#34273F" : "#FFF4DE"), P(night ? "#DEAA6D" : "#AD6026", 3.6), new Rect(7, 19, 38, 33), 9, 9);
                dc.DrawRoundedRectangle(B(night ? "#362C4B" : "#F2EAF8"), P(night ? "#A78AC4" : "#8C6AAF", 3.6), new Rect(28, 36, 35, 30), 8, 8);
                dc.DrawGeometry(B(night ? "#7B618F" : "#644575"), null, G("M 4,21 L 23,1 L 32,16 L 46,24 Q 25,28 4,21 Z"));
                dc.DrawGeometry(B("#D39A57"), null, G("M 12,17 L 33,16 L 36,20 L 9,21 Z"));
                dc.DrawEllipse(B(night ? "#E4B176" : "#B56629"), null, new Point(17, 34), 2.3, 2.3);
                dc.DrawEllipse(B(night ? "#E4B176" : "#B56629"), null, new Point(27, 34), 2.3, 2.3);
                dc.DrawGeometry(B(night ? "#F0E7E8" : "#FFFFFF"), P(night ? "#C3B0CF" : "#B69ACA", 0.8), G("M 36,57 L 36,49 C 36,39 54,39 54,49 L 55,60 L 50,57 L 46,61 L 42,57 L 37,60 Z"));
                dc.DrawEllipse(B("#644C71"), null, new Point(42, 49), 1.5, 2);
                dc.DrawEllipse(B("#644C71"), null, new Point(49, 49), 1.5, 2);
                Star(dc, new Point(60, 15), 5, B(night ? "#E1B779" : "#C98A43"));
                Star(dc, new Point(12, 62), 3, B(night ? "#A593BC" : "#9A7CAE"));
            });
        }

        internal static DrawingImage CreateGhostGlyph(Color color)
        {
            return Draw(dc =>
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 18, 18));
                dc.DrawGeometry(new SolidColorBrush(color), null,
                    G("M 3,16 L 3,8 C 3,0 15,0 15,8 L 15,16 L 12,14 L 9,17 L 6,14 Z M 6,7 L 6,10 L 8,10 L 8,7 Z M 10,7 L 10,10 L 12,10 L 12,7 Z"));
            });
        }

        private static DrawingImage CreatePreview(bool night)
        {
            return Draw(dc =>
            {
                dc.DrawRoundedRectangle(B(night ? "#20172D" : "#FFF2DF"), null, new Rect(0, 0, 112, 66), 9, 9);
                dc.DrawImage(night ? NightLogo : DayLogo, new Rect(5, 3, 20, 20));
                dc.DrawRoundedRectangle(B(night ? "#33253E" : "#FFFCF4"), P(night ? "#8E6F89" : "#CC9E6B", 0.7), new Rect(8, 27, 67, 31), 5, 5);
                dc.DrawRoundedRectangle(B(night ? "#33253E" : "#FFFCF4"), P(night ? "#8E6F89" : "#CC9E6B", 0.7), new Rect(81, 27, 23, 31), 5, 5);
                dc.DrawRoundedRectangle(B(night ? "#D9A065" : "#AB5C25"), null, new Rect(46, 35, 23, 11), 4, 4);
                dc.DrawImage(CreateGhostGlyph(C(night ? "#352018" : "#FFFFFF")), new Rect(49, 36, 9, 9));
                dc.DrawRectangle(B(night ? "#9D8BAF" : "#D9BB95"), null, new Rect(16, 38, 23, 3));
                dc.DrawRectangle(B(night ? "#78688B" : "#DDCDBA"), null, new Rect(16, 48, 48, 3));
                dc.DrawEllipse(B(night ? "#E0C18D" : "#E4AE65"), null, new Point(91, 12), 7, 7);
                Bat(dc, new Point(61, 15), 10, B(night ? "#B296CA" : "#9D7DAD"));
            });
        }

        private static DrawingImage CreateHarvest(bool night, ImageSource pumpkin)
        {
            return Draw(dc =>
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 82, 76));
                dc.DrawEllipse(null, P(night ? "#9A7CAD" : "#B39AC5", 1), new Point(42, 39), 31, 30);
                dc.DrawImage(pumpkin, new Rect(16, 27, 49, 46));
                dc.DrawImage(CreateGhostGlyph(C(night ? "#E6DDEB" : "#8B70A3")), new Rect(53, 2, 22, 27));
                dc.DrawGeometry(null, P(night ? "#A8997A" : "#A58E68", 1.5), G("M 17,62 C 1,57 3,41 12,46 C 18,49 7,54 9,44 M 65,62 C 80,54 77,45 72,47"));
                Star(dc, new Point(13, 17), 6, B(night ? "#D5AE78" : "#C28E47"));
                Star(dc, new Point(32, 6), 3, B(night ? "#A48CB9" : "#A48AB8"));
            });
        }

        private static BitmapSource Rasterize(ImageSource source)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(source, new Rect(0, 0, 72, 72));
            var bitmap = new RenderTargetBitmap(72, 72, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static DrawingImage Draw(Action<DrawingContext> render)
        {
            var group = new DrawingGroup();
            using (var dc = group.Open()) render(dc);
            group.Freeze();
            var result = new DrawingImage(group);
            result.Freeze();
            return result;
        }

        internal static Geometry G(string path) { var geometry = Geometry.Parse(path); geometry.Freeze(); return geometry; }
        internal static Color C(string value) => (Color)ColorConverter.ConvertFromString(value);
        internal static SolidColorBrush B(string value) { var brush = new SolidColorBrush(C(value)); brush.Freeze(); return brush; }
        internal static Pen P(string color, double width) { var pen = new Pen(B(color), width); pen.Freeze(); return pen; }
        private static Color ColorOf(Application app, string key) => ((SolidColorBrush)app.Resources[key]).Color;
        private static Brush Gradient(string start, string end, bool vertical)
        {
            var brush = new LinearGradientBrush(C(start), C(end), vertical ? 90 : 0);
            brush.Freeze();
            return brush;
        }

        internal static void Star(DrawingContext dc, Point center, double radius, Brush brush)
        {
            dc.DrawGeometry(brush, null, G(string.Format(CultureInfo.InvariantCulture,
                "M {0},{1} L {2},{3} L {4},{5} L {6},{7} Z", center.X, center.Y - radius,
                center.X + radius * 0.35, center.Y, center.X, center.Y + radius, center.X - radius * 0.35, center.Y)));
        }

        internal static void Bat(DrawingContext dc, Point center, double width, Brush brush)
        {
            dc.PushTransform(new TranslateTransform(center.X - width / 2, center.Y - width / 4));
            dc.PushTransform(new ScaleTransform(width / 40, width / 40));
            dc.DrawGeometry(brush, null, G("M 0,0 Q 9,3 12,8 L 17,7 L 18,3 L 21,6 L 24,3 L 25,8 Q 31,7 40,0 Q 37,11 32,14 Q 27,9 24,17 L 20,21 L 17,17 Q 14,9 9,14 Q 3,11 0,0 Z"));
            dc.Pop(); dc.Pop();
        }
    }

    public enum HalloweenSceneKind { Main, Installer, Dialog, Picker }

    public sealed class HalloweenScene : FrameworkElement
    {
        public static readonly DependencyProperty IsNightProperty = DependencyProperty.Register(nameof(IsNight), typeof(bool),
            typeof(HalloweenScene), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool IsNight { get => (bool)GetValue(IsNightProperty); set => SetValue(IsNightProperty, value); }
        public HalloweenSceneKind Scene { get; set; }

        public HalloweenScene()
        {
            IsHitTestVisible = false;
            Focusable = false;
            SetResourceReference(VisibilityProperty, "HalloweenDecorationVisibility");
            SetResourceReference(IsNightProperty, "HalloweenIsNight");
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var width = ActualWidth; var height = ActualHeight;
            if (width < 100 || height < 100) return;
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            var ink = SeasonalPresentation.B(IsNight ? "#8D739C" : "#B095B8");
            var gold = SeasonalPresentation.B(IsNight ? "#D8AC70" : "#CE944A");
            var headerHeight = Scene == HalloweenSceneKind.Main ? 96d : Scene == HalloweenSceneKind.Installer ? 108d : 78d;

            // Draw exclusively in the header's free space and the outer gutters, never over controls.
            if ((Scene == HalloweenSceneKind.Main || Scene == HalloweenSceneKind.Installer) && width >= 680)
            {
                var center = Scene == HalloweenSceneKind.Installer ? width - 275 : Math.Max(350, width - 330);
                var moon = new Point(center, 43);
                var glow = new RadialGradientBrush(SeasonalPresentation.C(IsNight ? "#26D6A572" : "#20DEAC68"), Colors.Transparent);
                dc.DrawEllipse(glow, null, moon, 58, 37);
                dc.DrawEllipse(gold, null, moon, 20, 20);
                if (IsNight) dc.DrawEllipse(SeasonalPresentation.B("#1C1725"), null, new Point(moon.X + 8, moon.Y - 6), 17, 17);
                else
                {
                    dc.DrawEllipse(SeasonalPresentation.B("#F4CA89"), null, new Point(moon.X - 6, moon.Y - 4), 2.5, 2.5);
                    dc.DrawEllipse(SeasonalPresentation.B("#F4CA89"), null, new Point(moon.X + 7, moon.Y + 8), 3.5, 3.5);
                }
                SeasonalPresentation.Bat(dc, new Point(center - 76, 30), 30, ink);
                if (Scene == HalloweenSceneKind.Main) SeasonalPresentation.Bat(dc, new Point(center + 62, 63), 22, ink);
                SeasonalPresentation.Star(dc, new Point(center - 34, 71), 4, gold);
                SeasonalPresentation.Star(dc, new Point(center + 49, 20), 3, gold);
            }

            var edge = SeasonalPresentation.P(IsNight ? "#66506F" : "#D8B68D", 0.8);
            dc.PushOpacity(IsNight ? 0.65 : 0.55);
            var webSize = Scene == HalloweenSceneKind.Main || Scene == HalloweenSceneKind.Installer ? 28 : 18;
            DrawWeb(dc, new Point(7, headerHeight + 7), webSize, false, edge);
            DrawWeb(dc, new Point(width - 7, headerHeight + 7), webSize, true, edge);
            if (Scene == HalloweenSceneKind.Main || Scene == HalloweenSceneKind.Installer)
            {
                var ghostY = Math.Min(height - 100, headerHeight + 111);
                dc.DrawLine(edge, new Point(14, headerHeight + 26), new Point(14, ghostY));
                dc.DrawImage((ImageSource)FindResource("SeasonalGhostImage"), new Rect(6, ghostY, 17, 20));
                SeasonalPresentation.Star(dc, new Point(width - 14, height * 0.63), 5, gold);
                SeasonalPresentation.Star(dc, new Point(14, height - 87), 3, ink);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, SeasonalPresentation.P(IsNight ? "#5B425D" : "#E7CAAA", 0.8), new Rect(4, 4, width - 8, height - 8), 14, 14);
            dc.Pop();
        }

        private static void DrawWeb(DrawingContext dc, Point origin, double size, bool mirror, Pen pen)
        {
            dc.PushTransform(new TranslateTransform(origin.X, origin.Y));
            dc.PushTransform(new ScaleTransform(mirror ? -1 : 1, 1));
            dc.DrawLine(pen, new Point(0, 0), new Point(size, 0));
            dc.DrawLine(pen, new Point(0, 0), new Point(size * 0.72, size * 0.72));
            dc.DrawLine(pen, new Point(0, 0), new Point(0, size));
            for (var index = 1; index <= 3; index++)
            {
                var radius = size * index / 3;
                dc.DrawGeometry(null, pen, SeasonalPresentation.G(string.Format(CultureInfo.InvariantCulture,
                    "M 0,{0} Q {1},{1} {2},{2} Q {1},{1} {0},0", radius, radius * 0.32, radius * 0.72)));
            }
            dc.Pop(); dc.Pop();
        }
    }
}
