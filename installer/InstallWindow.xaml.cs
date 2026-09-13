using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WechatDuokai.Presentation;
using WinForms = System.Windows.Forms;

namespace WechatDuokai.Installer
{
    public partial class InstallWindow : Window
    {
        private string _selectedInstallDirectory;
        private InstallResult _installedResult;
        private bool _installCompleted;

        public InstallWindow()
        {
            InitializeComponent();
            _selectedInstallDirectory = InstallerEngine.SuggestedInstallDirectory;
            InstallPathText.Text = _selectedInstallDirectory;
            UpdateThemeButton();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.Apply(this);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "请选择或新建微信多开助手的专用安装文件夹",
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = FindNearestExistingDirectory(_selectedInstallDirectory),
                ShowNewFolderButton = true
            })
            {
                var owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (dialog.ShowDialog(new NativeWindowOwner(owner)) != WinForms.DialogResult.OK ||
                    string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return;
                }

                _selectedInstallDirectory = Path.GetFullPath(dialog.SelectedPath);
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
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
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
                MessageBox.Show(validationError, "请选择其他文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
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
                SetStatus("安装失败，请关闭正在运行的旧版本后重试", "DangerBrush");
                MessageBox.Show(ex.Message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                SetStatus("应用未能启动；安装内容仍然保留", "WarningBrush");
                MessageBox.Show("安装已经完成，但启动应用失败：\r\n" + ex.Message,
                    "启动提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
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
            ThemeManager.Toggle();
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

        private sealed class NativeWindowOwner : WinForms.IWin32Window
        {
            internal NativeWindowOwner(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
