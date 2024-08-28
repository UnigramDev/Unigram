using System.Collections.Generic;

namespace Rg.DiffUtils
{
    public partial class DiffStep<T>
    {
        readonly List<DiffItem<T>> _items;

        public DiffStatus Status { get; internal set; }

        public int OldStartIndex { get; internal set; } = -1;

        public int NewStartIndex { get; internal set; } = -1;

        public IReadOnlyList<DiffItem<T>> Items => _items;

        public DiffStep(DiffItem<T> item)
        {
            _items = new List<DiffItem<T>>();

            _items.Add(item);
        }

        internal void InsertItem(DiffItem<T> item)
        {
            _items.Insert(0, item);
        }
    }
}
