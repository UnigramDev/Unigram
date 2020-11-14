using System;
using System.Collections;
using System.Collections.Generic;

namespace Unigram.Common
{
    public class UniqueList<TKey, TValue> : IList<TValue>
    {
        private readonly Func<TValue, TKey> _selector;
        private readonly SortedList<TKey, TValue> _inner;

        public UniqueList(Func<TValue, TKey> selector)
        {
            _selector = selector;
            _inner = new SortedList<TKey, TValue>();
        }

        public IList<TKey> Keys => _inner.Keys;

        public TValue this[int index]
        {
            get
            {
                return _inner.Values[index];
            }
            set { }
        }

        public int Count
        {
            get
            {
                return _inner.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(TValue item)
        {
            var key = _selector(item);
            if (_inner.ContainsKey(key))
            {
                return;
            }

            _inner.Add(key, item);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(TValue item)
        {
            return _inner.ContainsKey(_selector(item));
        }

        public bool ContainsKey(TKey key)
        {
            return _inner.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _inner.TryGetValue(key, out value);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            _inner.Values.CopyTo(array, arrayIndex);
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return _inner.Values.GetEnumerator();
        }

        public int IndexOf(TValue item)
        {
            return _inner.Values.IndexOf(item);
        }

        public void Insert(int index, TValue item)
        {
            var key = _selector(item);
            if (_inner.ContainsKey(key))
            {
                return;
            }

            _inner.Add(key, item);
        }

        public bool Remove(TValue item)
        {
            return _inner.Remove(_selector(item));
        }

        public bool Remove(TKey key)
        {
            return _inner.Remove(key);
        }

        public void RemoveAt(int index)
        {
            _inner.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _inner.Values.GetEnumerator();
        }
    }
}
