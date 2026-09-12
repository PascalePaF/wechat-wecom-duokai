using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WechatDuokai.UI;

namespace WechatDuokai.Installer
{
    internal sealed class InstallForm : PremiumForm
    {
        private readonly PremiumCheckBox _desktopShortcut;
        private readonly PremiumCheckBox _runAfterInstall;
        private readonly PremiumButton _browseButton;
        private readonly PremiumButton _installButton;
        private readonly TextBox _installPathTextBox;
        private readonly Label _statusDot;
        private readonly Label _statusLabel;
        private string _selectedInstallDirectory;

        internal InstallForm()
        {
            _selectedInstallDirectory = InstallerEngine.SuggestedInstallDirectory;

            Text = InstallerEngine.ProductName + " V" + InstallerEngine.Version + " 安装程序";
            ClientSize = new Size(684, 430);
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
                BackColor = UiPalette.GoldSurface,
                BorderColor = UiPalette.GoldBorder,
                CornerRadius = 14
            };
            var brandMark = CreateLabel("装", Point.Empty, brandTile.Size,
                new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), UiPalette.Gold,
                ContentAlignment.MiddleCenter);
            brandMark.Dock = DockStyle.Fill;
            brandTile.Controls.Add(brandMark);

            background.Controls.Add(brandTile);
            background.Controls.Add(CreateLabel("安装多开助手", new Point(87, 16), new Size(310, 30),
                new Font("Microsoft YaHei UI", 16F, FontStyle.Bold), UiPalette.Ivory));
            background.Controls.Add(CreateLabel("选择安装位置，一步完成当前用户安装 · 无需管理员权限", new Point(88, 48), new Size(390, 19),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted));

            var versionPill = new RoundedPanel
            {
                Location = new Point(488, 27),
                Size = new Size(82, 28),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.GoldBorder,
                CornerRadius = 13
            };
            var versionText = CreateLabel("V1.0.1", Point.Empty, versionPill.Size,
                new Font("Microsoft YaHei UI", 7.5F), UiPalette.Gold, ContentAlignment.MiddleCenter);
            versionText.Dock = DockStyle.Fill;
            versionPill.Controls.Add(versionText);
            background.Controls.Add(versionPill);

            var themeToggle = new ThemeToggleButton
            {
                Location = new Point(578, 27),
                Size = new Size(82, 28)
            };
            background.Controls.Add(themeToggle);

