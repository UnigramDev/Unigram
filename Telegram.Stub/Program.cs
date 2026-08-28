//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Diagnostics;
using Telegram.Common;
using static Telegram.Common.NotifyIcon;
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
                NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

                // The three icons are resources of this executable, addressed by the id
                // NotifyIconIcon carries. The app resolves the same names to files instead.
                _context = new NotifyIcon(ResolveIcon, "Unigram");
                _mutex.ReleaseMutex();
            }
        }

        private const uint IMAGE_ICON = 1;
        private const uint LR_DEFAULTCOLOR = 0x0;
        private const uint LR_DEFAULTSIZE = 0x40;

        private static IntPtr ResolveIcon(NotifyIconIcon icon)
        {
            var hModule = NativeMethods.GetModuleHandle(IntPtr.Zero);
            return NativeMethods.LoadImage(hModule, new IntPtr((int)icon), IMAGE_ICON, 0, 0,
                LR_DEFAULTSIZE | LR_DEFAULTCOLOR);
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
