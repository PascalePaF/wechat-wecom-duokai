using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WechatDuokai.Installer
{
    internal sealed class UninstallForm : Form
    {
        private readonly CleanupLocations _locations;
        private readonly RadioButton _keepSourceOption;
        private readonly RadioButton _deleteSourceOption;
        private readonly CheckBox _confirmSourceDeletion;
        private readonly Button _cleanupButton;
        private readonly Label _pathLabel;

        public UninstallForm()
        {
            _locations = InstallerEngine.GetCleanupLocations();

            Text = "完全卸载 · " + InstallerEngine.ProductName;
            ClientSize = new Size(620, 452);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(242, 245, 248);
            Font = new Font("微软雅黑", 9F);
            Icon = Icon.ExtractAssociatedIcon(InstallerEngine.InstallerExecutablePath);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(35, 43, 55)
            };
            header.Controls.Add(new Label
            {
                Text = "完全卸载与清理",
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 13)
            });
            header.Controls.Add(new Label
            {
                Text = "所有删除目标均经过项目标记校验，避免误删其他目录",
                ForeColor = Color.FromArgb(190, 200, 212),
                AutoSize = true,
                Location = new Point(27, 51)
            });
            Controls.Add(header);

            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(20, 100),
                Size = new Size(580, 258)
            };
            card.Controls.Add(new Label
            {
                Text = "请选择清理范围",
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(22, 18)
            });

            _keepSourceOption = new RadioButton
            {
                Text = "删除程序与全部发布包，保留源码",
                Font = new Font("微软雅黑", 10F, FontStyle.Bold),
                Checked = true,
                AutoSize = true,
                Location = new Point(24, 55)
            };
            _keepSourceOption.CheckedChanged += OptionChanged;
            card.Controls.Add(_keepSourceOption);
            card.Controls.Add(new Label
            {
                Text = "删除已安装程序、开始菜单/桌面快捷方式、安装包和绿色版；源码目录保留。",
                ForeColor = Color.FromArgb(100, 108, 118),
                AutoSize = true,
                Location = new Point(47, 82)
            });

            _deleteSourceOption = new RadioButton
            {
                Text = "全部删除，包括源码与全部发布包",
                Font = new Font("微软雅黑", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 48, 48),
                AutoSize = true,
                Location = new Point(24, 117),
                Enabled = InstallerEngine.ValidateSourceRoot(_locations.SourceRoot)
            };
            _deleteSourceOption.CheckedChanged += OptionChanged;
            card.Controls.Add(_deleteSourceOption);
            card.Controls.Add(new Label
            {
                Text = _deleteSourceOption.Enabled
                    ? "永久删除安装程序、绿色版、发布包以及下方已确认的源码目录。此操作不可撤销。"
                    : "未发现带安全标记的源码目录，因此该选项已禁用。",
                ForeColor = _deleteSourceOption.Enabled ? Color.FromArgb(160, 65, 65) : Color.FromArgb(120, 128, 138),
                AutoSize = true,
                Location = new Point(47, 144)
            });

            _confirmSourceDeletion = new CheckBox
            {
                Text = "我确认永久删除源码目录",
                ForeColor = Color.FromArgb(180, 48, 48),
                AutoSize = true,
                Location = new Point(47, 174),
                Visible = false
            };
            _confirmSourceDeletion.CheckedChanged += OptionChanged;
            card.Controls.Add(_confirmSourceDeletion);

            _pathLabel = new Label
            {
                Text = "源码：" + (_locations.SourceRoot ?? "未关联") + "\r\n发布包：" +
                       (_locations.ArtifactRoot ?? _locations.PackageRoot ?? "未关联"),
                ForeColor = Color.FromArgb(80, 89, 100),
                AutoEllipsis = true,
                Location = new Point(24, 207),
                Size = new Size(530, 42)
            };
            card.Controls.Add(_pathLabel);
            Controls.Add(card);

            var cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(413, 382),
                Size = new Size(82, 38)
            };
            Controls.Add(cancelButton);

            _cleanupButton = new Button
            {
                Text = "开始清理",
                BackColor = Color.FromArgb(45, 105, 197),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(506, 382),
                Size = new Size(94, 38)
            };
            _cleanupButton.FlatAppearance.BorderSize = 0;
            _cleanupButton.Click += CleanupButton_Click;
            Controls.Add(_cleanupButton);

            AcceptButton = _cleanupButton;
            CancelButton = cancelButton;
        }

        private void OptionChanged(object sender, EventArgs e)
        {
            _confirmSourceDeletion.Visible = _deleteSourceOption.Checked;
            _cleanupButton.Enabled = !_deleteSourceOption.Checked || _confirmSourceDeletion.Checked;
            _cleanupButton.BackColor = _deleteSourceOption.Checked
                ? Color.FromArgb(180, 48, 48)
                : Color.FromArgb(45, 105, 197);
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
    }
}
