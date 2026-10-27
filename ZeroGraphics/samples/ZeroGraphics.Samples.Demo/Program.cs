using System;
using System.Windows.Forms;

namespace ZeroGraphics.Samples.Demo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
#if NETCOREAPP || NET5_0_OR_GREATER
            ApplicationConfiguration.Initialize();
#else
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#endif
            Application.Run(new MainDemoForm());
        }
    }
}
