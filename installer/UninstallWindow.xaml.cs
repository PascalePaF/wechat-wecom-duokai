using System;
using System.Windows;
using WechatDuokai.Presentation;

namespace WechatDuokai.Installer
{
    public partial class UninstallWindow : Window
    {
        private readonly CleanupLocations _locations;

        public UninstallWindow()
        {
            _locations = InstallerEngine.GetCleanupLocations();
            InitializeComponent();

            KeepSourceOption.Checked += OptionChanged;
            DeleteSourceOption.Checked += OptionChanged;

            var sourceAvailable = InstallerEngine.ValidateSourceRoot(_locations.SourceRoot);
            DeleteSourceOption.IsEnabled = sourceAvailable;
            SourceUnavailableText.Visibility = sourceAvailable ? Visibility.Collapsed : Visibility.Visible;
            VerifiedPathsText.Text =
                "源码：" + (_locations.SourceRoot ?? "未关联") + "\r\n" +
                "发布包：" + (_locations.ArtifactRoot ?? _locations.PackageRoot ?? "未关联");

            UpdateChoice();
            UpdateThemeButton();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.Apply(this);
        }

        private void OptionChanged(object sender, RoutedEventArgs e)
        {
            UpdateChoice();
        }

        private void ConfirmSourceDeletion_Changed(object sender, RoutedEventArgs e)
        {
            UpdateChoice();
        }

        private void UpdateChoice()
        {
            if (CleanupButton == null || DeleteSourceOption == null || ConfirmSourceDeletion == null)
            {
                return;
            }

            var deleteSource = DeleteSourceOption.IsChecked == true;
            if (!deleteSource && ConfirmSourceDeletion.IsChecked == true)
            {
                ConfirmSourceDeletion.IsChecked = false;
            }
            ConfirmSourceDeletion.Visibility = deleteSource ? Visibility.Visible : Visibility.Collapsed;
            CleanupButton.IsEnabled = !deleteSource || ConfirmSourceDeletion.IsChecked == true;
            CleanupButton.Style = (Style)FindResource(deleteSource ? "DangerButtonStyle" : "PrimaryButtonStyle");
        }

        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteSource = DeleteSourceOption.IsChecked == true;
            if (deleteSource &&
                (!InstallerEngine.ValidateSourceRoot(_locations.SourceRoot) || ConfirmSourceDeletion.IsChecked != true))
            {
                MessageBox.Show("源码目录未通过安全校验，未执行删除。", "安全保护",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var details = deleteSource
                ? "即将永久删除源码目录：\r\n" + _locations.SourceRoot +
                  "\r\n\r\n同时删除安装程序、绿色版和全部发布包。"
                : "即将删除安装程序、绿色版和全部发布包。\r\n\r\n源码目录会保留：\r\n" +
                  (_locations.SourceRoot ?? "未关联");

            if (MessageBox.Show(details + "\r\n\r\n确定继续吗？", "最后确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
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
                    "清理失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
    }
}
