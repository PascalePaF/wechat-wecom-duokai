using System;
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
            var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/ThemeResources.xaml", UriKind.Relative)
            });
            ThemeManager.Initialize(ParseForcedTheme(args));
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
