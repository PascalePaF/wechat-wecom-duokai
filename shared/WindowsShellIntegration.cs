using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WechatDuokai.Presentation
{
    internal static class ProductIdentity
    {
        internal const string Name = "微窗助手";
        internal const string MainAppUserModelId = "PascalePaF.WechatDuokai";
        internal const string SetupAppUserModelId = "PascalePaF.WechatDuokai.Setup";
        internal const string CleanupAppUserModelId = "PascalePaF.WechatDuokai.Cleanup";

        internal static string GetVersion(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var version = assembly.GetName().Version;
            if (version == null || version.Major < 0 || version.Minor < 0 || version.Build < 0)
            {
                throw new InvalidOperationException("程序版本信息无效。");
            }

            return version.Major + "." + version.Minor + "." + version.Build;
        }
    }

    internal static class WindowsShellIntegration
    {
        private const uint ShellEventUpdateItem = 0x00002000;
        private const uint ShellEventAssociationsChanged = 0x08000000;
        private const uint ShellNotifyIdList = 0x0000;
        private const uint ShellNotifyPathW = 0x0005;
        private const uint ShellNotifyFlushNoWait = 0x2000;

        internal static bool TrySetCurrentProcessAppUserModelId(string appUserModelId)
        {
            if (string.IsNullOrWhiteSpace(appUserModelId)) return false;
            try
            {
                return SetCurrentProcessExplicitAppUserModelID(appUserModelId) >= 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        internal static void NotifyIconChanged(params string[] paths)
        {
            try
            {
                if (paths != null)
                {
                    foreach (var path in paths)
                    {
                        if (string.IsNullOrWhiteSpace(path)) continue;
                        var fullPath = Path.GetFullPath(path);
                        SHChangeNotify(ShellEventUpdateItem,
                            ShellNotifyPathW | ShellNotifyFlushNoWait, fullPath, IntPtr.Zero);
                    }
                }

                // Explorer can retain the previous icon for the same executable path. This
                // notification invalidates that association cache without restarting Explorer.
                SHChangeNotify(ShellEventAssociationsChanged,
                    ShellNotifyIdList | ShellNotifyFlushNoWait, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception)
            {
                // Icon refresh is best effort and must never turn a valid install into a failure.
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(uint eventId, uint flags,
            [MarshalAs(UnmanagedType.LPWStr)] string item1, IntPtr item2);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags,
            IntPtr item1, IntPtr item2);
    }
}
