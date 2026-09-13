using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using WechatDuokai.Core;
using WechatDuokai.Presentation;

namespace WechatDuokai.App
{
    public partial class MainWindow : Window
    {
        internal const double MinimumWindowWidth = 901d;
        internal const double MinimumWindowHeight = 513d;
        private const string CurrentVersion = "1.0.4";
        private static readonly Regex DigitsOnly = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly InstanceManager _instanceManager = new InstanceManager();
        private readonly DiagnosticReportService _diagnostics = new DiagnosticReportService();
        private readonly ReleaseUpdateChecker _updateChecker = new ReleaseUpdateChecker();
        private readonly DispatcherTimer _refreshTimer;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
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

            Loaded += async (sender, args) =>
            {
                LocateClients();
                UpdateThemeButton();
                ApplyProportionalScale();
                RefreshClientStatus();
                _refreshTimer.Start();
                await CheckForUpdatesAsync();
            };
            Closed += (sender, args) =>
            {
                _refreshTimer.Stop();
                _lifetime.Cancel();
                _lifetime.Dispose();
            };
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeHelper.Apply(this);
        }

        private void ScaleViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyProportionalScale(e.NewSize.Width, e.NewSize.Height);
        }

        internal static double CalculateInterfaceScale(double windowWidth, double windowHeight)
        {
            if (double.IsNaN(windowWidth) || double.IsInfinity(windowWidth) ||
                double.IsNaN(windowHeight) || double.IsInfinity(windowHeight) ||
                windowWidth <= 0 || windowHeight <= 0)
            {
                return 1d;
            }

            return Math.Max(1d, Math.Min(windowWidth / MinimumWindowWidth,
                windowHeight / MinimumWindowHeight));
        }

        private void ApplyProportionalScale()
        {
            if (ScaleViewport == null)
            {
                return;
            }

            ApplyProportionalScale(ScaleViewport.ActualWidth, ScaleViewport.ActualHeight);
        }

        private void ApplyProportionalScale(double viewportWidth, double viewportHeight)
        {
            if (ScaledRoot == null || InterfaceScaleTransform == null ||
                viewportWidth <= 0 || viewportHeight <= 0)
            {
                return;
            }

            var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            var windowHeight = ActualHeight > 0 ? ActualHeight : Height;
            var scale = CalculateInterfaceScale(windowWidth, windowHeight);

            InterfaceScaleTransform.ScaleX = scale;
            InterfaceScaleTransform.ScaleY = scale;
            ScaledRoot.Width = viewportWidth / scale;
            ScaledRoot.Height = viewportHeight / scale;
        }

        private void LocateClients()
        {
            _weChat = ApplicationLocator.FindWeChat(UserPreferences.LoadCustomClientPath(AppKind.WeChat));
            _weCom = ApplicationLocator.FindWeCom(UserPreferences.LoadCustomClientPath(AppKind.WeCom));
            WeChatPath.Text = DescribePath(_weChat);
            WeComPath.Text = DescribePath(_weCom);
        }

        private static string DescribePath(AppDefinition application)
        {
            return application != null && application.IsAvailable
                ? application.ExecutablePath
                : "未找到客户端 · 点击 … 手动选择";
        }

        private void RefreshClientStatus()
        {
            if (_busy) return;
            UpdateClientStatus(_weChat, WeChatStatus, WeChatButton);
            UpdateClientStatus(_weCom, WeComStatus, WeComButton);
        }

        private void UpdateClientStatus(AppDefinition application, TextBlock status, Button button)
        {
            if (application == null || !application.IsAvailable)
            {
                status.Text = "当前未检测到";
                status.SetResourceReference(ForegroundProperty, "WarningBrush");
                button.IsEnabled = false;
                return;
            }

            var count = _instanceManager.GetInstanceCount(application);
            status.Text = count > 0 ? $"当前运行 {count} 个实例" : "已验证官方客户端";
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

        private void WeChatSelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectClient(AppKind.WeChat);
        }

        private void WeComSelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectClient(AppKind.WeCom);
        }

        private void SelectClient(AppKind kind)
        {
            var expectedName = kind == AppKind.WeChat ? "微信" : "企业微信";
            var existing = kind == AppKind.WeChat ? _weChat : _weCom;
            var dialog = new OpenFileDialog
            {
                Title = "选择官方" + expectedName + "程序",
                CheckFileExists = true,
                Multiselect = false,
                Filter = kind == AppKind.WeChat
                    ? "微信程序 (Weixin.exe;WeChat.exe)|Weixin.exe;WeChat.exe|程序文件 (*.exe)|*.exe"
                    : "企业微信程序 (WXWork.exe)|WXWork.exe|程序文件 (*.exe)|*.exe"
            };
            if (existing != null && existing.IsAvailable)
            {
                dialog.InitialDirectory = Path.GetDirectoryName(existing.ExecutablePath);
                dialog.FileName = Path.GetFileName(existing.ExecutablePath);
            }

            if (dialog.ShowDialog(this) != true) return;
            var validation = ClientExecutableValidator.Validate(dialog.FileName, kind);
            if (!validation.IsValid)
            {
                SetStatus("所选文件未通过官方客户端验证", "WarningBrush");
                MessageBox.Show(validation.ErrorMessage,
                    "未通过验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UserPreferences.SaveCustomClientPath(kind, validation.NormalizedPath);
            LocateClients();
            RefreshClientStatus();
            SetStatus(expectedName + "位置已保存 · 腾讯签名有效", "SuccessBrush");
        }

        private async Task StartOrRestoreAsync(AppKind kind)
        {
            if (_busy) return;
            var application = kind == AppKind.WeChat ? _weChat : _weCom;
            if (application == null || !application.IsAvailable)
            {
                LocateClients();
                application = kind == AppKind.WeChat ? _weChat : _weCom;
            }

            if (application == null || !application.IsAvailable)
            {
                var name = kind == AppKind.WeChat ? "微信" : "企业微信";
                SetStatus("未找到" + name + "，可点击卡片内的 … 手动选择。", "WarningBrush");
                MessageBox.Show("没有找到通过腾讯签名验证的官方客户端。\r\n\r\n" +
                                "请先安装官方版本，或点击卡片中的 … 选择程序文件。",
                    "未找到客户端", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetBusy(true);
            try
            {
                var target = GetTargetCount();
                UserPreferences.SaveTargetCount(target);
                var result = await _instanceManager.EnsureTargetCountAsync(
                    application, target,
                    message => Dispatcher.Invoke(() => SetStatus(message, "InfoBrush")),
                    _lifetime.Token);
                SetStatus(result.Message, result.Success ? "SuccessBrush" : "WarningBrush");
                if (!result.Success)
                    MessageBox.Show(result.Message, "补开结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
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
            WeChatButton.IsEnabled = !busy && _weChat != null && _weChat.IsAvailable;
            WeComButton.IsEnabled = !busy && _weCom != null && _weCom.IsAvailable;
            WeChatSelectButton.IsEnabled = !busy;
            WeComSelectButton.IsEnabled = !busy;
            DecreaseButton.IsEnabled = !busy;
            IncreaseButton.IsEnabled = !busy;
            TargetCountBox.IsEnabled = !busy;
            DiagnosticsButton.IsEnabled = !busy;
        }

        private int GetTargetCount()
        {
            int count;
            if (!int.TryParse(TargetCountBox.Text, out count)) count = UserPreferences.LoadTargetCount();
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

        private void DecreaseButton_Click(object sender, RoutedEventArgs e) { SetTargetCount(GetTargetCount() - 1); }
        private void IncreaseButton_Click(object sender, RoutedEventArgs e) { SetTargetCount(GetTargetCount() + 1); }

        private void TargetCountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void TargetCountBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var text = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrWhiteSpace(text) || !DigitsOnly.IsMatch(text)) e.CancelCommand();
        }

        private void TargetCountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int count;
            if (_updatingCount || !int.TryParse(TargetCountBox.Text, out count)) return;
            if (count >= 1 && count <= 10) UserPreferences.SaveTargetCount(count);
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
            var dark = ThemeManager.Current == AppTheme.Dark;
            ThemeGlyph.Text = dark ? "☀" : "☾";
            ThemeLabel.Text = dark ? "日间" : "夜间";
        }

        private async Task CheckForUpdatesAsync()
        {
            ReleaseUpdateResult result;
            try
            {
                result = await _updateChecker.CheckAsync(CurrentVersion, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!result.CheckSucceeded)
            {
                ReleaseButton.ToolTip = "本次未能检查更新；点击仍可打开 GitHub 发布页";
                return;
            }

            if (result.IsUpdateAvailable)
            {
                ReleaseButton.Content = "发现 V" + result.LatestVersion + " ↗";
                ReleaseButton.SetResourceReference(ForegroundProperty, "SuccessBrush");
                ReleaseButton.ToolTip = "发现新版本；点击前往 GitHub 下载（不会在软件内更新）";
                SetStatus("发现新版本 V" + result.LatestVersion + " · 可前往 GitHub 下载", "InfoBrush");
            }
            else
            {
                ReleaseButton.ToolTip = "当前已是最新版；点击查看 GitHub 发布页";
            }
        }

        private void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = _diagnostics.CreateReport(AppDomain.CurrentDomain.BaseDirectory,
                    _weChat, _weCom, _instanceManager);
                SetStatus("本地诊断报告已生成", "SuccessBrush");
                if (MessageBox.Show("诊断报告已保存在程序目录：\r\n" + report +
                                    "\r\n\r\n报告不会自动上传。现在打开所在文件夹吗？",
                        "诊断完成", MessageBoxButton.YesNo, MessageBoxImage.Information,
                        MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + report + "\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                SetStatus("无法写入本地诊断报告", "WarningBrush");
                MessageBox.Show("无法在程序目录创建 diagnostics 文件夹：\r\n" + ex.Message,
                    "诊断提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ReleaseButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleaseUpdateChecker.ReleasesUrl,
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