            background.Controls.Add(new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(24, 84),
                Size = new Size(636, 1)
            });

            var card = new RoundedPanel
            {
                Location = new Point(24, 101),
                Size = new Size(636, 250),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.BorderSoft,
                CornerRadius = 18
            };
            card.Controls.Add(CreateLabel("安装位置", new Point(20, 17), new Size(180, 25),
                new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold), UiPalette.Ivory));
            card.Controls.Add(CreateLabel("请选择一个专用文件夹", new Point(21, 43), new Size(240, 18),
                new Font("Microsoft YaHei UI", 8F), UiPalette.MutedDark));

            var pathPanel = new RoundedPanel
            {
                Location = new Point(20, 68),
                Size = new Size(596, 52),
                BackColor = UiPalette.SurfaceRaised,
                BorderColor = UiPalette.Border,
                CornerRadius = 12
            };
            _installPathTextBox = new TextBox
            {
                BackColor = UiPalette.SurfaceRaised,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = UiPalette.Ivory,
                Location = new Point(14, 17),
                ReadOnly = true,
                Size = new Size(472, 20),
                TabStop = false,
                Text = _selectedInstallDirectory
            };
            _browseButton = new PremiumButton
            {
                BackColor = UiPalette.ActionSurface,
                HoverBackColor = UiPalette.Gold,
                PressedBackColor = UiPalette.GoldPressed,
                BorderColor = UiPalette.GoldBorder,
                BorderThickness = 1,
                CornerRadius = 9,
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                ForeColor = UiPalette.Gold,
                HoverForeColor = UiPalette.Canvas,
                PressedForeColor = UiPalette.Canvas,
                Location = new Point(495, 8),
                Size = new Size(92, 36),
                Text = "选择文件夹"
            };
            _browseButton.Click += BrowseButton_Click;
            pathPanel.Controls.Add(_installPathTextBox);
            pathPanel.Controls.Add(_browseButton);
            card.Controls.Add(pathPanel);

            card.Controls.Add(CreateLabel("为保护已有文件，不能安装到含有其他内容的普通文件夹。",
                new Point(21, 128), new Size(570, 19), new Font("Microsoft YaHei UI", 7.8F), UiPalette.MutedDark));
            card.Controls.Add(new Panel
            {
                BackColor = UiPalette.BorderSoft,
                Location = new Point(20, 156),
                Size = new Size(596, 1)
            });

            _desktopShortcut = CreateOption("创建桌面快捷方式", new Point(21, 171), new Size(220, 26), true);
            _runAfterInstall = CreateOption("安装完成后启动程序", new Point(276, 171), new Size(230, 26), true);
            card.Controls.Add(_desktopShortcut);
            card.Controls.Add(_runAfterInstall);
            card.Controls.Add(CreateLabel("✓  仅安装本助手，不会下载、替换或修改微信与企业微信文件",
                new Point(21, 214), new Size(570, 20), new Font("Microsoft YaHei UI", 8F), UiPalette.Green));
            background.Controls.Add(card);

            _statusDot = CreateLabel("●", new Point(26, 388), new Size(13, 18),
                new Font("Segoe UI", 8F), UiPalette.Green);
            _statusLabel = CreateLabel("准备安装", new Point(44, 380), new Size(315, 32),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted, ContentAlignment.MiddleLeft);
            _statusLabel.AutoEllipsis = true;
            background.Controls.Add(_statusDot);
            background.Controls.Add(_statusLabel);

            var cancelButton = new PremiumButton
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(458, 374),
                Size = new Size(90, 40),
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

            _installButton = new PremiumButton
            {
                Text = "立即安装",
                Location = new Point(558, 374),
                Size = new Size(102, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Gold,
                HoverBackColor = UiPalette.GoldHover,
                PressedBackColor = UiPalette.GoldPressed,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = UiPalette.Canvas
            };
            _installButton.Click += InstallButton_Click;
            background.Controls.Add(_installButton);

            AcceptButton = _installButton;
            CancelButton = cancelButton;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "请选择或新建微信多开助手的专用安装文件夹",
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = FindNearestExistingDirectory(_selectedInstallDirectory),
                ShowNewFolderButton = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return;
                }

                _selectedInstallDirectory = Path.GetFullPath(dialog.SelectedPath);
                _installPathTextBox.Text = _selectedInstallDirectory;

                string error;
                if (InstallerEngine.ValidateInstallTarget(_selectedInstallDirectory, out error))
                {
                    SetStatus("安装位置已选择", UiPalette.Green);
                }
                else
                {
                    SetStatus(error, UiPalette.Warning);
                }
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            string validationError;
            if (!InstallerEngine.ValidateInstallTarget(_selectedInstallDirectory, out validationError))
            {
                SetStatus(validationError, UiPalette.Warning);
                MessageBox.Show(validationError, "请选择其他文件夹", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetInteractiveState(false);
            SetStatus("正在写入程序文件和快捷方式…", UiPalette.Gold);
            Cursor = Cursors.WaitCursor;

            try
            {
                var result = InstallerEngine.Install(_selectedInstallDirectory, _desktopShortcut.Checked);
                SetStatus("安装完成", UiPalette.Green);

                if (_runAfterInstall.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = result.ExecutablePath,
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
                SetStatus("安装失败，请关闭正在运行的旧版本后重试", UiPalette.Danger);
                MessageBox.Show(ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetInteractiveState(true);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void SetInteractiveState(bool enabled)
        {
            _installButton.Enabled = enabled;
            _browseButton.Enabled = enabled;
            _desktopShortcut.Enabled = enabled;
            _runAfterInstall.Enabled = enabled;
        }

        private void SetStatus(string text, Color color)
        {
            _statusDot.ForeColor = color;
            _statusLabel.ForeColor = color;
            _statusLabel.Text = text;
        }

        private static string FindNearestExistingDirectory(string path)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(path));
                while (directory != null && !directory.Exists)
                {
                    directory = directory.Parent;
                }
                return directory?.FullName ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            catch (Exception)
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
        }

        private static PremiumCheckBox CreateOption(string text, Point location, Size size, bool isChecked)
        {
            return new PremiumCheckBox
            {
                Checked = isChecked,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = UiPalette.Ivory,
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
