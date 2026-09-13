using System;
using System.IO;
using System.Linq;
using System.Windows;
using WechatDuokai.Presentation;

namespace WechatDuokai.Installer
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && string.Equals(args[0], "/cleanup-worker", StringComparison.OrdinalIgnoreCase))
            {
                CleanupWorker.Execute(args[1]);
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
            ThemeManager.Initialize(ParseForcedTheme(args));

            var ownName = Path.GetFileNameWithoutExtension(System.Windows.Forms.Application.ExecutablePath) ?? string.Empty;
            var uninstallMode = args.Any(arg => string.Equals(arg, "/uninstall", StringComparison.OrdinalIgnoreCase)) ||
                                args.Any(arg => string.Equals(arg, "/cleanup", StringComparison.OrdinalIgnoreCase)) ||
                                ownName.IndexOf("cleanup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                ownName.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                ownName.IndexOf("完全卸载", StringComparison.OrdinalIgnoreCase) >= 0;

            Window window = uninstallMode ? (Window)new UninstallWindow() : new InstallWindow();
            return application.Run(window);
        }

        private static AppTheme? ParseForcedTheme(string[] args)
        {
            var value = args?.FirstOrDefault(arg => arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
            if (value == null)
            {
                return null;
            }

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
