using System.Collections.Generic;

namespace Rg.DiffUtils
{
    public partial class DiffEqualityComparer<T> : IDiffEqualityComparer<T>
    {
        readonly ComparerItemsDelegate _comparer;

        public delegate bool ComparerItemsDelegate(T x, T y);

        public static DiffEqualityComparer<T> Default => new DiffEqualityComparer<T>(EqualityComparer<T>.Default);

        public DiffEqualityComparer(ComparerItemsDelegate comparer)
        {
            _comparer = comparer;
        }

        public DiffEqualityComparer(IEqualityComparer<T> comparer)
        {
            _comparer = comparer.Equals;
        }

        public virtual bool CompareItems(T oldItem, T newItem)
        {
            return _comparer(oldItem, newItem); ;
        }
    }
}
