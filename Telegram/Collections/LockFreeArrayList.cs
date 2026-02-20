//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Buffers;
using System.Threading;

namespace Telegram.Collections
{
    // Tailored for AnimationScheduler
    public sealed class LockFreeArrayList<T> where T : class
    {
        private readonly T[] _slots;
        private readonly int _capacity;

        private int _count;
        private int _tail;
        private int _version;

        public LockFreeArrayList(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _slots = new T[capacity];
            _count = 0;
            _tail = 0;
            _version = 0;
        }

        public bool Add(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            // pick a starting probe index using a monotonic counter to reduce contention
            int start = Interlocked.Increment(ref _tail) - 1;
            int cap = _capacity;

            for (int i = 0; i < cap; i++)
            {
                int idx = (start + i) % cap;

                // Try to install into an empty slot (null)
                // If slot was null, CompareExchange returns null -> success
                if (Interlocked.CompareExchange(ref _slots[idx], item, null) == null)
                {
                    Interlocked.Increment(ref _count);
                    Interlocked.Increment(ref _version); // bump version for snapshots
                    return true;
                }
            }

            // full
            return false;
        }

        public bool Remove(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            int cap = _capacity;

            for (int i = 0; i < cap; i++)
            {
                // Read current slot
                T cur = Volatile.Read(ref _slots[i]);
                if (!ReferenceEquals(cur, item)) continue;

                // Try to CAS it out to null
                if (Interlocked.CompareExchange(ref _slots[i], null, cur) == cur)
                {
                    Interlocked.Decrement(ref _count);
                    Interlocked.Increment(ref _version);
                    return true;
                }
                // otherwise someone else changed it; continue scanning
            }

            return false;
        }

        public int Count => Volatile.Read(ref _count);

        public int Capacity => _capacity;

        public bool TrySnapshot(out T[] rentedBuffer, out int length)
        {
            // rent at least capacity; caller can return to pool after use
            rentedBuffer = ArrayPool<T>.Shared.Rent(_capacity);
            length = 0;

            // read version before starting
            int v1 = Volatile.Read(ref _version);

            // copy all non-null references
            int cap = _capacity;
            for (int i = 0; i < cap; i++)
            {
                T item = Volatile.Read(ref _slots[i]);
                if (item != null)
                {
                    rentedBuffer[length++] = item;
                }
            }

            // read version after copy
            int v2 = Volatile.Read(ref _version);

            if (v1 != v2)
            {
                // concurrent modification happened; return buffer and signal failure.
                // clear contents optionally (avoid keeping refs) - ArrayPool return supports clearing.
                try
                {
                    ArrayPool<T>.Shared.Return(rentedBuffer, clearArray: true);
                }
                catch
                {
                    // ignore pool return exceptions; set to null anyway
                }

                rentedBuffer = null;
                length = 0;
                return false;
            }

            // success; caller gets the rentedBuffer and length, must return it to pool.
            return true;
        }

        public LockFreeArrayList<T> SplitHalf()
        {
            int currentCount = Count;
            if (currentCount == 0)
                return new LockFreeArrayList<T>(_capacity);

            int toMove = currentCount / 2;
            if (toMove == 0)
                return new LockFreeArrayList<T>(_capacity);

            var newList = new LockFreeArrayList<T>(_capacity);

            int moved = 0;
            for (int i = 0; i < _capacity && moved < toMove; i++)
            {
                T cur = Volatile.Read(ref _slots[i]);
                if (cur == null) continue;

                // Try to steal the item atomically
                if (Interlocked.CompareExchange(ref _slots[i], null, cur) == cur)
                {
                    // place directly in new list (no contention since only one thread writes newList)
                    newList._slots[moved] = cur;
                    moved++;
                }
            }

            if (moved > 0)
            {
                Interlocked.Add(ref _count, -moved);
                Interlocked.Increment(ref _version);

                // finalize new list counts
                Volatile.Write(ref newList._count, moved);
                Interlocked.Increment(ref newList._version);
            }

            return newList;
        }
    }
}
