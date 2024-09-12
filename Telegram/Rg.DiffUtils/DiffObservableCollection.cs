using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Telegram;
using WinRT;

namespace Rg.DiffUtils
{
    public partial class DiffObservableCollection<T> : ObservableCollection<T>
    {
        public IDiffHandler<T> DefaultDiffHandler { get; }

        public DiffOptions DefaultOptions { get; }

        public DiffObservableCollection(IEnumerable<T> collection) : this(collection, DiffHandler<T>.Default, new DiffOptions()) { }

        public DiffObservableCollection(
            IEnumerable<T> collection,
            IDiffHandler<T> diffHandler) : this(collection, diffHandler, new DiffOptions()) { }

        public DiffObservableCollection(
            IEnumerable<T> collection,
            IDiffHandler<T> diffHandler,
            DiffOptions options) : base(collection)
        {
            DefaultDiffHandler = diffHandler;
            DefaultOptions = options;
        }

        public DiffObservableCollection(List<T> list) : this(list, DiffHandler<T>.Default, new DiffOptions()) { }

        public DiffObservableCollection(
            List<T> list,
            IDiffHandler<T> diffHandler) : this(list, diffHandler, new DiffOptions()) { }

        public DiffObservableCollection(
            List<T> list,
            IDiffHandler<T> diffHandler,
            DiffOptions options) : base(list)
        {
            DefaultDiffHandler = diffHandler;
            DefaultOptions = options;
        }

        public DiffObservableCollection(IDiffHandler<T> diffHandler) : this(diffHandler, new DiffOptions()) { }

        public DiffObservableCollection(DiffOptions options) : this(DiffHandler<T>.Default, options) { }

        public DiffObservableCollection(
            IDiffHandler<T> diffHandler,
            DiffOptions options)
        {
            DefaultDiffHandler = diffHandler;
            DefaultOptions = options;
        }

        public DiffObservableCollection()
        {
            DefaultDiffHandler = DiffHandler<T>.Default;
            DefaultOptions = new DiffOptions();
        }

        public virtual DiffResult<T> ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler)
        {
            return ReplaceDiff(seq, diffHandler, DefaultOptions);
        }

        public virtual DiffResult<T> ReplaceDiff(IEnumerable<T> seq, DiffOptions options)
        {
            return ReplaceDiff(seq, DefaultDiffHandler, options);
        }

        public virtual DiffResult<T> ReplaceDiff(IEnumerable<T> seq)
        {
            return ReplaceDiff(seq, DefaultDiffHandler, DefaultOptions);
        }

        public virtual DiffResult<T> ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler, DiffOptions options)
        {
            var diffResult = DiffUtil.CalculateDiff(this, seq, diffHandler, options);

            ReplaceDiff(diffResult, diffHandler);

            return diffResult;
        }

        public virtual void ReplaceDiff(DiffResult<T> diffResult)
        {
            ReplaceDiff(diffResult, DiffHandler<T>.Default);
        }

        public virtual void ReplaceDiff(DiffResult<T> diffResult, IDiffHandler<T> diffHandler)
        {
            foreach (var step in diffResult.Steps)
            {
                switch (step.Status)
                {
                    case DiffStatus.Add:
                        InsertRange(step.NewStartIndex, step.Items.Select(i => i.NewValue));
                        break;
                    case DiffStatus.Remove:
                        RemoveRangeInternal(step.OldStartIndex, step.Items.Count, step.Items.Select(i => i.OldValue));
                        break; ;
                    case DiffStatus.Move:
                        UpdateItems(step.Items, diffHandler);
                        Move(step.OldStartIndex, step.NewStartIndex);
                        break;

                    default:
                        throw new InvalidOperationException($"{step.Status} diff status is not supported");
                }
            }

            UpdateItems(diffResult.NotMovedItems, diffHandler);
        }

        public virtual void AddRange(IEnumerable<T> collection)
        {
            AddRangeInternal(collection);
        }

        public virtual void InsertRange(int startIndex, IEnumerable<T> collection)
        {
            if (startIndex < 0 || startIndex > Count)
                new ArgumentOutOfRangeException(nameof(startIndex));

            AddRangeInternal(collection, startIndex);
        }

        public virtual void RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

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
                return;

            RaiseChangeNotificationEvents(
                action: NotifyCollectionChangedAction.Remove,
                changedItems: changedItems);
        }

        public virtual void RemoveRange(int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= Count)
                new ArgumentOutOfRangeException(nameof(startIndex));

            if (startIndex + count >= Count)
                new ArgumentOutOfRangeException(nameof(count));

            CheckReentrancy();

            var changedItems = Items
                .Skip(startIndex)
                .Take(count)
                .ToList();

            RemoveRangeInternal(startIndex, count, changedItems);
        }

        public virtual void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            var previouslyEmpty = Items.Count == 0;

            Items.Clear();

            AddArrangeCore(collection);

            var currentlyEmpty = Items.Count == 0;

            if (previouslyEmpty && currentlyEmpty)
                return;

            RaiseChangeNotificationEvents(NotifyCollectionChangedAction.Reset);
        }

        protected virtual void AddRangeInternal(IEnumerable<T> collection, int startIndex = -1)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            CheckReentrancy();

            var eventStartIndex = startIndex < 0 ? Items.Count : startIndex;

            var itemsAdded = AddArrangeCore(collection, startIndex);

            if (!itemsAdded)
                return;

            var changedItems = collection is List<T>
                ? (List<T>)collection
                : new List<T>(collection);

            RaiseChangeNotificationEvents(
                NotifyCollectionChangedAction.Add,
                changedItems,
                eventStartIndex);
        }

        protected virtual void RemoveRangeInternal(int startIndex, int count, IEnumerable<T> changedItems)
        {
            if (startIndex < 0 || startIndex >= Count)
                new ArgumentOutOfRangeException(nameof(startIndex));

            if (startIndex + count >= Count)
                new ArgumentOutOfRangeException(nameof(count));

            CheckReentrancy();

            for (int i = startIndex + count - 1; i >= startIndex; i--)
                Items.RemoveAt(i);

            RaiseChangeNotificationEvents(
                NotifyCollectionChangedAction.Remove,
                changedItems.ToList(),
                startIndex);
        }

        protected virtual void UpdateItems(IReadOnlyList<DiffItem<T>> items, IDiffHandler<T> diffHandler)
        {
            foreach (var item in items)
                diffHandler.UpdateItem(item.OldValue, item.NewValue);
        }

        bool AddArrangeCore(IEnumerable<T> collection, int startIndex = -1)
        {
            var itemsAdded = false;
            var index = startIndex;

            foreach (var item in collection)
            {
                if (startIndex >= 0)
                    Items.Insert(index, item);
                else
                    Items.Add(item);

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
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(action));
            else
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, changedItems: changedItems, startingIndex: startingIndex));
        }
    }
}
