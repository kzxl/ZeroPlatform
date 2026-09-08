using System;
using System.Windows.Forms;

namespace ZeroPlatform.Samples.Showcase
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
            Application.Run(new ShowcaseForm());
        }
    }
}
