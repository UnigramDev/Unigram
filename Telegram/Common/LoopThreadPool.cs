//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;

namespace Telegram.Common
{
    public class LoopThreadPool
    {
        private static readonly ConcurrentDictionary<TimeSpan, LoopThread> _specificFrameRate = new();

        public static LoopThread Get(TimeSpan frameRate)
        {
            if (_specificFrameRate.TryGetValue(frameRate, out LoopThread thread))
            {
                return thread;
            }

            thread = new LoopThread(frameRate);
            _specificFrameRate[frameRate] = thread;

            return thread;
        }

        private readonly LoopThread[] _pool;
        private int _lastUsed = -1;

        public LoopThreadPool(int count, TimeSpan interval)
        {
            _pool = new LoopThread[count];

            for (int i = 0; i < count; i++)
            {
                _pool[i] = new LoopThread(interval);
            }
        }

        [ThreadStatic]
        private static LoopThreadPool _animations;
        public static LoopThreadPool Animations => _animations ??= new LoopThreadPool(3, TimeSpan.FromMilliseconds(1000 / 30));

        [ThreadStatic]
        private static LoopThreadPool _stickers;
        public static LoopThreadPool Stickers => _stickers ??= new LoopThreadPool(2, TimeSpan.FromMilliseconds(1000 / 30));

        public LoopThread Get()
        {
            _lastUsed++;

            if (_lastUsed >= _pool.Length)
            {
                _lastUsed = 0;
            }

            return _pool[_lastUsed];
        }
    }
}
