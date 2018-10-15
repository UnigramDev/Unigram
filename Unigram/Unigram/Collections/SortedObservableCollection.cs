using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Collections
{
    public class SortedObservableCollection<T> : MvxObservableCollection<T>
    {
        private readonly IComparer<T> _comparer;
        private readonly bool _ignore;

        public SortedObservableCollection(IComparer<T> comparer, bool ignore = false)
        {
            _comparer = comparer;
            _ignore = ignore;
        }

        public SortedObservableCollection(IComparer<T> comparer, IEnumerable<T> source)
            : base(source)
        {
            _comparer = comparer;
        }

        protected override void InsertItem(int index, T item)
        {
            if (_ignore)
            {
                base.InsertItem(index, item);
                return;
            }

            index = Array.BinarySearch(Items.ToArray(), item, _comparer);
            if (index >= 0) ; /*throw new ArgumentException("Cannot insert duplicated items");*/
            else base.InsertItem(~index, item);
        }

        public int NextIndexOf(T item)
        {
            var index = Array.BinarySearch(Items.Except(new[] { item }).ToArray(), item, _comparer);
            if (index >= 0)
            {
                return -1;
            }

            return ~index;
        }
    }
}
