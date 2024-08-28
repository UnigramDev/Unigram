using System.Collections.Generic;

namespace Rg.DiffUtils
{
    public partial class DiffHandler<T> : IDiffHandler<T>
    {
        readonly IDiffEqualityComparer<T> _comparer;
        readonly UpdateItemDelegate _updateHandler;

        public delegate void UpdateItemDelegate(T oldItem, T newItem);

        public static DiffHandler<T> Default => new DiffHandler<T>(DiffEqualityComparer<T>.Default);

        public IDiffEqualityComparer<T> Comparer { get; }

        public DiffHandler(IDiffEqualityComparer<T> comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = comparer;
            _updateHandler = updateHandler;
        }

        public DiffHandler(IEqualityComparer<T> comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = new DiffEqualityComparer<T>(comparer);
            _updateHandler = updateHandler;
        }

        public DiffHandler(DiffEqualityComparer<T>.ComparerItemsDelegate comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = new DiffEqualityComparer<T>(comparer);
            _updateHandler = updateHandler;
        }

        public virtual bool CompareItems(T oldItem, T newItem)
        {
            return _comparer.CompareItems(oldItem, newItem);
        }

        public virtual void UpdateItem(T oldItem, T newItem)
        {
            if (_updateHandler != null)
                _updateHandler(oldItem, newItem);
        }
    }
}
