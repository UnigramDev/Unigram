using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.System.Profile;

namespace Unigram.Common
{
    public static class InactivityHelper
    {
        [DllImport("user32.dll")]
        public static extern Boolean GetLastInputInfo(ref LASTINPUTINFO plii);
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public Int32 dwTime;
        }

        private static Timer _timer;
        private static int _timeout = 60 * 1000;

        private static int _lastTime;

        public static void Initialize(int timeout)
        {
            if (AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Desktop"))
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
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            lastInput.dwTime = 0;

            if (GetLastInputInfo(ref lastInput))
            {
                var idleTime = Environment.TickCount - lastInput.dwTime;
                if (idleTime >= _timeout && _lastTime < lastInput.dwTime)
                {
                    _lastTime = lastInput.dwTime;
                    Detected?.Invoke(null, EventArgs.Empty);
                }

            }
        }

        public static event EventHandler Detected;

        public static bool IsActive
        {
            get
            {
                if (AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Desktop"))
                {
                    LASTINPUTINFO lastInput = new LASTINPUTINFO();
                    lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
                    lastInput.dwTime = 0;

                    if (GetLastInputInfo(ref lastInput))
                    {
                        var idleTime = Environment.TickCount - lastInput.dwTime;
                        if (idleTime >= 60 * 1000)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }
}
