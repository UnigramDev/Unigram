using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Common;

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
