using System;
using System.Threading;
using System.Windows.Forms;

namespace ClickOCR
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (Has(args, "--selftest"))
            {
                NativeMethods.EnableDpiAwareness();
                return SelfTest.Run();
            }

            if (Has(args, "--demo"))
            {
                NativeMethods.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DemoForm());
                return 0;
            }

            if (args.Length >= 1 && string.Equals(args[0], "--send", StringComparison.OrdinalIgnoreCase))
            {
                string command = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "";
                return IpcClient.Send(command);
            }

            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, "ClickOCR.SingleInstance", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    MessageBox.Show("ClickOCR is already running.\r\nLook for its icon in the notification area.",
                        "ClickOCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Application.Run(new App(Has(args, "--silent")));
            }

            return 0;
        }

        private static bool Has(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string argument in args)
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
