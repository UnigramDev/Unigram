//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;

namespace Telegram.Collections
{
    public partial class SortedObservableCollection<T> : MvxObservableCollection<T>
    {
        private readonly IComparer<T> _comparer;

        public SortedObservableCollection(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public SortedObservableCollection(IComparer<T> comparer, IEnumerable<T> source)
            : base(source)
        {
            _comparer = comparer;
        }

        protected override void InsertItem(int index, T item)
        {
            index = Items.BinarySearch(item, _comparer);
            if (index >= 0)
            {
                ; /*throw new ArgumentException("Cannot insert duplicated items");*/
            }
            else
            {
                base.InsertItem(~index, item);
            }
        }
    }
}
