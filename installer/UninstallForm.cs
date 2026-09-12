using System;
using System.Drawing;
using System.Windows.Forms;
using WechatDuokai.UI;

namespace WechatDuokai.Installer
{
    internal sealed class UninstallForm : PremiumForm
    {
        private readonly CleanupLocations _locations;
        private readonly RadioButton _keepSourceOption;
        private readonly RadioButton _deleteSourceOption;
        private readonly CheckBox _confirmSourceDeletion;
        private readonly RoundedPanel _deleteSourceCard;
        private readonly PremiumButton _cleanupButton;
        private readonly Label _pathLabel;

        internal UninstallForm()
        {
            _locations = InstallerEngine.GetCleanupLocations();

            Text = "完全卸载 · " + InstallerEngine.ProductName;
            ClientSize = new Size(670, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiPalette.Border;
            Icon = Icon.ExtractAssociatedIcon(InstallerEngine.InstallerExecutablePath);
            HeaderDragHeight = 98;
            DragExclusionRight = 64;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = UiPalette.Canvas
            };
            header.Controls.Add(CreateLabel("DUOKAI  /  CLEANUP", new Point(25, 12), new Size(220, 16),
                new Font("Segoe UI", 7.5F, FontStyle.Bold), UiPalette.Gold));
            header.Controls.Add(CreateLabel("完全卸载与清理", new Point(23, 33), new Size(350, 34),
                new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), UiPalette.Ivory));
            header.Controls.Add(CreateLabel("所有目标均经过项目标记和路径校验，避免误删其他目录", new Point(25, 71), new Size(480, 20),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted));
            var closeButton = CreateChromeButton("×", new Point(622, 15));
            closeButton.Click += (sender, args) => Close();
            header.Controls.Add(closeButton);
            Controls.Add(header);

            var card = new RoundedPanel
            {
                Location = new Point(22, 118),
                Size = new Size(626, 318),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.Border,
                CornerRadius = 17
            };
            card.Controls.Add(CreateLabel("选择清理范围", new Point(22, 17), new Size(220, 25),
                new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), UiPalette.Ivory));

            var keepSourceCard = new RoundedPanel
            {
                Location = new Point(20, 53),
                Size = new Size(586, 78),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.Border,
                CornerRadius = 13
            };
            _keepSourceOption = CreateRadioOption("删除程序与全部发布包，保留源码", new Point(17, 12), true, UiPalette.Ivory);
            _keepSourceOption.CheckedChanged += OptionChanged;
            keepSourceCard.Controls.Add(_keepSourceOption);
            keepSourceCard.Controls.Add(CreateLabel("删除已安装程序、快捷方式、安装包和绿色版；源码目录继续保留。",
                new Point(39, 42), new Size(520, 20), new Font("Microsoft YaHei UI", 8F), UiPalette.Muted));
            card.Controls.Add(keepSourceCard);

