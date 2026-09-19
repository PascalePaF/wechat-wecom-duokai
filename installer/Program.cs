using System;
using System.Linq;
using System.Windows;
using WechatDuokai.Presentation;
using WechatDuokai.Update;

namespace WechatDuokai.Installer
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            WindowsShellIntegration.TrySetCurrentProcessAppUserModelId(ProductIdentity.SetupAppUserModelId);
            var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/ThemeResources.xaml", UriKind.Relative)
            });
            ThemeManager.Initialize(ParseForcedTheme(args));
            application.DispatcherUnhandledException += (sender, eventArgs) =>
            {
                MessageBox.Show("安装程序遇到未预期问题，已停止继续操作。\r\n\r\n" + eventArgs.Exception.Message,
                    "安装程序已停止", MessageBoxButton.OK, MessageBoxImage.Error);
                eventArgs.Handled = true;
                application.Shutdown(1);
            };
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "/auto-update", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var plan = UpdatePlan.Read(args[1]);
                    return application.Run(new InstallWindow(plan, args[1]));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("更新计划未通过验证，未修改任何程序文件。\r\n\r\n" + ex.Message,
                        "无法开始更新", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 2;
                }
            }

            return application.Run(new InstallWindow());
        }

        private static AppTheme? ParseForcedTheme(string[] args)
        {
            var value = args?.FirstOrDefault(arg => arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
            if (value == null) return null;
            return value.EndsWith("dark", StringComparison.OrdinalIgnoreCase) ? AppTheme.Dark : AppTheme.Light;
        }
    }

    internal static class InstallFlowPolicy
    {
        internal static bool RequiresExplicitLaunchConfirmation => true;

        internal static bool CanLaunch(bool installationCompleted, bool confirmationClicked)
        {
            return installationCompleted && confirmationClicked;
        }
    }
}
