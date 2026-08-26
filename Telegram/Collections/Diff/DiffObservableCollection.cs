//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// Based on Rg.DiffUtils, Copyright (c) 2021 Kirill Lyubimov. See DiffUtil.cs for the
// MIT notice covering this folder.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Telegram.Collections
{
    public partial class DiffObservableCollection<T> : ObservableCollection<T>
    {
        public IDiffHandler<T> DefaultDiffHandler { get; }

        public bool DetectMoves { get; }

        public DiffObservableCollection()
            : this(DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(IDiffHandler<T> diffHandler)
            : this(diffHandler, true)
        {
        }

        public DiffObservableCollection(IDiffHandler<T> diffHandler, bool detectMoves)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public DiffObservableCollection(IEnumerable<T> collection)
            : this(collection, DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(IEnumerable<T> collection, IDiffHandler<T> diffHandler)
            : this(collection, diffHandler, true)
        {
        }

        public DiffObservableCollection(IEnumerable<T> collection, IDiffHandler<T> diffHandler, bool detectMoves)
            : base(collection)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public DiffObservableCollection(List<T> list)
            : this(list, DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(List<T> list, IDiffHandler<T> diffHandler)
            : this(list, diffHandler, true)
        {
        }

        public DiffObservableCollection(List<T> list, IDiffHandler<T> diffHandler, bool detectMoves)
            : base(list)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public void ReplaceDiff(IEnumerable<T> seq)
        {
            ReplaceDiff(seq, DefaultDiffHandler, DetectMoves);
        }

        public void ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler)
        {
            ReplaceDiff(seq, diffHandler, DetectMoves);
        }

        public void ReplaceDiff(IEnumerable<T> seq, bool detectMoves)
        {
            ReplaceDiff(seq, DefaultDiffHandler, detectMoves);
        }

        public virtual void ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler, bool detectMoves)
        {
            // Computed and applied in one pass, so there is no window in which the indices
            // the walk reports can go stale against this collection.
            var diff = new DiffCalculator<T>(this, seq, diffHandler, detectMoves);

            while (diff.Next())
            {
                switch (diff.State)
                {
                    case DiffState.Add:
                        Insert(diff.NewIndex, diff.NewValue);
                        break;
                    case DiffState.Remove:
                        RemoveAt(diff.OldIndex);
                        break;
                    case DiffState.Move:
                        UpdateItem(diff.OldValue, diff.NewValue, diff.NewSeqIndex, diffHandler);
                        Move(diff.OldIndex, diff.NewIndex);
                        break;
                    case DiffState.Unchanged:
                        UpdateItem(diff.OldValue, diff.NewValue, diff.NewSeqIndex, diffHandler);
                        break;
                }
            }
        }

        // Overridden where an item that survived the diff has to be reconciled with the one
        // that replaced it.
        protected virtual void UpdateItem(T oldValue, T newValue, int newSeqIndex, IDiffHandler<T> diffHandler)
        {
            diffHandler.UpdateItem(oldValue, newValue);
        }

        public virtual void AddRange(IEnumerable<T> collection)
        {
            AddRangeInternal(collection);
        }

        public virtual void InsertRange(int startIndex, IEnumerable<T> collection)
        {
            if (startIndex < 0 || startIndex > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            AddRangeInternal(collection, startIndex);
        }

        public virtual void RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            CheckReentrancy();

            var changedItems = new List<T>(collection);

            for (var i = 0; i < changedItems.Count; i++)
            {
                if (!Items.Remove(changedItems[i]))
                {
                    changedItems.RemoveAt(i);
                    i--;
                }
            }

            if (changedItems.Count == 0)
            {
                return;
            }

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Remove, changedItems);
        }

        public virtual void RemoveRange(int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (startIndex + count > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            CheckReentrancy();

            var changedItems = new List<T>(count);

            for (int i = startIndex; i < startIndex + count; i++)
            {
                changedItems.Add(Items[i]);
            }

            RemoveRangeInternal(startIndex, count, changedItems);
        }

        public virtual void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            CheckReentrancy();

            var previouslyEmpty = Items.Count == 0;

            Items.Clear();

            AddArrangeCore(collection as List<T> ?? new List<T>(collection));

            var currentlyEmpty = Items.Count == 0;

            if (previouslyEmpty && currentlyEmpty)
            {
                return;
            }

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
        }

        protected virtual void AddRangeInternal(IEnumerable<T> collection, int startIndex = -1)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            CheckReentrancy();

            var eventStartIndex = startIndex < 0 ? Items.Count : startIndex;

            var changedItems = collection as List<T> ?? new List<T>(collection);

            if (!AddArrangeCore(changedItems, startIndex))
            {
                return;
            }

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Add, changedItems, eventStartIndex);
        }

        protected virtual void RemoveRangeInternal(int startIndex, int count, List<T> changedItems)
        {
            if (startIndex < 0 || startIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (startIndex + count > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            CheckReentrancy();

            for (int i = startIndex + count - 1; i >= startIndex; i--)
            {
                Items.RemoveAt(i);
            }

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Remove, changedItems, startIndex);
        }

        private bool AddArrangeCore(List<T> collection, int startIndex = -1)
        {
            var itemsAdded = false;
            var index = startIndex;

            foreach (var item in collection)
            {
                if (startIndex >= 0)
                {
                    Items.Insert(index, item);
                }
                else
                {
                    Items.Add(item);
                }

                itemsAdded = true;
                index++;
            }

            return itemsAdded;
        }

        protected virtual void RaiseChangeNotificationEvents(NotifyCollectionChangedAction action, List<T> changedItems = null, int startingIndex = -1)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

            if (changedItems == null)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(action));
            }
            else
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, changedItems: changedItems, startingIndex: startingIndex));
            }
        }
    }
}
