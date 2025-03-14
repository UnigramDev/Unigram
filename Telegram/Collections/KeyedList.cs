//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using WinRT;

namespace Telegram.Collections
{
    [GeneratedBindableCustomProperty]
    public partial class KeyedList<TKey, T> : MvxObservableCollection<T>
    {
        private TKey _key;
        public TKey Key
        {
            get => _key;
            set => _key = value;//PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Key"));
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
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
        }

        public override string ToString()
        {
            return Key?.ToString() ?? string.Empty;
        }

        //protected override event PropertyChangedEventHandler PropertyChanged;
    }
}
