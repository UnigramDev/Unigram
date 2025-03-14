//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Telegram.Native;

namespace Telegram.Common
{
    public static class InactivityHelper
    {
        private static Timer _timer;
        private static int _timeout = 60 * 1000;

        private static int _lastTime = int.MinValue;

        public static void Initialize(int timeout)
        {
            if (ApiInfo.IsDesktop)
            {
                if (timeout > 0)
                {
                    _timeout = timeout * 1000;

                    if (_timer != null)
                    {
                        _timer.Change(0, 1000);
                    }
                    else
                    {
                        _timer = new Timer(OnTick, null, 0, 1000);
                    }
                }
                else if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
            }
        }

        private static void OnTick(object state)
        {
            var lastInput = NativeUtils.GetLastInputTime();
            var idleTime = Environment.TickCount - lastInput;

            if (idleTime >= _timeout && _lastTime < lastInput)
            {
                _lastTime = lastInput;
                Detected?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler Detected;

        public static bool IsActive
        {
            get
            {
                var lastInput = NativeUtils.GetLastInputTime();
                var idleTime = Environment.TickCount - lastInput;

                if (idleTime >= 60 * 1000)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
