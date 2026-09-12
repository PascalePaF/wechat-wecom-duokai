using System;
using System.Drawing;
using System.Windows.Forms;
using WechatDuokai.UI;

namespace WechatDuokai.Installer
{
    internal sealed class UninstallForm : PremiumForm
    {
        private readonly CleanupLocations _locations;
        private readonly PremiumRadioButton _keepSourceOption;
        private readonly PremiumRadioButton _deleteSourceOption;
        private readonly PremiumCheckBox _confirmSourceDeletion;
        private readonly RoundedPanel _deleteSourceCard;
        private readonly PremiumButton _cleanupButton;
        private readonly Label _pathLabel;

        internal UninstallForm()
        {
            _locations = InstallerEngine.GetCleanupLocations();

            Text = "完全卸载 · " + InstallerEngine.ProductName;
            ClientSize = new Size(704, 524);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiPalette.Canvas;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Icon.ExtractAssociatedIcon(InstallerEngine.InstallerExecutablePath);

            var background = new GradientPanel
            {
                Dock = DockStyle.Fill,
                TopColor = UiPalette.CanvasTop,
                BottomColor = UiPalette.Canvas,
                GradientAngle = 112F
            };
            Controls.Add(background);

            var brandTile = new RoundedPanel
            {
                Location = new Point(24, 17),
                Size = new Size(48, 48),
                BackColor = UiPalette.DangerSurface,
                BorderColor = UiPalette.DangerBorder,
                CornerRadius = 14
            };
            var brandMark = CreateLabel("卸", Point.Empty, brandTile.Size,
                new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), UiPalette.Danger,
                ContentAlignment.MiddleCenter);
            brandMark.Dock = DockStyle.Fill;
            brandTile.Controls.Add(brandMark);
            background.Controls.Add(brandTile);

            background.Controls.Add(CreateLabel("完全卸载与清理", new Point(87, 16), new Size(320, 30),
                new Font("Microsoft YaHei UI", 16F, FontStyle.Bold), UiPalette.Ivory));
            background.Controls.Add(CreateLabel("只处理经过项目标记和路径校验的内容", new Point(88, 48), new Size(380, 19),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted));

            var safetyPill = new RoundedPanel
            {
                Location = new Point(457, 27),
                Size = new Size(133, 28),
                BackColor = UiPalette.SuccessSurface,
                BorderColor = UiPalette.SuccessBorder,
                CornerRadius = 13
            };
            var safetyText = CreateLabel("✓ 安全校验已启用", Point.Empty, safetyPill.Size,
                new Font("Microsoft YaHei UI", 7.8F), UiPalette.Green, ContentAlignment.MiddleCenter);
            safetyText.Dock = DockStyle.Fill;
            safetyPill.Controls.Add(safetyText);
            background.Controls.Add(safetyPill);

            var themeToggle = new ThemeToggleButton
            {
                Location = new Point(598, 27),
                Size = new Size(82, 28)
            };
            background.Controls.Add(themeToggle);

            background.Controls.Add(new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(24, 84),
                Size = new Size(656, 1)
            });

