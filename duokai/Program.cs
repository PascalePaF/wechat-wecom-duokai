using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
            using (var activation = new EventWaitHandle(false, EventResetMode.AutoReset,
                       SingleInstanceActivation.EventName))
            {
                if (!mutexCreated)
                {
                    if (!autoStartLaunch)
                    {
                        activation.Set();
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
                StartupIntegrationResult startupResult = null;
                try
                {
                    if (ApplicationStorage.IsInstallationPending())
                    {
                        MessageBox.Show("上次安装尚未完成。请重新运行正式安装包以恢复完整程序文件。",
                            "安装需要完成", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return 1;
                    }
                    ApplicationStorage.EnsureReady();
                    var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                    WindowsShellIntegration.NotifyIconChanged(executablePath);
                    var runAtStartup = UserPreferences.LoadRunAtWindowsStartup();
                    var previouslyRegisteredPath = UserPreferences.LoadStartupRegisteredPath();
                    if (!runAtStartup ||
                        new ApplicationUpdateService().DetectCurrentMode() !=
                        ApplicationInstallMode.Unknown)
                    {
                        startupResult = WindowsStartupIntegration.Synchronize(runAtStartup,
                            executablePath, previouslyRegisteredPath);
                        if (startupResult.Succeeded && !startupResult.Conflict &&
                            (startupResult.Changed ||
                             (runAtStartup && !string.Equals(previouslyRegisteredPath,
                                 executablePath, StringComparison.OrdinalIgnoreCase)) ||
                             (!runAtStartup && previouslyRegisteredPath != null)))
                        {
                            UserPreferences.SaveStartupRegistration(runAtStartup,
                                runAtStartup ? executablePath : null);
                        }
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
                if (startupResult != null && (!startupResult.Succeeded || startupResult.Conflict))
                    mainWindow.StartupSynchronizationWarning = startupResult.Message;
                if (autoStartLaunch && UserPreferences.LoadRunAtWindowsStartup() &&
                    UserPreferences.LoadStartMinimizedOnAutoStart())
                {
                    mainWindow.WindowState = WindowState.Minimized;
                }
                var activationListener = SingleInstanceActivation.Register(activation,
                    application.Dispatcher, mainWindow);
                try
                {
                    return application.Run(mainWindow);
                }
                finally
                {
                    activationListener.Unregister(null);
                }
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

    internal static class SingleInstanceActivation
    {
        internal const string EventName = "Local\\WechatDuokai.ControlCenter.Activate";

        internal static RegisteredWaitHandle Register(EventWaitHandle signal,
            Dispatcher dispatcher, Window window)
        {
            return ThreadPool.RegisterWaitForSingleObject(signal, (state, timedOut) =>
            {
                if (!timedOut && !dispatcher.HasShutdownStarted)
                    dispatcher.BeginInvoke(new Action(() => Restore(window)));
            }, null, Timeout.Infinite, false);
        }

        internal static void Restore(Window window)
        {
            if (window == null || !window.IsLoaded) return;
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Show();
            window.Activate();
        }
    }
}
