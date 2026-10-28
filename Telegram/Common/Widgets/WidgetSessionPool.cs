//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading;
using Windows.UI.Composition;

namespace Telegram.Common.Widgets
{
    // Widgets whose bubble went away, kept paused for a while. The list recycles a container as soon as
    // its message leaves the realized range; without this, scrolling back booted the widget again from
    // scratch, blank and resizing until it had loaded. UI thread, apart from the expiry timer.
    public static class WidgetSessionPool
    {
        private const int Capacity = 8;
        private const int LifetimeMs = 30000;

        private static readonly object _lock = new();
        // Oldest first.
        private static readonly List<(WidgetSession Session, ulong Parked)> _entries = new();
        private static Timer _timer;

        public static void Park(WidgetSession session)
        {
            session.View.SetVisible(false);

            WidgetSession evicted = null;
            lock (_lock)
            {
                _entries.Add((session, Logger.TickCount));

                if (_entries.Count > Capacity)
                {
                    evicted = _entries[0].Session;
                    _entries.RemoveAt(0);
                }

                _timer ??= new Timer(OnTimer, null, LifetimeMs, LifetimeMs);
            }

            evicted?.CloseInBackground();
        }

        // The parked widget of this message and HTML, drawing through this compositor (one per window).
        public static WidgetSession Take(WidgetKey key, ulong hash, Compositor compositor)
        {
            lock (_lock)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    var session = _entries[i].Session;
                    if (session.Key.Equals(key) && session.Hash == hash && session.View.Visual.Compositor == compositor)
                    {
                        _entries.RemoveAt(i);
                        return session;
                    }
                }
            }

            return null;
        }

        private static void OnTimer(object state)
        {
            List<WidgetSession> expired = null;
            lock (_lock)
            {
                var now = Logger.TickCount;
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (now - _entries[i].Parked >= LifetimeMs)
                    {
                        (expired ??= new()).Add(_entries[i].Session);
                        _entries.RemoveAt(i);
                    }
                }

                if (_entries.Count == 0)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
            }

            if (expired != null)
            {
                foreach (var session in expired)
                {
                    session.CloseInBackground();
                }
            }
        }
    }
}
