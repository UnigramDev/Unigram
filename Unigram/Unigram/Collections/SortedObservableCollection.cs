using System;
using System.Collections.Generic;
using System.Linq;

namespace Unigram.Collections
{
    public class SortedObservableCollection<T> : MvxObservableCollection<T>
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
            index = Array.BinarySearch(Items.ToArray(), item, _comparer);
            if (index >= 0) ; /*throw new ArgumentException("Cannot insert duplicated items");*/
            else base.InsertItem(~index, item);
        }
    }
}
