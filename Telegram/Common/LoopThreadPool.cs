//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Telegram.Common
{
    public partial class LoopThreadPool
    {
        private static readonly ConcurrentDictionary<TimeSpan, LoopThread> _specificFrameRate = new();

        private static readonly MultiValueDictionary<TimeSpan, LoopThread> _test = new();
        private static readonly object _lock = new();

        public static LoopThread Rent(TimeSpan frameRate)
        {
            LoopThread target = null;

            lock (_lock)
            {
                _test.TryGetValue(frameRate, out var threads);
                _test[frameRate] = threads ??= new List<LoopThread>();

                for (int i = 0; i < threads.Count; i++)
                {
                    var thread = threads[i];
                    if (thread.Count == 0)
                    {
                        if (target == null)
                        {
                            target = thread;
                        }
                        else
                        {
                            threads.RemoveAt(i);
                            i--;
                        }
                    }
                    else if (thread.HasMoreResources)
                    {
                        target = thread;
                    }
                }

                if (target == null)
                {
                    target = new LoopThread(frameRate);
                    threads.Add(target);
                }

                target.Rent();
            }

            return target;
        }

        public static void Release(LoopThread target)
        {
            if (target == null)
            {
                return;
            }

            lock (_lock)
            {
                target.Release();

                if (target.Count == 0 && _test.TryGetValue(target.Interval, out var threads))
                {
                    threads.Remove(target);
                }
            }
        }

        public static LoopThread Get2(TimeSpan frameRate)
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
