using System;
using System.Drawing;
using System.Windows.Forms;
using WechatDuokai.UI;

namespace shuangkai
{
    partial class Home
    {
        private System.ComponentModel.IContainer components;
        private GradientPanel backgroundPanel;
        private RoundedPanel applicationsPanel;
        private RoundedPanel wechatPanel;
        private PremiumButton wechatStartButton;
        private Label wechatPathLabel;
        private Label wechatCountLabel;
        private PictureBox wechatIcon;
        private RoundedPanel wecomPanel;
        private PremiumButton wecomStartButton;
        private Label wecomPathLabel;
        private Label wecomCountLabel;
        private PictureBox wecomIcon;
        private RoundedPanel quantityPanel;
        private TextBox targetCountInput;
        private PremiumButton increaseButton;
        private PremiumButton decreaseButton;
        private LinkLabel sourceLink;
        private Label statusDotLabel;
        private Label statusLabel;
        private Timer statusTimer;
        private ToolTip pathToolTip;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(Home));
            SuspendLayout();

            backgroundPanel = new GradientPanel
            {
                Dock = DockStyle.Fill,
                TopColor = UiPalette.CanvasTop,
                BottomColor = UiPalette.Canvas,
                GradientAngle = 112F
            };

            var brandTile = new RoundedPanel
            {
                Location = new Point(24, 18),
                Size = new Size(50, 50),
                BackColor = UiPalette.GoldSurface,
                BorderColor = UiPalette.GoldBorder,
                CornerRadius = 15
            };
            var brandMark = CreateLabel("双", Point.Empty, brandTile.Size,
                new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), UiPalette.Gold,
                ContentAlignment.MiddleCenter);
            brandMark.Dock = DockStyle.Fill;
            brandTile.Controls.Add(brandMark);

            var titleLabel = CreateLabel("微信 · 企业微信多开助手", new Point(90, 17), new Size(390, 31),
                new Font("Microsoft YaHei UI", 16.5F, FontStyle.Bold), UiPalette.Ivory);
            var subtitleLabel = CreateLabel("智能补足缺少的窗口，数量设置会自动记住", new Point(91, 49), new Size(430, 20),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted);

            var versionPanel = new RoundedPanel
            {
                Location = new Point(626, 27),
                Size = new Size(78, 28),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.GoldBorder,
                CornerRadius = 13
            };
            var versionLabel = CreateLabel("V1.0.1", Point.Empty, versionPanel.Size,
                new Font("Segoe UI", 8F, FontStyle.Bold), UiPalette.Gold, ContentAlignment.MiddleCenter);
            versionLabel.Dock = DockStyle.Fill;
            versionPanel.Controls.Add(versionLabel);

            var themeToggle = new ThemeToggleButton
            {
                Location = new Point(710, 27),
                Size = new Size(84, 28)
            };

            var headerLine = new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(24, 85),
                Size = new Size(772, 1)
            };

            applicationsPanel = new RoundedPanel
            {
                Location = new Point(24, 104),
                Size = new Size(552, 270),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 18
            };
            applicationsPanel.Controls.Add(CreateLabel("应用", new Point(18, 17), new Size(120, 25),
                new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), UiPalette.Ivory));
            applicationsPanel.Controls.Add(CreateLabel("选择客户端后，系统只会补开未达到数量的窗口", new Point(19, 43), new Size(430, 19),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark));

            CreateApplicationRow(
                new Point(16, 72),
                "微信",
                global::duokai.Properties.Resources.WeChat,
                UiPalette.WechatSurface,
                UiPalette.WechatBorder,
                wechatStartButton_Click,
                wechatIcon_Click,
                out wechatPanel,
                out wechatStartButton,
                out wechatPathLabel,
                out wechatCountLabel,
                out wechatIcon);
            applicationsPanel.Controls.Add(wechatPanel);

            CreateApplicationRow(
                new Point(16, 164),
                "企业微信",
                global::duokai.Properties.Resources.WXWork,
                UiPalette.WecomSurface,
                UiPalette.WecomBorder,
                wecomStartButton_Click,
                wecomIcon_Click,
                out wecomPanel,
                out wecomStartButton,
                out wecomPathLabel,
                out wecomCountLabel,
                out wecomIcon);
            applicationsPanel.Controls.Add(wecomPanel);

            quantityPanel = new RoundedPanel
            {
                Location = new Point(592, 104),
                Size = new Size(204, 270),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 18
            };
            quantityPanel.Controls.Add(CreateLabel("双开数量", new Point(18, 17), new Size(168, 27),
                new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), UiPalette.Ivory,
                ContentAlignment.MiddleCenter));
            quantityPanel.Controls.Add(CreateLabel("目标窗口数", new Point(18, 44), new Size(168, 18),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark, ContentAlignment.MiddleCenter));

            var targetEntryPanel = new RoundedPanel
            {
                Location = new Point(17, 79),
                Size = new Size(170, 66),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.Border,
                CornerRadius = 15
            };
            decreaseButton = CreateCounterButton("−", new Point(10, 12));
            decreaseButton.Click += decreaseButton_Click;
            increaseButton = CreateCounterButton("+", new Point(120, 12));
            increaseButton.Click += increaseButton_Click;
            targetCountInput = new TextBox
            {
                BackColor = UiPalette.SurfaceRaised,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 23F, FontStyle.Bold),
                ForeColor = UiPalette.Ivory,
                Location = new Point(51, 12),
                MaxLength = 2,
                Size = new Size(68, 42),
                Text = "2",
                TextAlign = HorizontalAlignment.Center
            };
            targetCountInput.TextChanged += targetCountInput_TextChanged;
            targetCountInput.KeyPress += targetCountInput_KeyPress;
            targetCountInput.Leave += targetCountInput_Leave;
            targetEntryPanel.Controls.AddRange(new Control[] { decreaseButton, targetCountInput, increaseButton });
            quantityPanel.Controls.Add(targetEntryPanel);

            quantityPanel.Controls.Add(CreateLabel("●  数量已自动保存", new Point(21, 158), new Size(162, 23),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Green, ContentAlignment.MiddleCenter));
            quantityPanel.Controls.Add(new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(20, 194),
                Size = new Size(164, 1)
            });
            quantityPanel.Controls.Add(CreateLabel("窗口误关后再次点击\r\n即可单独补开", new Point(20, 207), new Size(164, 39),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Muted, ContentAlignment.TopCenter));
            quantityPanel.Controls.Add(CreateLabel("支持 1–10 个窗口", new Point(20, 246), new Size(164, 17),
                new Font("Microsoft YaHei UI", 7.5F), UiPalette.MutedDark, ContentAlignment.TopCenter));

            var footerLine = new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(24, 397),
                Size = new Size(772, 1)
            };
            statusDotLabel = CreateLabel("●", new Point(26, 419), new Size(12, 15),
                new Font("Segoe UI", 8F), UiPalette.Green);
            statusLabel = CreateLabel("就绪 · 选择数量后启动，关闭的窗口可随时补开", new Point(44, 412), new Size(640, 28),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted, ContentAlignment.MiddleLeft);
            statusLabel.AutoEllipsis = true;
            sourceLink = new LinkLabel
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 8F),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = UiPalette.Gold,
                ActiveLinkColor = UiPalette.GoldHover,
                VisitedLinkColor = UiPalette.Gold,
                Location = new Point(748, 419),
                Text = "开源项目"
            };
            sourceLink.LinkClicked += sourceLink_LinkClicked;

            backgroundPanel.Controls.AddRange(new Control[]
            {
                brandTile, titleLabel, subtitleLabel, versionPanel, themeToggle, headerLine,
                applicationsPanel, quantityPanel, footerLine, statusDotLabel, statusLabel, sourceLink
            });

            statusTimer = new Timer(components) { Interval = 2000 };
            statusTimer.Tick += statusTimer_Tick;
            pathToolTip = new ToolTip(components)
            {
                AutoPopDelay = 8000,
                InitialDelay = 350,
                ReshowDelay = 100
            };
            pathToolTip.SetToolTip(themeToggle, "切换日间 / 夜间主题");

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = UiPalette.Canvas;
            ClientSize = new Size(820, 452);
            Controls.Add(backgroundPanel);
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = true;
            Name = "Home";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "微信 · 企业微信多开助手 V1.0.1";
            FormClosing += Home_FormClosing;
            Load += Home_Load;
            ResumeLayout(false);
        }

        private static Label CreateLabel(string text, Point location, Size size, Font font, Color color,
            ContentAlignment alignment = ContentAlignment.TopLeft)
        {
            return new Label
            {
                BackColor = Color.Transparent,
                Font = font,
                ForeColor = color,
                Location = location,
                Size = size,
                Text = text,
                TextAlign = alignment
            };
        }

        private static PremiumButton CreateCounterButton(string text, Point location)
        {
            return new PremiumButton
            {
                BackColor = UiPalette.SurfaceRaised,
                HoverBackColor = UiPalette.SurfaceHover,
                PressedBackColor = UiPalette.Border,
                DisabledBackColor = UiPalette.Surface,
                BorderColor = UiPalette.Border,
                BorderThickness = 1,
                CornerRadius = 10,
                Font = new Font("Segoe UI", 15F),
                ForeColor = UiPalette.Ivory,
                HoverForeColor = UiPalette.Ivory,
                PressedForeColor = UiPalette.Ivory,
                Location = location,
                Size = new Size(40, 42),
                Text = text
            };
        }

        private static void CreateApplicationRow(
            Point location,
            string displayName,
            Image image,
            Color iconBackground,
            Color iconBorder,
            EventHandler startHandler,
            EventHandler iconHandler,
            out RoundedPanel panel,
            out PremiumButton startButton,
            out Label pathLabel,
            out Label countLabel,
            out PictureBox icon)
        {
            panel = new RoundedPanel
            {
                Location = location,
                Size = new Size(520, 78),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 14
            };
            var iconPanel = new RoundedPanel
            {
                Location = new Point(13, 13),
                Size = new Size(52, 52),
                BackColor = iconBackground,
                BorderColor = iconBorder,
                CornerRadius = 13
            };
            icon = new PictureBox
            {
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Image = image,
                Location = new Point(7, 7),
                Size = new Size(38, 38),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            icon.Click += iconHandler;
            iconPanel.Controls.Add(icon);

            var nameLabel = CreateLabel(displayName, new Point(81, 9), new Size(130, 24),
                new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold), UiPalette.Ivory);
            countLabel = CreateLabel("当前未运行", new Point(82, 34), new Size(245, 19),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Muted);
            pathLabel = CreateLabel("正在检测安装路径…", new Point(82, 54), new Size(295, 17),
                new Font("Microsoft YaHei UI", 7.5F), UiPalette.MutedDark);
            pathLabel.AutoEllipsis = true;

            startButton = new PremiumButton
            {
                BackColor = UiPalette.ActionSurface,
                HoverBackColor = UiPalette.Gold,
                PressedBackColor = UiPalette.GoldPressed,
                DisabledBackColor = UiPalette.Surface,
                BorderColor = UiPalette.GoldBorder,
                BorderThickness = 1,
                CornerRadius = 11,
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                ForeColor = UiPalette.Gold,
                HoverForeColor = UiPalette.Canvas,
                PressedForeColor = UiPalette.Canvas,
                Location = new Point(393, 19),
                Size = new Size(111, 40),
                Text = "启动 / 补开"
            };
            startButton.Click += startHandler;
            panel.Controls.AddRange(new Control[] { iconPanel, nameLabel, countLabel, pathLabel, startButton });
        }
    }
}
