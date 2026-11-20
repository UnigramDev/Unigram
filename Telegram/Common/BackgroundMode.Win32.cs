//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Services;

namespace Telegram.Common
{
    /// <summary>
    /// The process while it runs for the notification area icon alone, every window closed. It asks
    /// to be scheduled as background work, and once the closed windows have had time to unload it
    /// collects what they left behind and gives the pages back.
    /// </summary>
    public static partial class BackgroundMode
    {
        // Enough for the closed tree's Unloaded cascade, and the releases queued behind it, to run
        // first: until then the collection would find it still rooted.
        private static readonly TimeSpan TrimDelay = TimeSpan.FromSeconds(5);

        // Bumped by every Enter and Leave, so a trim scheduled before a window came back drops out.
        private static int _epoch;

        // Enter and Leave both run on the UI thread, which every window shares.
        private static bool _throttled;

        public static void Enter()
        {
            // EcoQoS applies to every thread in the process, and a call's audio is one of them.
            if (LifetimeService.Current.Voip.ActiveCall != null)
            {
                return;
            }

            Logger.Info();

            var epoch = Interlocked.Increment(ref _epoch);
            SetThrottled(true);

            _ = TrimAsync(epoch);
        }

        public static void Leave()
        {
            Interlocked.Increment(ref _epoch);
            SetThrottled(false);
        }

        private static async Task TrimAsync(int epoch)
        {
            await Task.Delay(TrimDelay).ConfigureAwait(false);

            if (epoch != Volatile.Read(ref _epoch))
            {
                return;
            }

            // Twice, because a wrapper released by the first pass can unroot the next one along.
            // The wait between them is only safe off the UI thread: the releases marshal back to it.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

            if (epoch != Volatile.Read(ref _epoch))
            {
                return;
            }

            // Commit is unchanged and touched pages fault back in; what this changes is that
            // Windows can reclaim the rest without waiting for the app.
            if (!NativeMethods.SetProcessWorkingSetSizeEx(NativeMethods.GetCurrentProcess(), nuint.MaxValue, nuint.MaxValue, 0))
            {
                Logger.Warning(Marshal.GetLastPInvokeError());
            }
            else
            {
                Logger.Info("Trimmed");
            }
        }

        private static unsafe void SetThrottled(bool throttled)
        {
            if (_throttled == throttled)
            {
                return;
            }

            _throttled = throttled;

            // A zero control mask hands the decision back to the system rather than opting out.
            var state = new NativeMethods.PROCESS_POWER_THROTTLING_STATE
            {
                Version = NativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = throttled ? NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
                StateMask = throttled ? NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0
            };

            if (!NativeMethods.SetProcessInformation(NativeMethods.GetCurrentProcess(), NativeMethods.ProcessPowerThrottling, ref state, (uint)sizeof(NativeMethods.PROCESS_POWER_THROTTLING_STATE)))
            {
                Logger.Warning(Marshal.GetLastPInvokeError());
            }
        }

        private static partial class NativeMethods
        {
            public const int ProcessPowerThrottling = 4;

            public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
            public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_POWER_THROTTLING_STATE
            {
                public uint Version;
                public uint ControlMask;
                public uint StateMask;
            }

            [LibraryImport("kernel32.dll")]
            public static partial IntPtr GetCurrentProcess();

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetProcessWorkingSetSizeEx(IntPtr hProcess, nuint dwMinimumWorkingSetSize, nuint dwMaximumWorkingSetSize, uint flags);
        }
    }
}
