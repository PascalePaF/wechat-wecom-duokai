using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WechatDuokai.Presentation;
using WechatDuokai.Update;

namespace WechatDuokai.Installer
{
    public partial class InstallWindow : Window
    {
        private string _selectedInstallDirectory;
        private InstallResult _installedResult;
        private bool _installCompleted;
        private readonly UpdatePlan _updatePlan;
        private readonly string _updatePlanPath;
        private bool _autoUpdateRunning;

        public InstallWindow()
        {
            InitializeComponent();
            ApplyProductIdentity();
            _selectedInstallDirectory = InstallerEngine.SuggestedInstallDirectory;
            InstallPathText.Text = _selectedInstallDirectory;
            UpdateThemeButton();
        }

        internal InstallWindow(UpdatePlan updatePlan, string updatePlanPath)
        {
            _updatePlan = updatePlan ?? throw new ArgumentNullException(nameof(updatePlan));
            _updatePlanPath = Path.GetFullPath(updatePlanPath);
            InitializeComponent();
            ApplyProductIdentity();
            _selectedInstallDirectory = _updatePlan.TargetDirectory;
            InstallPathText.Text = _selectedInstallDirectory;
            ConfigureAutoUpdatePresentation();
            UpdateThemeButton();
            Loaded += async (sender, args) => await BeginAutoUpdateAsync();
        }

        private void ApplyProductIdentity()
        {
            Title = ProductIdentity.Name + " V" + InstallerEngine.Version + " 安装程序";
            VersionText.Text = "V" + InstallerEngine.Version;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.Apply(this);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ThemedPathPicker.SelectFolder(this,
                "选择微窗助手安装位置", FindNearestExistingDirectory(_selectedInstallDirectory));
            if (string.IsNullOrWhiteSpace(selected)) return;
            _selectedInstallDirectory = Path.GetFullPath(selected);
            InstallPathText.Text = _selectedInstallDirectory;
            if (InstallerEngine.ValidateInstallTarget(_selectedInstallDirectory, out var error))
            {
                SetStatus("安装位置已选择", "SuccessBrush");
            }
            else
            {
                SetStatus(error, "WarningBrush");
            }
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_updatePlan != null)
            {
                return;
            }

            if (_installCompleted)
            {
                if (InstallFlowPolicy.CanLaunch(_installCompleted, true))
                {
                    LaunchInstalledApplication();
                }
                return;
            }

            if (!InstallerEngine.ValidateInstallTarget(_selectedInstallDirectory, out var validationError))
            {
                SetStatus(validationError, "WarningBrush");
                ThemedDialogService.Show(this, "请选择其他文件夹", validationError, true);
                return;
            }

            var running = InstallerEngine.FindRunningApplications(_selectedInstallDirectory);
            if (running.Count > 0)
            {
                var closeChoice = ThemedDialogService.Confirm(this,
                    "需要关闭正在运行的助手",
                    "检测到这个安装位置的微窗助手正在运行。\r\n\r\n" +
                    "是否先正常关闭助手，再继续覆盖安装？QQ、微信和企业微信不会被关闭。",
                    "关闭并安装");
                if (!closeChoice)
                {
                    SetStatus("安装已暂停；正在运行的助手保持不变", "WarningBrush");
                    return;
                }

                var remaining = InstallerEngine.CloseRunningApplications(
                    _selectedInstallDirectory, running, false);
                if (remaining.Count > 0)
                {
                    var forceChoice = ThemedDialogService.Confirm(this,
                        "助手仍在运行",
                        "助手未能在 5 秒内正常退出。\r\n\r\n" +
                        "是否只强制关闭该安装目录中的助手进程，然后继续安装？QQ、微信和企业微信不会被关闭。",
                        "强制关闭助手", true);
                    if (!forceChoice)
                    {
                        SetStatus("安装已暂停；请退出助手后重试", "WarningBrush");
                        return;
                    }

                    remaining = InstallerEngine.CloseRunningApplications(
                        _selectedInstallDirectory, remaining, true);
                    if (remaining.Count > 0)
                    {
                        SetStatus("无法关闭正在运行的助手，未覆盖文件", "DangerBrush");
                        ThemedDialogService.Show(this, "无法继续安装",
                            "仍有助手进程占用安装文件。请手动退出后重试。", true);
                        return;
                    }
                }
            }

