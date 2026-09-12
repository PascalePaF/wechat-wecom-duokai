using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using shuangkai.Core;
using WechatDuokai.UI;

namespace shuangkai
{
    public partial class Home : PremiumForm
    {
        private const int MinimumTargetCount = 1;
        private const int MaximumTargetCount = 10;

        private readonly InstanceManager _instanceManager = new InstanceManager();
        private AppDefinition _wechat;
        private AppDefinition _wecom;
        private bool _launchInProgress;
        private bool _updatingTargetCount;

        public Home()
        {
            InitializeComponent();
        }

        private void Home_Load(object sender, EventArgs e)
        {
            var cachedCount = Math.Max(MinimumTargetCount,
                Math.Min(MaximumTargetCount, UserPreferences.LoadTargetCount()));
            SetTargetCount(cachedCount, false);

            ReloadApplications();
            RefreshInstanceStatus();
            statusTimer.Start();
        }

        private void ReloadApplications()
        {
            _wechat = ApplicationLocator.FindWeChat();
            _wecom = ApplicationLocator.FindWeCom();

            ConfigureApplicationRow(_wechat, wechatStartButton, wechatPathLabel, wechatIcon);
            ConfigureApplicationRow(_wecom, wecomStartButton, wecomPathLabel, wecomIcon);
        }

        private void ConfigureApplicationRow(AppDefinition application, Button actionButton, Label pathLabel, PictureBox icon)
        {
            var available = application != null && application.IsAvailable;
            actionButton.Enabled = available;
            icon.Enabled = available;

            if (available)
            {
                pathLabel.Text = ShortenPath(application.ExecutablePath, 48);
                pathToolTip.SetToolTip(pathLabel, application.ExecutablePath);
                pathToolTip.SetToolTip(icon, $"点击启动或补足{application.DisplayName}");
            }
            else
            {
                pathLabel.Text = "未检测到安装路径";
                pathToolTip.SetToolTip(pathLabel, "请先安装官方客户端，然后重新打开本工具。该工具不会下载或替换客户端。");
            }
        }

        private void targetCountInput_TextChanged(object sender, EventArgs e)
        {
            if (_updatingTargetCount)
            {
                return;
            }

            int targetCount;
            if (TryGetTargetCount(out targetCount))
            {
                targetCountInput.ForeColor = UiPalette.Ivory;
                TrySaveSettings(targetCount);
            }
            else
            {
                targetCountInput.ForeColor = UiPalette.Warning;
            }
        }

