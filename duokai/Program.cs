using System;
using System.Windows.Forms;

using WechatDuokai.UI;

namespace shuangkai
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) =>
                MessageBox.Show(args.Exception.Message, "程序发生错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ThemeManager.Initialize();
            Application.Run(new Home());
        }
    }
}
