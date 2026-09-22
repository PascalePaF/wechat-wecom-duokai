using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using WechatDuokai.Core;
using WechatDuokai.Presentation;

namespace WechatDuokai.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var autoStartLaunch = HasArgument(args, WindowsStartupIntegration.AutoStartArgument);
            WindowsShellIntegration.TrySetCurrentProcessAppUserModelId(ProductIdentity.MainAppUserModelId);
            var mutexCreated = false;
            using (var mutex = new Mutex(true, "Local\\WechatDuokai.ControlCenter", out mutexCreated))
            {
                if (!mutexCreated)
                {
                    if (!autoStartLaunch)
                    {
                        MessageBox.Show("微窗助手已经在运行。", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    return 0;
                }

                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("Themes/ThemeResources.xaml", UriKind.Relative)
                });
                try
                {
                    ApplicationStorage.EnsureReady();
                    var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                    WindowsShellIntegration.NotifyIconChanged(executablePath);
                    var runAtStartup = UserPreferences.LoadRunAtWindowsStartup();
                    if (!runAtStartup ||
                        new ApplicationUpdateService().DetectCurrentMode() !=
                        ApplicationInstallMode.Unknown)
                    {
                        WindowsStartupIntegration.Synchronize(runAtStartup, executablePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法在程序安装目录创建 data 文件夹。\r\n\r\n" +
                                    "请确认当前用户对程序目录具有写入权限：\r\n" +
                                    ApplicationStorage.ApplicationDirectory + "\r\n\r\n" +
                                    ex.Message,
                        "程序目录不可写", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
                ThemeManager.Initialize(ParseForcedTheme(args));
                application.DispatcherUnhandledException += (sender, eventArgs) =>
                {
                    MessageBox.Show("程序遇到问题，但没有修改微信或企业微信文件。\r\n\r\n" + eventArgs.Exception.Message,
                        "运行提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    eventArgs.Handled = true;
                    application.Shutdown(1);
                };

                var mainWindow = new MainWindow();
                if (autoStartLaunch && UserPreferences.LoadRunAtWindowsStartup() &&
                    UserPreferences.LoadStartMinimizedOnAutoStart())
                {
                    mainWindow.WindowState = WindowState.Minimized;
                }
                return application.Run(mainWindow);
            }
        }

        private static AppTheme? ParseForcedTheme(string[] args)
        {
            var themeArgument = args?.FirstOrDefault(value => value.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
            if (themeArgument == null)
            {
                return null;
            }

            return themeArgument.EndsWith("dark", StringComparison.OrdinalIgnoreCase)
                ? AppTheme.Dark
                : AppTheme.Light;
        }

        internal static bool HasArgument(string[] args, string expected)
        {
            return args != null && !string.IsNullOrWhiteSpace(expected) &&
                   args.Any(value => string.Equals(value, expected,
                       StringComparison.OrdinalIgnoreCase));
        }
    }
}
