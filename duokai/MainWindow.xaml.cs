using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
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
        private static readonly string CurrentVersion = ProductIdentity.GetVersion(typeof(MainWindow).Assembly);
        private static readonly Regex DigitsOnly = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly InstanceManager _instanceManager;
        private readonly DiagnosticReportService _diagnostics = new DiagnosticReportService();
        private readonly ReleaseUpdateChecker _updateChecker;
        private readonly ApplicationUpdateService _applicationUpdater = new ApplicationUpdateService();
        private readonly DispatcherTimer _refreshTimer;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private AppDefinition _weChat;
        private AppDefinition _weCom;
        private bool _busy;
        private bool _updatingCount;
        private bool _checkingUpdates;
        private bool _installingUpdate;
        private bool _showingSettings;
        private bool _initializingSettings = true;
        private ReleaseUpdateResult _availableUpdate;

        public MainWindow()
            : this(new InstanceManager(), new ReleaseUpdateChecker())
        {
        }

        internal MainWindow(InstanceManager instanceManager, ReleaseUpdateChecker updateChecker)
        {
            _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            InitializeComponent();
            Title = ProductIdentity.Name + " V" + CurrentVersion;
            HeaderVersionText.Text = "V" + CurrentVersion;
            SettingsVersionText.Text = "V" + CurrentVersion;
            DataObject.AddPastingHandler(TargetCountBox, TargetCountBox_OnPaste);
            var targetCount = UserPreferences.LoadTargetCount();
            _updatingCount = true;
            TargetCountBox.Text = targetCount.ToString();
            _updatingCount = false;
            // Canonicalize V1.0.6's two target-count keys into the single shared V1.0.7 key.
            UserPreferences.SaveTargetCount(targetCount);
            SynchronizeSettingsControls();
            _initializingSettings = false;
            SetBusy(true);

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += (sender, args) => RefreshClientStatus();

            Loaded += async (sender, args) =>
            {
                var recovery = await Task.Run(() => _instanceManager.RecoverPendingWeComRegistryState());
                LocateClients();
                SetBusy(false);
                UpdateThemeButton();
                ApplyProportionalScale();
                RefreshClientStatus();
                _refreshTimer.Start();
                await Task.Run(() => _applicationUpdater.CleanupStaleDownloads());
                if (recovery.Outcome != WeComRegistryRecoveryOutcome.None &&
                    !string.IsNullOrWhiteSpace(recovery.Message))
                {
                    var brush = recovery.Outcome == WeComRegistryRecoveryOutcome.Restored
                        ? "SuccessBrush"
                        : recovery.Outcome == WeComRegistryRecoveryOutcome.ExternalStatePreserved
                            ? "InfoBrush"
                            : "WarningBrush";
                    SetStatus(recovery.Message, brush);
                }
                if (UserPreferences.LoadAutoCheckForUpdates() &&
                    UpdateCheckSchedule.ShouldCheckAutomatically())
                {
                    try
                    {
                        // A stable per-installation delay spreads simultaneous startup checks
                        // without creating an identifier or sending any extra local information.
                        await Task.Delay(UpdateCheckSchedule.GetStartupDelay(), _lifetime.Token);
                        if (UpdateCheckSchedule.ShouldCheckAutomatically())
                        {
                            await CheckForUpdatesAsync(false);
                        }
                    }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
                    {
                    }
                }
            };
            Activated += (sender, args) =>
            {
                if (ThemeManager.RefreshSystemTheme())
                {
                    UpdateThemeButton();
                    WindowChromeHelper.Apply(this);
                }
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
            var weChatCount = GetCurrentCount(_weChat);
            var weComCount = GetCurrentCount(_weCom);
            UpdateFooterCounts(weChatCount, weComCount);
            if (_busy) return;
            UpdateClientStatus(_weChat, WeChatStatus, WeChatButton, WeChatExitButton, weChatCount);
            UpdateClientStatus(_weCom, WeComStatus, WeComButton, WeComExitButton, weComCount);
        }

        private void UpdateFooterCounts(int weChatCount, int weComCount)
        {
            FooterWeChatCount.Text = weChatCount.ToString();
            FooterWeComCount.Text = weComCount.ToString();
            AutomationProperties.SetName(FooterWeChatCountLine,
                $"当前微信窗口 {weChatCount} 个");
            AutomationProperties.SetName(FooterWeComCountLine,
                $"当前企业微信窗口 {weComCount} 个");
        }

        private int GetCurrentCount(AppDefinition application)
        {
            if (application == null || !application.IsAvailable)
            {
                return 0;
            }

            try
            {
                return _instanceManager.GetInstanceCount(application);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void UpdateClientStatus(AppDefinition application, TextBlock status, Button button,
            Button exitButton, int count)
        {
            if (application == null || !application.IsAvailable)
            {
                status.Text = "当前未检测到";
                status.SetResourceReference(ForegroundProperty, "WarningBrush");
                button.IsEnabled = false;
                exitButton.IsEnabled = false;
                return;
            }

            status.Text = count > 0 ? $"当前运行 {count} 个实例" : "已验证官方客户端";
            status.SetResourceReference(ForegroundProperty, count > 0 ? "SuccessBrush" : "TextSecondaryBrush");
            button.IsEnabled = !_busy;
            exitButton.IsEnabled = !_busy && count > 0;
        }

        private async void WeChatButton_Click(object sender, RoutedEventArgs e)
        {
            await StartOrRestoreAsync(AppKind.WeChat);
        }

        private async void WeComButton_Click(object sender, RoutedEventArgs e)
        {
            await StartOrRestoreAsync(AppKind.WeCom);
        }

        private async void WeChatExitButton_Click(object sender, RoutedEventArgs e)
        {
            await ExitAllAsync(AppKind.WeChat);
        }

        private async void WeComExitButton_Click(object sender, RoutedEventArgs e)
        {
            await ExitAllAsync(AppKind.WeCom);
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

        private async Task ExitAllAsync(AppKind kind)
        {
            if (_busy) return;
            var application = kind == AppKind.WeChat ? _weChat : _weCom;
            var name = kind == AppKind.WeChat ? "微信" : "企业微信";
            var count = GetCurrentCount(application);
            if (count == 0)
            {
                SetStatus("当前没有正在运行的" + name + "窗口。", "InfoBrush");
                RefreshClientStatus();
                return;
            }

            var confirmation = MessageBox.Show(
                "将退出当前 Windows 会话中检测到的全部 " + count + " 个" + name + "窗口。\r\n\r\n" +
                "这只会关闭客户端程序，不会注销账号；尚未发送的内容可能丢失。是否继续？",
                "退出全部" + name + "窗口",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            SetBusy(true);
            try
            {
                var result = await _instanceManager.ExitAllInstancesAsync(
                    application,
                    message => Dispatcher.Invoke(() => SetStatus(message, "InfoBrush")),
                    _lifetime.Token);
                SetStatus(result.Message, result.Success ? "SuccessBrush" : "WarningBrush");
                if (!result.Success)
                {
                    MessageBox.Show(result.Message,
                        "退出结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                SetStatus("退出未完成：" + ex.Message, "DangerBrush");
                MessageBox.Show("退出未完成；程序只尝试处理路径完全匹配的官方客户端进程。\r\n\r\n" + ex.Message,
                    "退出提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            WeChatExitButton.IsEnabled = !busy && GetCurrentCount(_weChat) > 0;
            WeComExitButton.IsEnabled = !busy && GetCurrentCount(_weCom) > 0;
            WeChatSelectButton.IsEnabled = !busy;
            WeComSelectButton.IsEnabled = !busy;
            DecreaseButton.IsEnabled = !busy;
            IncreaseButton.IsEnabled = !busy;
            TargetCountBox.IsEnabled = !busy;
            DiagnosticsButton.IsEnabled = !busy;
            AutoUpdateCheckBox.IsEnabled = !busy;
            SystemThemeRadio.IsEnabled = !busy;
            LightThemeRadio.IsEnabled = !busy;
            DarkThemeRadio.IsEnabled = !busy;
            CheckUpdateButton.IsEnabled = !busy && !_checkingUpdates;
            UpdateNowButton.IsEnabled = !busy && !_installingUpdate &&
                                        _availableUpdate != null && _availableUpdate.CanInstallUpdate;
            SettingsReleaseButton.IsEnabled = !busy;
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
            SynchronizeThemePreferenceControls();
            WindowChromeHelper.Apply(this);
        }

        private void UpdateThemeButton()
        {
            var dark = ThemeManager.Current == AppTheme.Dark;
            ThemeGlyph.Text = dark ? "☀" : "☾";
            ThemeLabel.Text = dark ? "日间" : "夜间";
        }

        private void SynchronizeSettingsControls()
        {
            var wasInitializing = _initializingSettings;
            _initializingSettings = true;
            AutoUpdateCheckBox.IsChecked = UserPreferences.LoadAutoCheckForUpdates();
            SynchronizeThemePreferenceControls();
            _initializingSettings = wasInitializing;
        }

        private void SynchronizeThemePreferenceControls()
        {
            var wasInitializing = _initializingSettings;
            _initializingSettings = true;
            SystemThemeRadio.IsChecked = ThemeManager.Preference == AppThemePreference.System;
            LightThemeRadio.IsChecked = ThemeManager.Preference == AppThemePreference.Light;
            DarkThemeRadio.IsChecked = ThemeManager.Preference == AppThemePreference.Dark;
            _initializingSettings = wasInitializing;
        }

        private void ThemePreference_Checked(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings || !(sender is RadioButton radio) || radio.IsChecked != true)
            {
                return;
            }

            AppThemePreference preference;
            if (!Enum.TryParse(Convert.ToString(radio.Tag), true, out preference))
            {
                return;
            }

            ThemeManager.SetPreference(preference);
            UpdateThemeButton();
            WindowChromeHelper.Apply(this);
            SetStatus(preference == AppThemePreference.System
                ? "主题已设为跟随 Windows 系统"
                : "主题设置已保存", "SuccessBrush");
        }

        private void AutoUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializingSettings)
            {
                return;
            }

            var enabled = AutoUpdateCheckBox.IsChecked == true;
            UserPreferences.SaveAutoCheckForUpdates(enabled);
            SetStatus(enabled
                ? "已开启每日版本检查"
                : "已关闭每日版本检查 · 仍可手动检查", "SuccessBrush");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SetSettingsViewVisible(!_showingSettings);
        }

        private void GeneralSettingsTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetSettingsSection(false);
        }

        private void AboutSettingsTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetSettingsSection(true);
        }

        internal void SetSettingsSection(bool showAbout)
        {
            GeneralSettingsPanel.Visibility = showAbout ? Visibility.Collapsed : Visibility.Visible;
            AboutSettingsPanel.Visibility = showAbout ? Visibility.Visible : Visibility.Collapsed;
            GeneralSettingsTabButton.FontWeight = showAbout ? FontWeights.Normal : FontWeights.SemiBold;
            AboutSettingsTabButton.FontWeight = showAbout ? FontWeights.SemiBold : FontWeights.Normal;
            GeneralSettingsTabButton.Opacity = showAbout ? 0.68 : 1d;
            AboutSettingsTabButton.Opacity = showAbout ? 1d : 0.68;

            if (_showingSettings)
            {
                SetStatus(showAbout
                    ? "软件介绍 · 使用说明与安全边界"
                    : "常规设置 · 更改会自动保存在本机", "InfoBrush");
            }
        }

        internal void SetSettingsViewVisible(bool visible)
        {
            _showingSettings = visible;
            WorkspaceGrid.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            SettingsWorkspace.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            SettingsButton.Content = visible ? "返回" : "设置";
            AutomationProperties.SetName(SettingsButton, visible ? "返回主界面" : "打开设置界面");
            if (visible)
            {
                SynchronizeSettingsControls();
                SetSettingsSection(false);
            }
            else
            {
                SetStatus(string.Empty, "SuccessBrush");
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(true);
        }

        private async Task CheckForUpdatesAsync(bool userInitiated)
        {
            if (_checkingUpdates)
            {
                return;
            }

            _checkingUpdates = true;
            CheckUpdateButton.IsEnabled = false;
            if (userInitiated)
            {
                SetStatus("正在检查 GitHub 发布版本…", "InfoBrush");
                UpdateStatusText.Text = "正在读取本项目最新正式 Release…";
            }

            ReleaseUpdateResult result;
            try
            {
                result = await _updateChecker.CheckAsync(CurrentVersion, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _checkingUpdates = false;
                CheckUpdateButton.IsEnabled = !_busy;
            }

            if (result.CheckSucceeded)
            {
                UpdateCheckSchedule.RecordSuccess(result.LatestVersion);
            }
            else
            {
                UpdateCheckSchedule.RecordFailure(result.RetryAfterUtc);
            }

            if (!result.CheckSucceeded)
            {
                var reason = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "暂时无法读取 GitHub 版本信息。"
                    : result.ErrorMessage;
                SettingsReleaseButton.ToolTip = reason + " 点击仍可打开 GitHub 发布页";
                UpdateStatusText.Text = reason + "\r\n核心多开功能不受影响，也可直接打开发布页。";
                if (userInitiated)
                {
                    SetStatus("检查更新失败 · 可直接打开 GitHub 发布页", "WarningBrush");
                }
                return;
            }

            if (result.IsUpdateAvailable)
            {
                _availableUpdate = result;
                SettingsReleaseButton.Content = "发现 V" + result.LatestVersion + " ↗";
                SettingsReleaseButton.SetResourceReference(ForegroundProperty, "SuccessBrush");
                SettingsReleaseButton.ToolTip = "发现新版本；可在设置中一键更新或查看 GitHub 发布页";
                var mode = _applicationUpdater.DetectCurrentMode();
                if (result.CanInstallUpdate && mode != ApplicationInstallMode.Unknown)
                {
                    UpdateNowButton.Content = "一键更新 V" + result.LatestVersion;
                    UpdateNowButton.Visibility = Visibility.Visible;
                    UpdateNowButton.IsEnabled = !_busy && !_installingUpdate;
                    UpdateStatusText.Text = result.HasGitHubAssetDigests
                        ? "静态清单与 GitHub 摘要已核对 · 点击后执行多重 SHA-256 校验"
                        : result.HasReleaseManifestHashes
                            ? "已读取免 API 静态更新清单 · 点击后执行多重 SHA-256 校验"
                            : "已确认固定 Release 附件 · 点击后按 SHA256SUMS.txt 校验再覆盖";
                    SetStatus("发现新版本 V" + result.LatestVersion + " · 可一键更新", "InfoBrush");
                }
                else
                {
                    UpdateNowButton.Visibility = Visibility.Collapsed;
                    UpdateStatusText.Text = mode == ApplicationInstallMode.Unknown
                        ? "当前是未标记的开发目录，只允许从发布页手动更新"
                        : (result.OneClickUpdateError ?? "该 Release 缺少一键更新所需附件");
                    SetStatus("发现新版本 V" + result.LatestVersion + " · 请打开发布页更新", "WarningBrush");
                }
            }
            else
            {
                _availableUpdate = null;
                UpdateNowButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                SettingsReleaseButton.Content = "发布版本 ↗";
                SettingsReleaseButton.SetResourceReference(ForegroundProperty, "AccentBrush");
                SettingsReleaseButton.ToolTip = "当前已是最新版；点击查看 GitHub 发布页";
                UpdateStatusText.Text = "当前已是最新版 V" + CurrentVersion;
                if (userInitiated)
                {
                    SetStatus("当前已是最新版 V" + CurrentVersion, "SuccessBrush");
                }
            }
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || _installingUpdate || _availableUpdate == null ||
                !_availableUpdate.CanInstallUpdate)
            {
                return;
            }

            var mode = _applicationUpdater.DetectCurrentMode();
            if (mode == ApplicationInstallMode.Unknown)
            {
                MessageBox.Show("当前程序目录不是经过标记验证的安装版或绿色版。\r\n\r\n" +
                                "为避免覆盖源码或其他文件，本次只能打开 GitHub Release 手动更新。",
                    "不能自动覆盖当前目录", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var description = mode == ApplicationInstallMode.Installed ? "安装版" : "绿色免安装版";
            var verification = _availableUpdate.HasGitHubAssetDigests
                ? "GitHub 摘要 + 静态清单 + SHA256SUMS.txt + 下载文件必须一致"
                : _availableUpdate.HasReleaseManifestHashes
                    ? "静态更新清单 + SHA256SUMS.txt + 下载文件必须一致"
                    : "固定 GitHub Release 地址 + SHA256SUMS.txt + 下载文件必须一致";
            var choice = MessageBox.Show(
                "将把当前" + description + "从 V" + CurrentVersion + " 更新到 V" +
                _availableUpdate.LatestVersion + "。\r\n\r\n" +
                "下载来源：本项目 GitHub Release\r\n" +
                "安装包大小：" + FormatBytes(_availableUpdate.SetupAsset.Size) + "\r\n" +
                "安全校验：" + verification + "\r\n" +
                "本地数据：data 中的设置、主题、诊断和恢复日志全部保留\r\n\r\n" +
                "校验通过后助手会关闭，由独立安装程序覆盖并重新启动。现在继续吗？",
                "确认一键更新", MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.Yes);
            if (choice != MessageBoxResult.Yes)
            {
                SetStatus("已取消更新 · 未下载或执行任何文件", "InfoBrush");
                return;
            }

            _installingUpdate = true;
            SetBusy(true);
            UpdateProgressBar.Value = 0;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "正在建立安全下载…";
            SetStatus("正在下载 V" + _availableUpdate.LatestVersion + " 正式安装包…", "InfoBrush");
            try
            {
                var progress = new Progress<UpdateProgressInfo>(info =>
                {
                    UpdateStatusText.Text = info.Message +
                                            (info.TotalBytes > 0
                                                ? " · " + info.Percentage + "%"
                                                : string.Empty);
                    UpdateProgressBar.Value = info.Percentage;
                });
                var package = await _applicationUpdater.DownloadAndVerifyAsync(
                    _availableUpdate, progress, _lifetime.Token);
                UpdateProgressBar.Value = 100;
                UpdateStatusText.Text = _availableUpdate.HasGitHubAssetDigests
                    ? "多重 SHA-256 校验通过 · 正在交给独立更新程序"
                    : _availableUpdate.HasReleaseManifestHashes
                        ? "静态清单与本机文件校验通过 · 正在启动更新"
                        : "Release 校验文件与安装包 SHA-256 校验通过 · 正在启动更新";
                SetStatus("更新包校验通过 · 正在安全切换版本", "SuccessBrush");
                _applicationUpdater.LaunchVerifiedInstaller(package, CurrentVersion,
                    ThemeManager.Current == AppTheme.Dark ? "dark" : "light");
                Application.Current.Shutdown(0);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _installingUpdate = false;
                SetBusy(false);
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = "更新已停止；未执行未通过校验的文件";
                SetStatus("一键更新未完成 · 当前版本保持不变", "WarningBrush");
                MessageBox.Show("没有执行更新安装包。\r\n\r\n" + ex.Message +
                                "\r\n\r\n你仍可打开 GitHub 发布页手动下载并核对 SHA-256。",
                    "更新提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("无法在程序目录的 data\\diagnostics 文件夹中创建报告：\r\n" + ex.Message,
                    "诊断提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ReleaseButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _availableUpdate != null &&
                           !string.IsNullOrWhiteSpace(_availableUpdate.ReleasePageUrl)
                    ? _availableUpdate.ReleasePageUrl
                    : ReleaseUpdateChecker.ReleasesUrl,
                UseShellExecute = true
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.0") + " KB";
            return (bytes / 1024d / 1024d).ToString("0.0") + " MB";
        }

        private void SetStatus(string message, string resourceKey)
        {
            FooterStatus.Text = message;
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, resourceKey);
        }
    }
}
