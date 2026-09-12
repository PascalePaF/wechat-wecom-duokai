using System;
using System.Drawing;
using System.Windows.Forms;
using WechatDuokai.UI;

namespace shuangkai
{
    partial class Home
    {
        private System.ComponentModel.IContainer components;
        private Panel headerPanel;
        private PremiumButton closeButton;
        private PremiumButton minimizeButton;
        private RoundedPanel versionPanel;
        private Label versionLabel;
        private Label subtitleLabel;
        private Label titleLabel;
        private Label brandLabel;
        private RoundedPanel wechatPanel;
        private PremiumButton wechatStartButton;
        private Label wechatPathLabel;
        private Label wechatCountLabel;
        private Label wechatNameLabel;
        private RoundedPanel wechatIconPanel;
        private PictureBox wechatIcon;
        private RoundedPanel wecomPanel;
        private PremiumButton wecomStartButton;
        private Label wecomPathLabel;
        private Label wecomCountLabel;
        private Label wecomNameLabel;
        private RoundedPanel wecomIconPanel;
        private PictureBox wecomIcon;
        private RoundedPanel quantityPanel;
        private Label targetHintLabel;
        private Label savedLabel;
        private RoundedPanel targetEntryPanel;
        private PremiumButton increaseButton;
        private PremiumButton decreaseButton;
        private TextBox targetCountInput;
        private Label targetLabel;
        private Label targetEyebrowLabel;
        private Panel footerPanel;
        private Panel footerLine;
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

            headerPanel = new Panel
            {
                BackColor = UiPalette.Canvas,
                Dock = DockStyle.Top,
                Height = 88
            };
            brandLabel = CreateLabel("DUOKAI  /  WINDOWS", new Point(25, 10), new Size(160, 16),
                new Font("Segoe UI", 7.5F, FontStyle.Bold), UiPalette.Gold);
            titleLabel = CreateLabel("微信 · 企业微信多开助手", new Point(23, 29), new Size(320, 32),
                new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), UiPalette.Ivory);
            subtitleLabel = CreateLabel("精确识别运行实例，只补开缺少的窗口", new Point(25, 62), new Size(300, 18),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted);

            versionPanel = new RoundedPanel
            {
                Location = new Point(612, 18),
                Size = new Size(76, 27),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.Border,
                CornerRadius = 12
            };
            versionLabel = CreateLabel("V1.0.1", Point.Empty, versionPanel.Size,
                new Font("Segoe UI", 8F, FontStyle.Bold), UiPalette.Gold, ContentAlignment.MiddleCenter);
            versionLabel.Dock = DockStyle.Fill;
            versionPanel.Controls.Add(versionLabel);

            minimizeButton = CreateChromeButton("−", new Point(704, 15), UiPalette.SurfaceRaised, UiPalette.SurfaceHover);
            minimizeButton.Click += minimizeButton_Click;
            closeButton = CreateChromeButton("×", new Point(742, 15), Color.FromArgb(71, 37, 39), Color.FromArgb(91, 43, 46));
            closeButton.Click += closeButton_Click;
            headerPanel.Controls.AddRange(new Control[]
            {
                brandLabel, titleLabel, subtitleLabel, versionPanel, minimizeButton, closeButton
            });

            CreateApplicationCard(
                new Point(24, 103),
                "微信",
                global::duokai.Properties.Resources.WeChat,
                Color.FromArgb(34, 38, 39),
                Color.FromArgb(48, 60, 52),
                wechatStartButton_Click,
                wechatIcon_Click,
                out wechatPanel,
                out wechatStartButton,
                out wechatPathLabel,
                out wechatCountLabel,
                out wechatNameLabel,
                out wechatIconPanel,
                out wechatIcon);

            CreateApplicationCard(
                new Point(24, 231),
                "企业微信",
                global::duokai.Properties.Resources.WXWork,
                Color.FromArgb(31, 36, 43),
                Color.FromArgb(43, 57, 72),
                wecomStartButton_Click,
                wecomIcon_Click,
                out wecomPanel,
                out wecomStartButton,
                out wecomPathLabel,
                out wecomCountLabel,
                out wecomNameLabel,
                out wecomIconPanel,
                out wecomIcon);

