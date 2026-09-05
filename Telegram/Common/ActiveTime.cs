//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Diagnostics;

namespace Telegram.Common
{
    /// <summary>
    /// How long the app has been in front of someone, and what the collector did in that time.
    ///
    /// Wall clock since launch is the wrong denominator for a pause percentage: most of a session
    /// is spent in the background, where nothing renders and nobody is there to see a stutter, so
    /// dividing by it turns any freeze into a rounding error. Both halves have to be scoped, not
    /// just the denominator - counting every pause against only the time a window was up would be
    /// wrong in the other direction.
    /// </summary>
    public class ActiveTime
    {
        /// <summary>
        /// A window has focus: the app is the one being used.
        /// </summary>
        public static readonly ActiveTime Focused = new();

        /// <summary>
        /// A window is on screen, focused or not. That is when XAML renders, so it is both when a
        /// pause is something anyone could notice and when the framework is asking for collections
        /// in the first place.
        /// </summary>
        public static readonly ActiveTime Visible = new();

        private readonly Stopwatch _elapsed = new();
        private readonly object _lock = new();

        private int _count;
        private TimeSpan _pausedAtEnter;
        private TimeSpan _paused;

        /// <summary>
        /// Windows come and go independently and their intervals overlap, so this counts rather
        /// than toggles: the clock runs while at least one of them is up.
        /// </summary>
        public void Set(bool active)
        {
            lock (_lock)
            {
                if (active)
                {
                    if (_count++ == 0)
                    {
                        _pausedAtEnter = TotalPause;
                        _elapsed.Start();
                    }
                }
                else if (_count > 0 && --_count == 0)
                {
                    _elapsed.Stop();
                    _paused += TotalPause - _pausedAtEnter;
                }
            }
        }

        public TimeSpan Elapsed
        {
            get
            {
                lock (_lock)
                {
                    return _elapsed.Elapsed;
                }
            }
        }

        /// <summary>
        /// Covers the interval in progress as well, so a readout taken with the window up runs to
        /// the moment it is read - the same as <see cref="Elapsed"/>.
        /// </summary>
        public TimeSpan Paused
        {
            get
            {
                lock (_lock)
                {
                    return _count > 0
                        ? _paused + (TotalPause - _pausedAtEnter)
                        : _paused;
                }
            }
        }

        private static TimeSpan TotalPause
        {
            get
            {
#if NET9_0_OR_GREATER
                // 4ns, so there is no reason to cache it or to sample it any less often.
                return GC.GetTotalPauseDuration();
#else
                return TimeSpan.Zero;
#endif
            }
        }
    }
}
