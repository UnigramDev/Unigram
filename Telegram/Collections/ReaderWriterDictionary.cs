//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Telegram.Collections
{
    public class ReaderWriterDictionary<TKey, TValue> : IEnumerable<TValue>
    {
        protected readonly ReaderWriterLockSlim _lock = new();

        // Protected for the caches that keep a second index beside it and have to update both
        // under one lock. Touch it only with _lock held.
        protected readonly Dictionary<TKey, TValue> _dictionary;

        public ReaderWriterDictionary()
        {
            _dictionary = new();
        }

        public ReaderWriterDictionary(int capacity)
        {
            _dictionary = new(capacity);
        }

        public virtual TValue this[TKey key]
        {
            set
            {
                _lock.EnterWriteLock();
                try
                {
                    _dictionary[key] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public virtual void Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                _dictionary.Remove(key);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public virtual void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _dictionary.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dictionary.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public virtual bool TryGetValue(TKey key, out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                return _dictionary.TryGetValue(key, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public virtual bool TryRemove(TKey key, out TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_dictionary.TryGetValue(key, out value))
                {
                    _dictionary.Remove(key);
                    return true;
                }

                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsKey(TKey key)
        {
            _lock.EnterReadLock();
            try
            {
                return _dictionary.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public TValue Find(Predicate<TValue> predicate)
        {
            _lock.EnterReadLock();
            try
            {
                // Not FirstOrDefault: wrapping the predicate in a lambda allocated a closure
                // and an enumerator on every call, for what the struct enumerator does free.
                foreach (var value in _dictionary.Values)
                {
                    if (predicate(value))
                    {
                        return value;
                    }
                }

                return default;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void ForEach(Action<TValue> action)
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var value in _dictionary.Values)
                {
                    action(value);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IList<TValue> Values
        {
            get
            {
                IList<TValue> snapshot;

                _lock.EnterReadLock();
                try
                {
                    snapshot = _dictionary.Values.ToArray();
                }
                finally
                {
                    _lock.ExitReadLock();
                }

                return snapshot;
            }
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