            _deleteSourceCard = new RoundedPanel
            {
                Location = new Point(20, 142),
                Size = new Size(586, 105),
                BackColor = Color.FromArgb(34, 27, 29),
                BorderColor = Color.FromArgb(65, 42, 45),
                CornerRadius = 13
            };
            _deleteSourceOption = CreateRadioOption("全部删除，包括源码与全部发布包", new Point(17, 11), false, UiPalette.Danger);
            _deleteSourceOption.Enabled = InstallerEngine.ValidateSourceRoot(_locations.SourceRoot);
            _deleteSourceOption.CheckedChanged += OptionChanged;
            _deleteSourceCard.Controls.Add(_deleteSourceOption);
            _deleteSourceCard.Controls.Add(CreateLabel(
                _deleteSourceOption.Enabled
                    ? "永久删除安装程序、绿色版、发布包和已确认的源码目录。此操作不可撤销。"
                    : "未发现带安全标记的源码目录，因此该选项已禁用。",
                new Point(39, 41), new Size(520, 19), new Font("Microsoft YaHei UI", 8F),
                _deleteSourceOption.Enabled ? Color.FromArgb(194, 126, 126) : UiPalette.MutedDark));
            _confirmSourceDeletion = new CheckBox
            {
                AutoSize = true,
                BackColor = _deleteSourceCard.BackColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold),
                ForeColor = UiPalette.Danger,
                Location = new Point(39, 72),
                Text = "我确认永久删除源码目录",
                UseVisualStyleBackColor = false,
                Visible = false
            };
            _confirmSourceDeletion.CheckedChanged += OptionChanged;
            _deleteSourceCard.Controls.Add(_confirmSourceDeletion);
            card.Controls.Add(_deleteSourceCard);

            _pathLabel = CreateLabel(
                "源码：" + (_locations.SourceRoot ?? "未关联") + "\r\n发布包：" +
                (_locations.ArtifactRoot ?? _locations.PackageRoot ?? "未关联"),
                new Point(22, 264), new Size(580, 42), new Font("Microsoft YaHei UI", 7.8F), UiPalette.MutedDark);
            _pathLabel.AutoEllipsis = true;
            card.Controls.Add(_pathLabel);
            Controls.Add(card);

            var cancelButton = new PremiumButton
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(442, 458),
                Size = new Size(92, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Surface,
                HoverBackColor = UiPalette.SurfaceHover,
                PressedBackColor = UiPalette.Border,
                BorderColor = UiPalette.Border,
                BorderThickness = 1,
                ForeColor = UiPalette.Ivory
            };
            Controls.Add(cancelButton);

            _cleanupButton = new PremiumButton
            {
                Text = "开始清理",
                Location = new Point(544, 458),
                Size = new Size(104, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Gold,
                HoverBackColor = UiPalette.GoldHover,
                PressedBackColor = UiPalette.GoldPressed,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = UiPalette.Canvas
            };
            _cleanupButton.Click += CleanupButton_Click;
            Controls.Add(_cleanupButton);

            Controls.Add(CreateLabel("●  安全路径校验已启用", new Point(27, 467), new Size(300, 22),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Green, ContentAlignment.MiddleLeft));

            AcceptButton = _cleanupButton;
            CancelButton = cancelButton;
        }

        private void OptionChanged(object sender, EventArgs e)
        {
            _confirmSourceDeletion.Visible = _deleteSourceOption.Checked;
            _cleanupButton.Enabled = !_deleteSourceOption.Checked || _confirmSourceDeletion.Checked;
            _cleanupButton.BackColor = _deleteSourceOption.Checked ? UiPalette.Danger : UiPalette.Gold;
            _cleanupButton.HoverBackColor = _deleteSourceOption.Checked
                ? Color.FromArgb(238, 127, 127)
                : UiPalette.GoldHover;
            _cleanupButton.PressedBackColor = _deleteSourceOption.Checked
                ? Color.FromArgb(194, 77, 77)
                : UiPalette.GoldPressed;
            _deleteSourceCard.BorderColor = _deleteSourceOption.Checked
                ? Color.FromArgb(116, 57, 62)
                : Color.FromArgb(65, 42, 45);
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

        private static RadioButton CreateRadioOption(string text, Point location, bool isChecked, Color color)
        {
            return new RadioButton
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                Checked = isChecked,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = color,
                Location = location,
                Text = text,
                UseVisualStyleBackColor = false
            };
        }

        private static PremiumButton CreateChromeButton(string text, Point location)
        {
            return new PremiumButton
            {
                BackColor = UiPalette.Canvas,
                HoverBackColor = Color.FromArgb(71, 37, 39),
                PressedBackColor = Color.FromArgb(91, 43, 46),
                DisabledBackColor = UiPalette.Canvas,
                Font = new Font("Segoe UI", 12F),
                ForeColor = UiPalette.Muted,
                Location = location,
                Size = new Size(32, 32),
                CornerRadius = 10,
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
