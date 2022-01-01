using System;
using System.Threading;
using System.Windows.Forms;

namespace UnigramBridge
{
    static class Program
    {
#if DEBUG
        const string MUTEX_NAME = "TelegramBridgeMutex";
#else
        const string MUTEX_NAME = "UnigramBridgeMutex";
#endif

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!Mutex.TryOpenExisting(MUTEX_NAME, out Mutex mutex))
            {
                mutex = new Mutex(false, MUTEX_NAME);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BridgeApplicationContext());
                mutex.Close();
            }
        }
    }
}
