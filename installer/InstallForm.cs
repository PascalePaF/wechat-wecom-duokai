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
        private readonly CheckBox _desktopShortcut;
        private readonly CheckBox _runAfterInstall;
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
            ClientSize = new Size(650, 430);
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
            header.Controls.Add(CreateLabel("DUOKAI  /  INSTALLER", new Point(25, 12), new Size(220, 16),
                new Font("Segoe UI", 7.5F, FontStyle.Bold), UiPalette.Gold));
            header.Controls.Add(CreateLabel("安装微信 · 企业微信多开助手", new Point(23, 33), new Size(420, 34),
                new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), UiPalette.Ivory));
            header.Controls.Add(CreateLabel("V1.0.1  ·  当前用户安装，无需管理员权限", new Point(25, 71), new Size(400, 20),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted));

            var closeButton = CreateChromeButton("×", new Point(602, 15));
            closeButton.Click += (sender, args) => Close();
            header.Controls.Add(closeButton);
            Controls.Add(header);

            var card = new RoundedPanel
            {
                Location = new Point(22, 118),
                Size = new Size(606, 224),
                BackColor = UiPalette.Surface,
                BorderColor = UiPalette.Border,
                CornerRadius = 17
            };
            card.Controls.Add(CreateLabel("安装到您的电脑", new Point(22, 18), new Size(250, 24),
                new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), UiPalette.Ivory));
            card.Controls.Add(CreateLabel("安装文件夹", new Point(22, 50), new Size(100, 18),
                new Font("Microsoft YaHei UI", 8F), UiPalette.Muted));

            var pathPanel = new RoundedPanel
            {
                Location = new Point(22, 73),
                Size = new Size(562, 50),
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
                Location = new Point(14, 16),
                ReadOnly = true,
                Size = new Size(443, 20),
                TabStop = false,
                Text = _selectedInstallDirectory
            };
            _browseButton = new PremiumButton
            {
                BackColor = UiPalette.SurfaceHover,
                HoverBackColor = Color.FromArgb(48, 51, 58),
                PressedBackColor = UiPalette.Border,
                BorderColor = UiPalette.Border,
                BorderThickness = 1,
                CornerRadius = 9,
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                ForeColor = UiPalette.Ivory,
                Location = new Point(469, 7),
                Size = new Size(84, 36),
                Text = "浏览…"
            };
            _browseButton.Click += BrowseButton_Click;
            pathPanel.Controls.Add(_installPathTextBox);
            pathPanel.Controls.Add(_browseButton);
            card.Controls.Add(pathPanel);

            card.Controls.Add(CreateLabel("请通过“浏览”选择或新建专用文件夹；为保护已有文件，不允许安装到非空的普通目录。",
                new Point(23, 132), new Size(558, 20), new Font("Microsoft YaHei UI", 7.8F), UiPalette.MutedDark));

            _desktopShortcut = CreateOption("创建桌面快捷方式", new Point(23, 164), true);
            _runAfterInstall = CreateOption("安装完成后启动程序", new Point(225, 164), true);
            card.Controls.Add(_desktopShortcut);
            card.Controls.Add(_runAfterInstall);
            card.Controls.Add(CreateLabel("不会安装微信或企业微信，也不会修改它们的程序文件。",
                new Point(23, 197), new Size(520, 18), new Font("Microsoft YaHei UI", 7.8F), UiPalette.MutedDark));
            Controls.Add(card);

            _statusDot = CreateLabel("●", new Point(25, 377), new Size(13, 18),
                new Font("Segoe UI", 8F), UiPalette.Green);
            _statusLabel = CreateLabel("准备安装", new Point(43, 369), new Size(315, 32),
                new Font("Microsoft YaHei UI", 8.5F), UiPalette.Muted, ContentAlignment.MiddleLeft);
            _statusLabel.AutoEllipsis = true;
            Controls.Add(_statusDot);
            Controls.Add(_statusLabel);

            var cancelButton = new PremiumButton
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(430, 364),
                Size = new Size(86, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Surface,
                HoverBackColor = UiPalette.SurfaceHover,
                PressedBackColor = UiPalette.Border,
                BorderColor = UiPalette.Border,
                BorderThickness = 1,
                ForeColor = UiPalette.Ivory
            };
            Controls.Add(cancelButton);

            _installButton = new PremiumButton
            {
                Text = "立即安装",
                Location = new Point(526, 364),
                Size = new Size(102, 40),
                CornerRadius = 11,
                BackColor = UiPalette.Gold,
                HoverBackColor = UiPalette.GoldHover,
                PressedBackColor = UiPalette.GoldPressed,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = UiPalette.Canvas
            };
            _installButton.Click += InstallButton_Click;
            Controls.Add(_installButton);

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

        private static CheckBox CreateOption(string text, Point location, bool isChecked)
        {
            return new CheckBox
            {
                AutoSize = true,
                BackColor = UiPalette.Surface,
                Checked = isChecked,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = UiPalette.Ivory,
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
