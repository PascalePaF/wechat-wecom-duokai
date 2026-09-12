using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using WechatDuokai.UI;

namespace WechatDuokai.Installer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && string.Equals(args[0], "/cleanup-worker", StringComparison.OrdinalIgnoreCase))
            {
                CleanupWorker.Execute(args[1]);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ThemeManager.Initialize();

            var ownName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) ?? string.Empty;
            var uninstallMode = args.Any(arg => string.Equals(arg, "/uninstall", StringComparison.OrdinalIgnoreCase)) ||
                                args.Any(arg => string.Equals(arg, "/cleanup", StringComparison.OrdinalIgnoreCase)) ||
                                ownName.IndexOf("cleanup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                ownName.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                ownName.IndexOf("完全卸载", StringComparison.OrdinalIgnoreCase) >= 0;

            Application.Run(uninstallMode ? (Form)new UninstallForm() : new InstallForm());
        }
    }
}
