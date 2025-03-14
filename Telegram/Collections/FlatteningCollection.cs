//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Telegram.Collections
{
    public interface IKeyedCollection : ICollection, IList, INotifyCollectionChanged
    {
        int Index { get; set; }

        int TotalIndex { get; }

        int TotalCount { get; }

        string Key { get; }
    }

    public partial class FlatteningCollection : IncrementalCollection<object>
    {
        private readonly List<IKeyedCollection> _groups = new();

        public FlatteningCollection(IIncrementalCollectionOwner owner, params IKeyedCollection[] groups)
            : base(owner)
        {
            foreach (var group in groups)
            {
                AddInternal(group);
            }
        }

        public void AddInternal(IKeyedCollection collection)
        {
            _groups.Add(collection);
            collection.CollectionChanged += OnCollectionChanged;
            collection.Index = Count;

            if (collection.Count > 0)
            {
                Add(collection);
            }

            foreach (var item in collection)
            {
                Add(item);
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not IKeyedCollection collection)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Insert(collection, e.NewStartingIndex, e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Remove(collection, e.OldStartingIndex, e.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Reset(collection);
                    break;
            }

            UpdateIndexes(collection);
        }

        private void Insert(IKeyedCollection collection, int newStartingIndex, IList newItems)
        {
            if (newStartingIndex == 0 && newItems.Count == collection.Count && collection.Count > 0 && collection.Key != null)
            {
                Insert(collection.Index, collection);
            }

            foreach (var item in newItems)
            {
                Insert(collection.TotalIndex + newStartingIndex, item);
                newStartingIndex++;
            }
        }

        private void Remove(IKeyedCollection collection, int oldStartingIndex, int oldItemsCount)
        {
            for (int i = oldStartingIndex; i < oldStartingIndex + oldItemsCount; i++)
            {
                RemoveAt(collection.TotalIndex + i);
            }

            if (collection.Count == 0 && oldItemsCount > 0 && collection.Key != null)
            {
                RemoveAt(collection.Index);
            }
        }

        private void Reset(IKeyedCollection collection)
        {
            var index = _groups.IndexOf(collection);
            var count = index < _groups.Count - 1
                ? _groups[index + 1].Index - collection.Index
                : Count - collection.Index;

            for (int i = collection.Index; i < collection.Index + count; i++)
            {
                RemoveAt(collection.Index);
            }

            Insert(collection, 0, collection);
        }

        private void UpdateIndexes(IKeyedCollection from)
        {
            var index = _groups.IndexOf(from);
            if (index >= 0)
            {
                var displacement = from.Index + from.TotalCount;

                for (int i = index + 1; i < _groups.Count; i++)
                {
                    _groups[i].Index = displacement;
                    displacement += _groups[i].TotalCount;
                }
            }
        }
    }
}
