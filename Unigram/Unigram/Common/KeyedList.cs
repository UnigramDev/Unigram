using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public class KeyedList<TKey, T> : ObservableCollection<T>
    {
        public TKey Key { get; private set; }

        public KeyedList(TKey key)
        {
            Key = key;
        }

        public KeyedList(TKey key, IEnumerable<T> source)
            : base(source)
        {
            Key = key;
        }

        public KeyedList(IGrouping<TKey, T> source)
            : base(source)
        {
            Key = source.Key;
        }
    }
}
