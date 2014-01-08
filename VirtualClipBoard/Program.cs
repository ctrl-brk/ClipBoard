using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using VirtualClipBoard.Properties;

namespace VirtualClipBoard
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!String.IsNullOrEmpty(Settings.Default.Culture))
            {
                var ci = new CultureInfo(Settings.Default.Culture);
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VirtualClipBoard());
        }
    }
}
