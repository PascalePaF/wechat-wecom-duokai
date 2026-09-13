using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WechatDuokai.Core;
using WechatDuokai.Presentation;

namespace WechatDuokai.App
{
    public partial class MainWindow : Window
    {
        private static readonly Regex DigitsOnly = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly InstanceManager _instanceManager = new InstanceManager();
        private readonly DispatcherTimer _refreshTimer;
        private AppDefinition _weChat;
        private AppDefinition _weCom;
        private bool _busy;
        private bool _updatingCount;

        public MainWindow()
        {
            InitializeComponent();
            DataObject.AddPastingHandler(TargetCountBox, TargetCountBox_OnPaste);

            _updatingCount = true;
            TargetCountBox.Text = UserPreferences.LoadTargetCount().ToString();
            _updatingCount = false;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += (sender, args) => RefreshClientStatus();

            Loaded += (sender, args) =>
            {
                LocateClients();
                UpdateThemeButton();
                RefreshClientStatus();
                _refreshTimer.Start();
            };
            Closed += (sender, args) => _refreshTimer.Stop();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.Apply(this);
        }

        private void LocateClients()
        {
            _weChat = ApplicationLocator.FindWeChat();
            _weCom = ApplicationLocator.FindWeCom();
            WeChatPath.Text = DescribePath(_weChat);
            WeComPath.Text = DescribePath(_weCom);
        }

        private static string DescribePath(AppDefinition application)
        {
            return application != null && application.IsAvailable
                ? application.ExecutablePath
                : "未找到客户端，请先安装官方版本";
        }

        private void RefreshClientStatus()
        {
            if (_busy)
            {
                return;
            }

            UpdateClientStatus(_weChat, WeChatStatus, WeChatButton);
            UpdateClientStatus(_weCom, WeComStatus, WeComButton);
        }

        private void UpdateClientStatus(AppDefinition application, TextBlock status, Button button)
        {
            if (application == null || !application.IsAvailable)
            {
                status.Text = "当前未检测到";
                status.SetResourceReference(ForegroundProperty, "WarningBrush");
                button.IsEnabled = !_busy;
                return;
            }

            var count = _instanceManager.GetInstanceCount(application);
            status.Text = count > 0 ? $"当前运行 {count} 个实例" : "当前未运行";
            status.SetResourceReference(ForegroundProperty, count > 0 ? "SuccessBrush" : "TextSecondaryBrush");
            button.IsEnabled = !_busy;
        }

        private async void WeChatButton_Click(object sender, RoutedEventArgs e)
        {
            await StartOrRestoreAsync(AppKind.WeChat);
        }

        private async void WeComButton_Click(object sender, RoutedEventArgs e)
        {
            await StartOrRestoreAsync(AppKind.WeCom);
        }

        private async Task StartOrRestoreAsync(AppKind kind)
        {
            if (_busy)
            {
                return;
            }

            var application = kind == AppKind.WeChat ? _weChat : _weCom;
            if (application == null || !application.IsAvailable)
            {
                LocateClients();
                application = kind == AppKind.WeChat ? _weChat : _weCom;
            }

            if (application == null || !application.IsAvailable)
            {
                SetStatus($"未找到{(kind == AppKind.WeChat ? "微信" : "企业微信")}，请先安装官方桌面客户端。", "WarningBrush");
                MessageBox.Show("没有找到官方客户端程序。\r\n\r\n请先安装或启动一次客户端，再回到这里重试。",
                    "未找到客户端", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetBusy(true);
            try
            {
                var target = GetTargetCount();
                UserPreferences.SaveTargetCount(target);
                var result = await _instanceManager.EnsureTargetCountAsync(
                    application,
                    target,
                    message => Dispatcher.Invoke(() => SetStatus(message, "InfoBrush")),
                    CancellationToken.None);

                SetStatus(result.Message, result.Success ? "SuccessBrush" : "WarningBrush");
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "补开结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetStatus("启动未完成：" + ex.Message, "DangerBrush");
                MessageBox.Show("启动未完成，但程序没有修改客户端文件。\r\n\r\n" + ex.Message,
                    "启动提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
                RefreshClientStatus();
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            WeChatButton.IsEnabled = !busy;
            WeComButton.IsEnabled = !busy;
            DecreaseButton.IsEnabled = !busy;
            IncreaseButton.IsEnabled = !busy;
            TargetCountBox.IsEnabled = !busy;
        }

        private int GetTargetCount()
        {
            if (!int.TryParse(TargetCountBox.Text, out var count))
            {
                count = UserPreferences.LoadTargetCount();
            }

            return Math.Max(1, Math.Min(10, count));
        }

        private void SetTargetCount(int count)
        {
            count = Math.Max(1, Math.Min(10, count));
            _updatingCount = true;
            TargetCountBox.Text = count.ToString();
            TargetCountBox.CaretIndex = TargetCountBox.Text.Length;
            _updatingCount = false;
            UserPreferences.SaveTargetCount(count);
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            SetTargetCount(GetTargetCount() - 1);
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            SetTargetCount(GetTargetCount() + 1);
        }

        private void TargetCountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void TargetCountBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var text = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrWhiteSpace(text) || !DigitsOnly.IsMatch(text))
            {
                e.CancelCommand();
            }
        }

        private void TargetCountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingCount || !int.TryParse(TargetCountBox.Text, out var count))
            {
                return;
            }

            if (count >= 1 && count <= 10)
            {
                UserPreferences.SaveTargetCount(count);
            }
        }

        private void TargetCountBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            SetTargetCount(GetTargetCount());
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
            UpdateThemeButton();
            WindowChromeHelper.Apply(this);
        }

        private void UpdateThemeButton()
        {
            var isDark = ThemeManager.Current == AppTheme.Dark;
            ThemeGlyph.Text = isDark ? "☀" : "☾";
            ThemeLabel.Text = isDark ? "日间" : "夜间";
        }

        private void ProjectButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/PascalePaF/wechat-wecom-duokai",
                UseShellExecute = true
            });
        }

        private void SetStatus(string message, string resourceKey)
        {
            FooterStatus.Text = message;
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, resourceKey);
        }
    }
}
