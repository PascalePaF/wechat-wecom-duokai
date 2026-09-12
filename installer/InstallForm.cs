using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WechatDuokai.Installer
{
    internal sealed class InstallForm : Form
    {
        private readonly CheckBox _desktopShortcut;
        private readonly CheckBox _runAfterInstall;
        private readonly Button _installButton;
        private readonly Label _statusLabel;

        public InstallForm()
        {
            Text = InstallerEngine.ProductName + " V" + InstallerEngine.Version + " 安装程序";
            ClientSize = new Size(568, 368);
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
                Height = 82,
                BackColor = Color.FromArgb(35, 43, 55)
            };
            header.Controls.Add(new Label
            {
                Text = "微信 · 企业微信多开助手",
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 13)
            });
            header.Controls.Add(new Label
            {
                Text = "V1.0.0  ·  当前用户安装，无需管理员权限",
                ForeColor = Color.FromArgb(190, 200, 212),
                Font = new Font("微软雅黑", 9F),
                AutoSize = true,
                Location = new Point(27, 51)
            });
            Controls.Add(header);

            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(20, 102),
                Size = new Size(528, 178)
            };
            card.Controls.Add(new Label
            {
                Text = "安装位置",
                Font = new Font("微软雅黑", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 18)
            });
            card.Controls.Add(new TextBox
            {
                Text = InstallerEngine.InstallDirectory,
                ReadOnly = true,
                BackColor = Color.FromArgb(247, 248, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(22, 48),
                Size = new Size(482, 25)
            });

            _desktopShortcut = new CheckBox
            {
                Text = "创建桌面快捷方式",
                Checked = true,
                AutoSize = true,
                Location = new Point(23, 91)
            };
            card.Controls.Add(_desktopShortcut);

            _runAfterInstall = new CheckBox
            {
                Text = "安装完成后启动程序",
                Checked = true,
                AutoSize = true,
                Location = new Point(23, 122)
            };
            card.Controls.Add(_runAfterInstall);

            card.Controls.Add(new Label
            {
                Text = "不会安装微信或企业微信，也不会修改它们的程序文件。",
                ForeColor = Color.FromArgb(105, 113, 122),
                AutoSize = true,
                Location = new Point(216, 124)
            });
            Controls.Add(card);

            _statusLabel = new Label
            {
                Text = "准备安装。",
                ForeColor = Color.FromArgb(80, 89, 100),
                AutoEllipsis = true,
                Location = new Point(22, 294),
                Size = new Size(342, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_statusLabel);

            var cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(374, 294),
                Size = new Size(78, 36)
            };
            Controls.Add(cancelButton);

            _installButton = new Button
            {
                Text = "立即安装",
                BackColor = Color.FromArgb(45, 105, 197),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(461, 294),
                Size = new Size(87, 36)
            };
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.Click += InstallButton_Click;
            Controls.Add(_installButton);

            AcceptButton = _installButton;
            CancelButton = cancelButton;
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            _installButton.Enabled = false;
            _statusLabel.Text = "正在写入程序文件和快捷方式…";
            Cursor = Cursors.WaitCursor;

            try
            {
                var result = InstallerEngine.Install(_desktopShortcut.Checked);
                _statusLabel.Text = "安装完成。";

                if (_runAfterInstall.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = InstallerEngine.InstalledExecutable,
                        WorkingDirectory = result.InstallDirectory,
                        UseShellExecute = true
                    });
                }

                MessageBox.Show("安装成功。你可以从桌面或开始菜单启动程序。",
                    "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "安装失败。请关闭正在运行的旧版本后重试。";
                MessageBox.Show(ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _installButton.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
