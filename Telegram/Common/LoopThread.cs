//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;

namespace Telegram.Common
{
    public partial class LoopThread
    {
        private TimeSpan _interval;

        private readonly object _timerLock = new();
        private readonly Timer _timer;

        public TimeSpan Interval => _interval;

        public LoopThread(TimeSpan interval)
        {
            _interval = interval;
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        [ThreadStatic]
        private static LoopThread _chats;
        public static LoopThread Chats => _chats ??= new LoopThread(TimeSpan.FromMilliseconds(1000 / 60));

        private void OnTick(object state)
        {
            lock (_timerLock)
            {
                _tick?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Rent()
        {
            _count++;
        }

        public void Release()
        {
            _count--;
        }

        private int _count;
        public int Count => _count;

        public bool HasMoreResources => _count < 120;

        private event EventHandler _tick;
        public event EventHandler Tick
        {
            add
            {
                lock (_timerLock)
                {
                    if (_tick == null) _timer.Change(TimeSpan.Zero, _interval);
                    _tick += value;
                }
            }
            remove
            {
                lock (_timerLock)
                {
                    _tick -= value;
                    if (_tick == null) _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }
    }
}
