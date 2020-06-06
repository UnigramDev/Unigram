using System;

namespace Unigram.Common
{
    public class LoopThreadPool
    {
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
