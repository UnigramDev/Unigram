//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Diagnostics;
using Windows.ApplicationModel;

namespace Telegram.Stub
{
    static class Program
    {
#if DEBUG
        const string MUTEX_NAME = "TelegramBridgeMutexV2";
#else
        const string MUTEX_NAME = "UnigramBridgeMutexV2";
#endif

        private static readonly Mutex _mutex = new Mutex(true, MUTEX_NAME);

        private static NotifyIcon? _context;

        [STAThread]
        public static void Main(string[] args)
        {
            AddLoopbackExemption();

            if (args.Contains("/LoopbackExempt"))
            {
                return;
            }

            if (_mutex.WaitOne(0, true))
            {
                _context = new NotifyIcon();
                _mutex.ReleaseMutex();
            }
        }

        private static void AddLoopbackExemption()
        {
            var familyName = Package.Current.Id.FamilyName;
            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = "CheckNetIsolation.exe",
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "LoopbackExempt -a -n=" + familyName
            };

            try
            {
                Process? process = Process.Start(info);
                process?.WaitForExit();
                process?.Dispose();
            }
            catch { }
        }
    }
}