            var card = new RoundedPanel
            {
                Location = new Point(24, 101),
                Size = new Size(656, 342),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 18
            };
            card.Controls.Add(CreateLabel("选择清理范围", new Point(20, 17), new Size(220, 26),
                new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), UiPalette.Ivory));
            card.Controls.Add(CreateLabel("源码保留是默认且更安全的选择", new Point(21, 43), new Size(340, 18),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark));

            var keepSourceCard = new RoundedPanel
            {
                Location = new Point(20, 70),
                Size = new Size(616, 76),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.GoldBorder,
                CornerRadius = 13
            };
            card.Controls.Add(keepSourceCard);

            _deleteSourceCard = new RoundedPanel
            {
                Location = new Point(20, 156),
                Size = new Size(616, 104),
                BackColor = UiPalette.DangerSurface,
                BorderColor = UiPalette.DangerBorder,
                CornerRadius = 13
            };
            card.Controls.Add(_deleteSourceCard);

            _keepSourceOption = CreateRadioOption("删除程序与发布包，保留源码", new Point(17, 11),
                new Size(360, 25), true, UiPalette.Gold, UiPalette.Ivory);
            _keepSourceOption.CheckedChanged += OptionChanged;
            keepSourceCard.Controls.Add(_keepSourceOption);
            var keepDescription = CreateLabel("移除已安装程序、快捷方式、安装包和绿色版，源码目录继续保留。",
                new Point(46, 42), new Size(550, 19), new Font("Microsoft YaHei UI", 8F), UiPalette.Muted);
            keepSourceCard.Controls.Add(keepDescription);

            _deleteSourceOption = CreateRadioOption("全部删除，包括源码与所有发布内容", new Point(17, 10),
                new Size(430, 25), false, UiPalette.Danger, UiPalette.Danger);
            _deleteSourceOption.Enabled = InstallerEngine.ValidateSourceRoot(_locations.SourceRoot);
            _deleteSourceOption.CheckedChanged += OptionChanged;
            _deleteSourceCard.Controls.Add(_deleteSourceOption);
            var deleteDescription = CreateLabel(
                _deleteSourceOption.Enabled
                    ? "永久删除已确认的源码目录、安装程序、绿色版和全部发布包。"
                    : "未找到带安全标记的源码目录，因此此选项已禁用。",
                new Point(46, 41), new Size(550, 19), new Font("Microsoft YaHei UI", 8F),
                _deleteSourceOption.Enabled ? UiPalette.Danger : UiPalette.MutedDark);
            _deleteSourceCard.Controls.Add(deleteDescription);

            _confirmSourceDeletion = new PremiumCheckBox
            {
                AccentColor = UiPalette.Danger,
                BoxBorderColor = UiPalette.DangerBorder,
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
                ForeColor = UiPalette.Danger,
                Location = new Point(46, 68),
                Size = new Size(300, 25),
                Text = "我确认永久删除源码目录",
                Visible = false
            };
            _confirmSourceDeletion.CheckedChanged += OptionChanged;
            _deleteSourceCard.Controls.Add(_confirmSourceDeletion);

            var pathsPanel = new RoundedPanel
            {
                Location = new Point(20, 273),
                Size = new Size(616, 52),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 11
            };
            _pathLabel = CreateLabel(
                "源码：" + (_locations.SourceRoot ?? "未关联") + "\r\n发布包：" +
                (_locations.ArtifactRoot ?? _locations.PackageRoot ?? "未关联"),
                new Point(12, 7), new Size(590, 38), new Font("Microsoft YaHei UI", 7.5F), UiPalette.MutedDark);
            _pathLabel.AutoEllipsis = true;
            pathsPanel.Controls.Add(_pathLabel);
            card.Controls.Add(pathsPanel);
            background.Controls.Add(card);

            background.Controls.Add(CreateLabel("●", new Point(26, 482), new Size(13, 18),
                new Font("Segoe UI", 8F), UiPalette.Green));
            background.Controls.Add(CreateLabel("删除前会再次确认；其他目录不会被处理", new Point(44, 474), new Size(360, 32),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted, ContentAlignment.MiddleLeft));

            var cancelButton = new PremiumButton
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(478, 468),
                Size = new Size(92, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Surface,
                HoverBackColor = UiPalette.SurfaceHover,
                PressedBackColor = UiPalette.Border,
                BorderColor = UiPalette.Border,
                BorderThickness = 1,
                ForeColor = UiPalette.Ivory,
                HoverForeColor = UiPalette.Ivory,
                PressedForeColor = UiPalette.Ivory
            };
            background.Controls.Add(cancelButton);

            _cleanupButton = new PremiumButton
            {
                Text = "开始清理",
                Location = new Point(580, 468),
                Size = new Size(100, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Gold,
                HoverBackColor = UiPalette.GoldHover,
                PressedBackColor = UiPalette.GoldPressed,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = UiPalette.Canvas
            };
            _cleanupButton.Click += CleanupButton_Click;
            background.Controls.Add(_cleanupButton);

            keepSourceCard.Click += (sender, args) => _keepSourceOption.Checked = true;
            keepDescription.Click += (sender, args) => _keepSourceOption.Checked = true;
            _deleteSourceCard.Click += (sender, args) =>
            {
                if (_deleteSourceOption.Enabled)
                {
                    _deleteSourceOption.Checked = true;
                }
            };
            deleteDescription.Click += (sender, args) =>
            {
                if (_deleteSourceOption.Enabled)
                {
                    _deleteSourceOption.Checked = true;
                }
            };

            AcceptButton = _cleanupButton;
            CancelButton = cancelButton;
        }

        private void OptionChanged(object sender, EventArgs e)
        {
            if (ReferenceEquals(sender, _keepSourceOption) && _keepSourceOption.Checked)
            {
                _deleteSourceOption.Checked = false;
            }
            else if (ReferenceEquals(sender, _deleteSourceOption) && _deleteSourceOption.Checked)
            {
                _keepSourceOption.Checked = false;
            }

            _confirmSourceDeletion.Visible = _deleteSourceOption.Checked;
            _cleanupButton.Enabled = !_deleteSourceOption.Checked || _confirmSourceDeletion.Checked;
            _cleanupButton.BackColor = _deleteSourceOption.Checked ? UiPalette.Danger : UiPalette.Gold;
            _cleanupButton.HoverBackColor = _deleteSourceOption.Checked
                ? UiPalette.DangerHover
                : UiPalette.GoldHover;
            _cleanupButton.PressedBackColor = _deleteSourceOption.Checked
                ? UiPalette.DangerPressed
                : UiPalette.GoldPressed;
            _deleteSourceCard.BorderColor = _deleteSourceOption.Checked
                ? UiPalette.Danger
                : UiPalette.DangerBorder;
        }

        private void CleanupButton_Click(object sender, EventArgs e)
        {
            var deleteSource = _deleteSourceOption.Checked;
            if (deleteSource && (!InstallerEngine.ValidateSourceRoot(_locations.SourceRoot) || !_confirmSourceDeletion.Checked))
            {
                MessageBox.Show("源码目录未通过安全校验，未执行删除。", "安全保护",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var details = deleteSource
                ? "即将永久删除源码目录：\r\n" + _locations.SourceRoot + "\r\n\r\n同时删除安装程序和全部发布包。"
                : "即将删除安装程序和全部发布包。\r\n源码目录会保留：\r\n" + (_locations.SourceRoot ?? "未关联");

            if (MessageBox.Show(details + "\r\n\r\n确定继续吗？", "最后确认",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                InstallerEngine.StartCleanup(_locations, deleteSource);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法启动清理程序：\r\n" + ex.Message,
                    "清理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static PremiumRadioButton CreateRadioOption(string text, Point location, Size size,
            bool isChecked, Color accentColor, Color textColor)
        {
            return new PremiumRadioButton
            {
                AccentColor = accentColor,
                Checked = isChecked,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = textColor,
                Location = location,
                Size = size,
                Text = text
            };
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
    }
}
