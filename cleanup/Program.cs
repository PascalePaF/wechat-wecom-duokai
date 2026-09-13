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

            var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("Themes/ThemeResources.xaml", UriKind.Relative)
            });
            WechatDuokai.Presentation.ThemeManager.Initialize(ParseForcedTheme(args));
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
