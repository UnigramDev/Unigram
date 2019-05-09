using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UnigramBridge
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Mutex mutex = null;
            if (!Mutex.TryOpenExisting("UnigramBridgeMutex", out mutex))
            {
                mutex = new Mutex(false, "UnigramBridgeMutex");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BridgeApplicationContext());
                mutex.Close();
            }
        }
    }
}