            SetInteractiveState(false);
            SetStatus("正在写入程序文件和快捷方式…", "AccentBrush");
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _installedResult = InstallerEngine.Install(
                    _selectedInstallDirectory,
                    DesktopShortcutCheckBox.IsChecked == true);
                EnterCompletedState();
            }
            catch (Exception ex)
            {
                SetStatus("安装失败；原有客户端文件未被修改", "DangerBrush");
                ThemedDialogService.Show(this, "安装失败", ex.Message, true);
                SetInteractiveState(true);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void EnterCompletedState()
        {
            _installCompleted = true;
            CompletionPanel.Visibility = Visibility.Visible;
            BrowseButton.IsEnabled = false;
            DesktopShortcutCheckBox.IsEnabled = false;
            PrimaryButton.IsEnabled = true;
            SecondaryButton.IsEnabled = true;
            PrimaryButton.Content = "确认并启动";
            SecondaryButton.Content = "稍后启动";
            SetStatus("安装完成；应用尚未启动", "SuccessBrush");
            PrimaryButton.Focus();
        }

        private void ConfigureAutoUpdatePresentation()
        {
            Title = "微窗助手 V" + _updatePlan.TargetVersion + " 更新程序";
            HeaderTitle.Text = "更新微窗助手";
            HeaderSubtitle.Text = "校验正式 Release · 安全覆盖 · 保留本地数据";
            VersionText.Text = "V" + _updatePlan.TargetVersion;
            LocationTitle.Text = "当前程序位置";
            LocationSubtitle.Text = "只更新经过标记验证的安装版或绿色版目录";
            LocationHint.Text = "更新前会再次验证安装包 SHA-256；失败时自动恢复旧程序文件，data 文件夹不会被覆盖。";
            System.Windows.Controls.Grid.SetColumnSpan(InstallPathText, 2);
            BrowseButton.Visibility = Visibility.Collapsed;
            DesktopShortcutCheckBox.Visibility = Visibility.Collapsed;
            CompletionTitle.Text = "更新完成，正在重新启动";
            CompletionSubtitle.Text = "设置、主题、诊断和恢复日志均已保留。";
            PrimaryButton.Visibility = Visibility.Collapsed;
            SecondaryButton.Content = "取消";
            SetStatus("准备验证更新计划", "AccentBrush");
        }

        private async Task BeginAutoUpdateAsync()
        {
            if (_autoUpdateRunning)
            {
                return;
            }

            _autoUpdateRunning = true;
            SetInteractiveState(false);
            PrimaryButton.Visibility = Visibility.Collapsed;
            SetStatus("正在等待旧版本安全退出…", "AccentBrush");
            Mouse.OverrideCursor = Cursors.Wait;
            await Task.Delay(120);
            try
            {
                _installedResult = InstallerEngine.ApplyVerifiedUpdate(_updatePlan, _updatePlanPath);
                _installCompleted = true;
                CompletionPanel.Visibility = Visibility.Visible;
                SetStatus("更新完成 · 即将重新启动 V" + _updatePlan.TargetVersion, "SuccessBrush");
                await Task.Delay(650);
                LaunchInstalledApplication();
            }
            catch (Exception ex)
            {
                SetStatus("更新未完成 · 已保留或恢复原版本", "DangerBrush");
                ThemedDialogService.Show(this, "更新失败",
                    "一键更新没有完成。程序已停止覆盖，并尽可能恢复原版本。\r\n\r\n" +
                    ex.Message + "\r\n\r\n如仍有问题，可从 GitHub Release 手动下载安装。", true);
                SecondaryButton.Content = "关闭";
                SecondaryButton.IsEnabled = true;
            }
            finally
            {
                _autoUpdateRunning = false;
                Mouse.OverrideCursor = null;
            }
        }

        private void LaunchInstalledApplication()
        {
            try
            {
                if (_installedResult == null || !File.Exists(_installedResult.ExecutablePath))
                {
                    throw new FileNotFoundException("安装后的应用文件不存在。");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _installedResult.ExecutablePath,
                    WorkingDirectory = _installedResult.InstallDirectory,
                    UseShellExecute = true
                });
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("应用未能启动；已写入的程序仍然保留", "WarningBrush");
                ThemedDialogService.Show(this, "启动提示",
                    (_updatePlan == null ? "安装" : "更新") +
                    "已经完成，但启动应用失败：\r\n" + ex.Message, true);
            }
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_autoUpdateRunning)
            {
                return;
            }
            Close();
        }

        private void SetInteractiveState(bool enabled)
        {
            PrimaryButton.IsEnabled = enabled;
            BrowseButton.IsEnabled = enabled;
            DesktopShortcutCheckBox.IsEnabled = enabled;
            SecondaryButton.IsEnabled = enabled;
        }

        private void SetStatus(string text, string resourceKey)
        {
            StatusText.Text = text;
            StatusText.SetResourceReference(ForegroundProperty, resourceKey);
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, resourceKey);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle(false);
            UpdateThemeButton();
            WindowChromeHelper.Apply(this);
        }

        private void UpdateThemeButton()
        {
            var dark = ThemeManager.Current == AppTheme.Dark;
            ThemeGlyph.Text = dark ? "☀" : "☾";
            ThemeLabel.Text = dark ? "日间" : "夜间";
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

    }
}
