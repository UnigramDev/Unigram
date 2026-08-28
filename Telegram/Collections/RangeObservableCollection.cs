//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// The range and event-suppression half of what used to be MvxObservableCollection. Split out
// so that a collection which never diffs does not instantiate DiffUtil: under .NET Native every
// closed generic is code-generated, and most of these never call ReplaceDiff.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Telegram.Collections
{
    // Named for the diff collection when it was the only one; SelectedItemsBinder type-checks it,
    // so it belongs to whichever type actually carries ReplaceWithT.
    public interface IRangeObservableCollection : IList
    {
        void ReplaceWithT(IEnumerable collection);
    }

    public partial class RangeObservableCollection<T>
        : SuppressObservableCollection<T>
        , IRangeObservableCollection
        , IList<T>
    {
        public RangeObservableCollection()
        {
        }

        public RangeObservableCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public RangeObservableCollection(List<T> list)
            : base(list)
        {
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

        // Writes straight into Items rather than going through Insert: the base would raise
        // Count and Item[] for every single one, and suppression does not cover those.
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

        #region Mvx

        // Everything below moved over from MvxObservableCollection, which used to sit
        // between this class and its callers. Its AddRange and RemoveRange are gone rather
        // than moved: they hid the ones above, so which of the two ran depended on the
        // compile-time type of the reference.

        public void AddRangeT(IEnumerable items)
        {
            AddRange(items.Cast<T>());
        }

        // Suffixed rather than overloaded: a List<T> satisfies both IList and
        // IEnumerable<T>, so sharing the name would make every such call ambiguous.
        public void InsertRangeT(int startIndex, IList items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            InsertRange(startIndex, items.Cast<T>());
        }

        public void ReplaceWithT(IEnumerable items)
        {
            ReplaceWith(items.Cast<T>());
        }

        /// <summary>
        /// Replaces the current <see cref="DiffObservableCollection{T}"/> instance items with the ones specified in the items collection, raising a single <see cref="NotifyCollectionChangedAction.Reset"/> event.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void ReplaceWith(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            using (SuppressEvents())
            {
                Clear();
                AddRange(items);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Switches the current <see cref="DiffObservableCollection{T}"/> instance items with the ones specified in the items collection, raising the minimum required change events.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void SwitchTo(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var itemIndex = 0;
            var count = Count;

            foreach (var item in items)
            {
                if (itemIndex >= count)
                {
                    Add(item);
                }
                else if (!Equals(this[itemIndex], item))
                {
                    this[itemIndex] = item;
                }

                itemIndex++;
            }

            while (count > itemIndex)
            {
                RemoveAt(--count);
            }
        }

        public void Change(int index)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, this[index], index));
        }

        public void Clear(bool suppress)
        {
            using (SuppressEvents())
            {
                Clear();
            }
        }

        public void Reset()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public virtual bool Set<P>(ref P storage, P value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        public virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            try
            {
                OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }
            catch { }
        }

        #endregion
    }

    public class SuppressObservableCollection<T> : ObservableCollection<T>
    {
        public SuppressObservableCollection()
        {
        }

        public SuppressObservableCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public SuppressObservableCollection(List<T> list)
            : base(list)
        {
        }

        protected readonly struct SuppressEventsDisposable : IDisposable
        {
            private readonly SuppressObservableCollection<T> _collection;

            public SuppressEventsDisposable(SuppressObservableCollection<T> collection)
            {
                _collection = collection;
                ++collection._suppressEvents;
            }

            public void Dispose()
            {
                --_collection._suppressEvents;
            }
        }

        private int _suppressEvents;

        protected SuppressEventsDisposable SuppressEvents()
        {
            return new SuppressEventsDisposable(this);
        }

        public bool EventsAreSuppressed
        {
            get { return _suppressEvents > 0; }
        }

        public void Dispose()
        {
            _suppressEvents = int.MaxValue;
        }

        /// <summary>
        /// Raises the CollectionChanged event with the provided event data.
        /// </summary>
        /// <param name="e">The event data to report in the event.</param>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!EventsAreSuppressed)
            {
                base.OnCollectionChanged(e);
            }
        }

        public void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            OnCollectionChanged(args);
        }
    }
}
