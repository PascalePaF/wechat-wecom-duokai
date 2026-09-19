using System;
using System.Linq;
using System.Windows;
using WechatDuokai.Presentation;

namespace WechatDuokai.Cleanup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && string.Equals(args[0], "/cleanup-worker", StringComparison.OrdinalIgnoreCase))
            {
                WechatDuokai.Installer.CleanupWorker.Execute(args[1]);
                return 0;
            }

            WechatDuokai.Presentation.WindowsShellIntegration.TrySetCurrentProcessAppUserModelId(
                WechatDuokai.Presentation.ProductIdentity.CleanupAppUserModelId);
            var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/ThemeResources.xaml", UriKind.Relative)
            });
            WechatDuokai.Presentation.ThemeManager.Initialize(ParseForcedTheme(args));
            application.DispatcherUnhandledException += (sender, eventArgs) =>
            {
                MessageBox.Show("清理程序遇到未预期问题，已停止继续操作。\r\n\r\n" + eventArgs.Exception.Message,
                    "清理程序已停止", MessageBoxButton.OK, MessageBoxImage.Error);
                eventArgs.Handled = true;
                application.Shutdown(1);
            };
            return application.Run(new WechatDuokai.Installer.UninstallWindow());
        }

        private static WechatDuokai.Presentation.AppTheme? ParseForcedTheme(string[] args)
        {
            var value = args?.FirstOrDefault(arg => arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
            if (value == null) return null;
            return value.EndsWith("dark", StringComparison.OrdinalIgnoreCase)
                ? WechatDuokai.Presentation.AppTheme.Dark
                : WechatDuokai.Presentation.AppTheme.Light;
        }
    }
}
