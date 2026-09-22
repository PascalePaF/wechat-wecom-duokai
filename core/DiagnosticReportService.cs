using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WechatDuokai.Core
{
    public sealed class DiagnosticReportService
    {
        public const string FolderName = "diagnostics";

        public string CreateReport(string applicationDirectory, AppDefinition weChat, AppDefinition weCom,
            InstanceManager instanceManager)
        {
            if (string.IsNullOrWhiteSpace(applicationDirectory))
            {
                throw new InvalidOperationException("无法确定程序所在目录。");
            }

            var root = Path.GetFullPath(applicationDirectory);
            var dataDirectory = Path.Combine(root, ApplicationStorage.DataFolderName);
            var folder = Path.Combine(dataDirectory, FolderName);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "diagnostic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>
            {
                "微窗助手 本地诊断报告",
                "生成时间=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                "助手版本=" + GetVersion(),
                "操作系统=" + Environment.OSVersion.VersionString,
                "系统位数=" + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
                "进程位数=" + (Environment.Is64BitProcess ? "64-bit" : "32-bit"),
                ".NET=" + Environment.Version,
                "联网说明=本报告只写入本地；不会自动上传",
                "开机自动启动设置=" + (UserPreferences.LoadRunAtWindowsStartup(dataDirectory) ? "开启" : "关闭"),
                "自动启动时最小化=" + (UserPreferences.LoadStartMinimizedOnAutoStart(dataDirectory) ? "开启" : "关闭"),
                "每日版本检查=" + (UserPreferences.LoadAutoCheckForUpdates(dataDirectory) ? "开启" : "关闭"),
                "更新包自动下载=" + (UserPreferences.LoadAutoDownloadUpdates(dataDirectory) ? "开启；安装仍需确认" : "关闭"),
                string.Empty
            };

            AppendClient(lines, "微信", weChat, AppKind.WeChat, instanceManager);
            lines.Add(string.Empty);
            AppendClient(lines, "企业微信", weCom, AppKind.WeCom, instanceManager);
            lines.Add(string.Empty);
            lines.Add("底层策略=精确进程路径 + 当前会话 + 已知互斥锁/lock.ini 白名单");
            lines.Add("企业微信双开策略=启动期间临时使用官方 multi_instances=2 提示；仅在临时值未被外部改动时恢复原值");
            lines.Add("企业微信三开及以上策略=启动期间临时移除双开提示并释放已知独占互斥锁；外部新值优先保留");
            lines.Add("注册表冲突保护=结束会话前比较当前值、类型与助手临时状态；不匹配时跳过恢复，避免覆盖第三方新设置");
            lines.Add("注册表断电恢复=修改前将原始状态与预期临时状态原子写入 data\\recovery；下次启动仅在临时状态仍归助手所有时恢复");
            lines.Add("注册表恢复审计=data\\logs\\registry-recovery.log；只记录事务结果，不记录聊天、账号或凭据");
            lines.Add("企业微信扩展模式实测基线=WXWork 5.0.11.6018 已验证 1→2→3；其他版本需逐版验证");
            lines.Add("程序数据目录=" + Path.Combine(root, ApplicationStorage.DataFolderName));
            AppendRecoveryState(lines, root);
            lines.Add("诊断目录=" + folder);

            File.WriteAllLines(path, lines, new UTF8Encoding(true));
            return path;
        }

        private static void AppendRecoveryState(ICollection<string> lines, string applicationRoot)
        {
            var recovery = Path.Combine(applicationRoot, ApplicationStorage.DataFolderName, "recovery");
            try
            {
                lines.Add("注册表活动恢复事务=" +
                          (File.Exists(Path.Combine(recovery, "wecom-registry-transaction.ini")) ? "存在" : "无"));
                lines.Add("注册表隔离恢复日志=" +
                          (Directory.Exists(recovery)
                              ? Directory.EnumerateFiles(recovery,
                                  "wecom-registry-transaction.ini.invalid-*", SearchOption.TopDirectoryOnly).Count()
                              : 0));
            }
            catch (Exception)
            {
                lines.Add("注册表恢复日志状态=无法读取");
            }
        }

        private static void AppendClient(ICollection<string> lines, string label, AppDefinition app,
            AppKind kind, InstanceManager instanceManager)
        {
            lines.Add("[" + label + "]");
            if (app == null || !app.IsAvailable)
            {
                lines.Add("状态=未找到有效官方客户端");
                return;
            }

            var validation = ClientExecutableValidator.Validate(app.ExecutablePath, kind);
            lines.Add("路径=" + app.ExecutablePath);
            lines.Add("产品=" + (validation.ProductName ?? string.Empty));
            lines.Add("版本=" + (validation.FileVersion ?? string.Empty));
            lines.Add("腾讯签名=" + (validation.IsValid ? "有效" : "未通过"));
            lines.Add("检测实例=" + instanceManager.GetInstanceCount(app));
        }

        private static string GetVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version == null ? "未知" : version.ToString(3);
        }
    }
}