        private void targetCountInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }

            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                NormalizeTargetCount();
                SelectNextControl(targetCountInput, true, true, true, true);
            }
        }

        private void targetCountInput_Leave(object sender, EventArgs e)
        {
            NormalizeTargetCount();
        }

        private void decreaseButton_Click(object sender, EventArgs e)
        {
            SetTargetCount(Math.Max(MinimumTargetCount, GetTargetCount() - 1), true);
        }

        private void increaseButton_Click(object sender, EventArgs e)
        {
            SetTargetCount(Math.Min(MaximumTargetCount, GetTargetCount() + 1), true);
        }

        private void wechatStartButton_Click(object sender, EventArgs e)
        {
            StartOrRestoreAsync(_wechat);
        }

        private void wecomStartButton_Click(object sender, EventArgs e)
        {
            StartOrRestoreAsync(_wecom);
        }

        private void wechatIcon_Click(object sender, EventArgs e)
        {
            StartOrRestoreAsync(_wechat);
        }

        private void wecomIcon_Click(object sender, EventArgs e)
        {
            StartOrRestoreAsync(_wecom);
        }

        private async void StartOrRestoreAsync(AppDefinition application)
        {
            if (_launchInProgress)
            {
                return;
            }

            if (application == null || !application.IsAvailable)
            {
                MessageBox.Show("没有检测到对应的官方客户端，请先完成客户端安装。", "未找到程序",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _launchInProgress = true;
            SetActionsEnabled(false);
            SetTargetControlsEnabled(false);
            statusTimer.Stop();

            try
            {
                var targetCount = GetTargetCount();
                TrySaveSettings(targetCount);

                var result = await _instanceManager.EnsureTargetCountAsync(
                    application,
                    targetCount,
                    message => BeginInvoke(new Action(() => SetStatus(message, UiPalette.Blue))),
                    CancellationToken.None);

                SetStatus(result.Message, result.Success ? UiPalette.Green : UiPalette.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("启动失败：" + ex.Message, UiPalette.Danger);
                MessageBox.Show(ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _launchInProgress = false;
                SetTargetControlsEnabled(true);
                SetActionsEnabled(true);
                RefreshInstanceStatus();
                statusTimer.Start();
            }
        }

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            if (!_launchInProgress)
            {
                RefreshInstanceStatus();
            }
        }

        private void RefreshInstanceStatus()
        {
            SetApplicationStatus(_wechat, wechatCountLabel);
            SetApplicationStatus(_wecom, wecomCountLabel);
        }

        private void SetApplicationStatus(AppDefinition application, Label countLabel)
        {
            if (application == null || !application.IsAvailable)
            {
                countLabel.Text = "不可用";
                countLabel.ForeColor = UiPalette.MutedDark;
                return;
            }

            var count = _instanceManager.GetInstanceCount(application);
            countLabel.Text = count == 0 ? "当前未运行" : $"当前运行 {count} 个实例";
            countLabel.ForeColor = count == 0
                ? UiPalette.Muted
                : UiPalette.Green;
        }

        private void SetActionsEnabled(bool enabled)
        {
            wechatStartButton.Enabled = enabled && _wechat != null && _wechat.IsAvailable;
            wechatIcon.Enabled = wechatStartButton.Enabled;
            wecomStartButton.Enabled = enabled && _wecom != null && _wecom.IsAvailable;
            wecomIcon.Enabled = wecomStartButton.Enabled;
        }

        private void SetStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = color;
            statusDotLabel.ForeColor = color;
        }

        private void sourceLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/PascalePaF/wechat-wecom-duokai",
                UseShellExecute = true
            });
        }

        private void minimizeButton_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Home_FormClosing(object sender, FormClosingEventArgs e)
        {
            TrySaveSettings(GetTargetCount());
        }

        private void SetTargetControlsEnabled(bool enabled)
        {
            targetCountInput.Enabled = enabled;
            decreaseButton.Enabled = enabled;
            increaseButton.Enabled = enabled;
        }

        private bool TryGetTargetCount(out int targetCount)
        {
            return int.TryParse(targetCountInput.Text, out targetCount) &&
                   targetCount >= MinimumTargetCount &&
                   targetCount <= MaximumTargetCount;
        }

        private int GetTargetCount()
        {
            int targetCount;
            return TryGetTargetCount(out targetCount)
                ? targetCount
                : Math.Max(MinimumTargetCount,
                    Math.Min(MaximumTargetCount, UserPreferences.LoadTargetCount()));
        }

        private void NormalizeTargetCount()
        {
            int parsed;
            if (!int.TryParse(targetCountInput.Text, out parsed))
            {
                parsed = UserPreferences.LoadTargetCount();
            }

            SetTargetCount(Math.Max(MinimumTargetCount, Math.Min(MaximumTargetCount, parsed)), true);
        }

        private void SetTargetCount(int targetCount, bool save)
        {
            targetCount = Math.Max(MinimumTargetCount, Math.Min(MaximumTargetCount, targetCount));
            _updatingTargetCount = true;
            targetCountInput.Text = targetCount.ToString();
            targetCountInput.ForeColor = UiPalette.Ivory;
            _updatingTargetCount = false;

            if (save)
            {
                TrySaveSettings(targetCount);
            }
        }

        private static void TrySaveSettings(int targetCount)
        {
            try
            {
                UserPreferences.SaveTargetCount(targetCount);
            }
            catch (Exception)
            {
                // Failure to persist a preference must not prevent launching the clients.
            }
        }

        private static string ShortenPath(string path, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length <= maximumLength)
            {
                return path ?? string.Empty;
            }

            var fileName = Path.GetFileName(path);
            var availablePrefix = Math.Max(6, maximumLength - fileName.Length - 4);
            return path.Substring(0, Math.Min(availablePrefix, path.Length)) + "…\\" + fileName;
        }
    }
}
