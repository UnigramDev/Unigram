using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Common;

namespace Unigram.Common
{
    public class KeyedList<TKey, T> : MvxObservableCollection<T>
    {
        private TKey _key;
        public TKey Key
        {
            get
            {
                return _key;
            }
            set
            {
                _key = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Key"));
            }
        }

        public KeyedList(TKey key)
        {
            _key = key;
        }

        public KeyedList(TKey key, IEnumerable<T> source)
            : base(source)
        {
            _key = key;
        }

        public KeyedList(IGrouping<TKey, T> source)
            : base(source)
        {
            _key = source.Key;
        }

        public void Update()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
        }

        protected override event PropertyChangedEventHandler PropertyChanged;
    }
}
