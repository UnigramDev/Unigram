using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Telegram.Api.Services.Cache
{
    public class Context<T> : ConcurrentDictionary<long, T>
    {
        public Context()
        {
            
        }

        public Context(IEnumerable<T> items, Func<T, long> keyFunc)
        {
            foreach (var item in items)
            {
                this[keyFunc(item)] = item;
            }
        }

        public virtual new T this[long index]
        {
            get
            {
                return TryGetValue(index, out T val) ? val : default(T);
            }

            set
            {
                base[index] = value;
            }
        }

        public virtual void Remove(long key)
        {
            TryRemove(key, out T value);
        }
    }
}