            quantityPanel = new RoundedPanel
            {
                Location = new Point(570, 103),
                Size = new Size(196, 244),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.Border,
                CornerRadius = 17
            };
            targetEyebrowLabel = CreateLabel("TARGET INSTANCES", new Point(18, 15), new Size(160, 17),
                new Font("Segoe UI", 7.5F, FontStyle.Bold), UiPalette.Gold, ContentAlignment.MiddleCenter);
            targetLabel = CreateLabel("目标窗口数", new Point(18, 36), new Size(160, 28),
                new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), UiPalette.Ivory, ContentAlignment.MiddleCenter);
            targetEntryPanel = new RoundedPanel
            {
                Location = new Point(16, 79),
                Size = new Size(164, 62),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.Border,
                CornerRadius = 14
            };
            decreaseButton = CreateCounterButton("−", new Point(10, 10));
            decreaseButton.Click += decreaseButton_Click;
            increaseButton = CreateCounterButton("+", new Point(114, 10));
            increaseButton.Click += increaseButton_Click;
            targetCountInput = new TextBox
            {
                BackColor = UiPalette.SurfaceRaised,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = UiPalette.Ivory,
                Location = new Point(52, 10),
                MaxLength = 2,
                Size = new Size(60, 43),
                Text = "2",
                TextAlign = HorizontalAlignment.Center
            };
            targetCountInput.TextChanged += targetCountInput_TextChanged;
            targetCountInput.KeyPress += targetCountInput_KeyPress;
            targetCountInput.Leave += targetCountInput_Leave;
            targetEntryPanel.Controls.AddRange(new Control[] { decreaseButton, targetCountInput, increaseButton });
            savedLabel = CreateLabel("●  设置自动保存", new Point(30, 157), new Size(136, 23),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Green, ContentAlignment.MiddleCenter);
            targetHintLabel = CreateLabel("再次点击时，仅补足\r\n已关闭的实例", new Point(20, 190), new Size(156, 39),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark, ContentAlignment.TopCenter);
            quantityPanel.Controls.AddRange(new Control[]
            {
                targetEyebrowLabel, targetLabel, targetEntryPanel, savedLabel, targetHintLabel
            });

            footerPanel = new Panel
            {
                BackColor = UiPalette.Canvas,
                Dock = DockStyle.Bottom,
                Height = 68
            };
            footerLine = new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(23, 1),
                Size = new Size(742, 1)
            };
            statusDotLabel = CreateLabel("●", new Point(25, 27), new Size(12, 15),
                new Font("Segoe UI", 8F), UiPalette.Green);
            statusLabel = CreateLabel("就绪 · 选择目标窗口数，然后启动或补开", new Point(43, 21), new Size(635, 27),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted, ContentAlignment.MiddleLeft);
            statusLabel.AutoEllipsis = true;
            sourceLink = new LinkLabel
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8F),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = UiPalette.Gold,
                ActiveLinkColor = UiPalette.GoldHover,
                VisitedLinkColor = UiPalette.Gold,
                Location = new Point(699, 25),
                Text = "GitHub"
            };
            sourceLink.LinkClicked += sourceLink_LinkClicked;
            footerPanel.Controls.AddRange(new Control[] { footerLine, statusDotLabel, statusLabel, sourceLink });

            statusTimer = new Timer(components) { Interval = 2000 };
            statusTimer.Tick += statusTimer_Tick;
            pathToolTip = new ToolTip(components)
            {
                AutoPopDelay = 8000,
                InitialDelay = 350,
                ReshowDelay = 100
            };

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = UiPalette.Border;
            ClientSize = new Size(790, 458);
            Controls.AddRange(new Control[] { footerPanel, quantityPanel, wecomPanel, wechatPanel, headerPanel });
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
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

        private static PremiumButton CreateChromeButton(string text, Point location, Color hover, Color pressed)
        {
            return new PremiumButton
            {
                BackColor = UiPalette.Canvas,
                HoverBackColor = hover,
                PressedBackColor = pressed,
                DisabledBackColor = UiPalette.Canvas,
                Font = new Font("Segoe UI", 12F),
                ForeColor = UiPalette.Muted,
                Location = location,
                Size = new Size(32, 32),
                CornerRadius = 10,
                Text = text
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
                Font = new Font("Segoe UI", 16F),
                ForeColor = UiPalette.Ivory,
                Location = location,
                Size = new Size(40, 42),
                Text = text
            };
        }

        private static void CreateApplicationCard(
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
            out Label nameLabel,
            out RoundedPanel iconPanel,
            out PictureBox icon)
        {
            panel = new RoundedPanel
            {
                Location = location,
                Size = new Size(530, 116),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.Border,
                CornerRadius = 17
            };
            iconPanel = new RoundedPanel
            {
                Location = new Point(18, 23),
                Size = new Size(70, 70),
                BackColor = iconBackground,
                BorderColor = iconBorder,
                CornerRadius = 16
            };
            icon = new PictureBox
            {
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Image = image,
                Location = new Point(9, 9),
                Size = new Size(52, 52),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            icon.Click += iconHandler;
            iconPanel.Controls.Add(icon);
            nameLabel = CreateLabel(displayName, new Point(102, 22), new Size(160, 25),
                new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), UiPalette.Ivory);
            countLabel = CreateLabel("当前未运行", new Point(104, 51), new Size(250, 20),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted);
            pathLabel = CreateLabel("正在检测安装路径…", new Point(104, 78), new Size(276, 19),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark);
            pathLabel.AutoEllipsis = true;
            startButton = new PremiumButton
            {
                BackColor = UiPalette.Gold,
                HoverBackColor = UiPalette.GoldHover,
                PressedBackColor = UiPalette.GoldPressed,
                DisabledBackColor = UiPalette.SurfaceRaised,
                CornerRadius = 12,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = UiPalette.Canvas,
                Location = new Point(396, 35),
                Size = new Size(113, 46),
                Text = "启动 / 补开"
            };
            startButton.Click += startHandler;
            panel.Controls.AddRange(new Control[] { iconPanel, nameLabel, countLabel, pathLabel, startButton });
        }
    }
}
