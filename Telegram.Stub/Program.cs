//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Windows.Storage;

namespace Telegram.Stub
{
    static class Program
    {
#if DEBUG
        const string MUTEX_NAME = "TelegramBridgeMutexV2";
#else
        const string MUTEX_NAME = "UnigramBridgeMutexV2";
#endif

        static readonly Mutex _mutex = new Mutex(true, MUTEX_NAME);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (_mutex.WaitOne(0, true))
            {
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BridgeApplicationContext());

                _mutex.ReleaseMutex();
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            File.WriteAllText(ApplicationData.Current.LocalFolder.Path + "\\stub.txt", e.Exception.ToString());
            Application.Exit();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            File.WriteAllText(ApplicationData.Current.LocalFolder.Path + "\\stub.txt", e.ExceptionObject.ToString());
            Application.Exit();
        }
    }
}
