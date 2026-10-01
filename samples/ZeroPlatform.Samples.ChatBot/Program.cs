using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZeroPlatform.Samples.ChatBot
{
    internal static class Program
    {
        [STAThread]
        private static async Task Main(string[] args)
        {
            if (args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase) || a.Equals("-c", StringComparison.OrdinalIgnoreCase)))
            {
                await ChatBotCli.RunInteractiveAsync();
                return;
            }

#if NETCOREAPP || NET5_0_OR_GREATER
            ApplicationConfiguration.Initialize();
#else
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#endif
            Application.Run(new ChatBotForm());
        }
    }
}
