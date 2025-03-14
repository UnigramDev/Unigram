//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// MvxObservableCollection.cs
// https://github.com/MvvmCross/MvvmCross/blob/develop/MvvmCross/Core/Core/ViewModels/MvxObservableCollection.cs

// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
//
// Project Lead - Stuart Lodge, @slodge, me@slodge.com

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
    public interface IMvxObservableCollection : IList
    {
        void ReplaceWith(IEnumerable collection);
    }

    public partial class MvxObservableCollection<T>
        : ObservableCollection<T>
        , IMvxObservableCollection
        , IList<T>
    {
        protected readonly struct SuppressEventsDisposable : IDisposable
        {
            private readonly MvxObservableCollection<T> _collection;

            public SuppressEventsDisposable(MvxObservableCollection<T> collection)
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

        /// <summary>
        /// Initializes a new instance of the <see cref="MvxObservableCollection{T}"/> class.
        /// </summary>
        public MvxObservableCollection()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MvxObservableCollection{T}"/> class.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        public MvxObservableCollection(IEnumerable<T> items)
            : base(items)
        {
        }

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
        /// Raises the <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.CollectionChanged"/> event with the provided event data.
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

        /// <summary>
        /// Adds the specified items collection to the current <see cref="MvxObservableCollection{T}"/> instance.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            List<T> changedItems = null;
            int startingIndex = Count;

            using (SuppressEvents())
            {
                foreach (var item in items)
                {
                    Add(item);

                    changedItems ??= new();
                    changedItems.Add(item);
                }
            }

            if (changedItems != null)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, changedItems: changedItems, startingIndex: startingIndex));
            }
        }

        /// <summary>
        /// Adds the specified items collection to the current <see cref="MvxObservableCollection{T}"/> instance.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void InsertRange(int index, IList items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            using (SuppressEvents())
            {
                foreach (T item in items)
                {
                    Insert(index, item);
                }
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, changedItems: items, startingIndex: index));
        }

        /// <summary>
        /// Replaces the current <see cref="MvxObservableCollection{T}"/> instance items with the ones specified in the items collection, raising a single <see cref="NotifyCollectionChangedAction.Reset"/> event.
        /// </summary>
        /// <param name="items">The collection from which the items are copied.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void ReplaceWith(IEnumerable items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            using (SuppressEvents())
            {
                Clear();
                AddRange(items.Cast<T>());
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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

        public void ReplaceRange(IEnumerable<T> items, int firstIndex, int oldSize)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            using (SuppressEvents())
            {
                var lastIndex = firstIndex + oldSize - 1;

                // If there are more items in the previous list, remove them.
                while (firstIndex + items.Count() <= lastIndex)
                {
                    RemoveAt(lastIndex--);
                }

                foreach (var item in items)
                {
                    if (firstIndex <= lastIndex)
                    {
                        SetItem(firstIndex++, item);
                    }
                    else
                    {
                        Insert(firstIndex++, item);
                    }
                }
            }

            // TODO: Emit up to two OnCollectionChangedEvents:
            //   1. Replace for those items replaced.
            //   2. Add for items added beyond the original size.
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Switches the current <see cref="MvxObservableCollection{T}"/> instance items with the ones specified in the items collection, raising the minimum required change events.
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

        /// <summary>
        /// Removes the current <see cref="MvxObservableCollection{T}"/> instance items of the ones specified in the items collection, raising the minimum required change events.
        /// </summary>
        /// <param name="items">The collection which items will be removed.</param>
        /// <exception cref="ArgumentNullException">The items list is null.</exception>
        public void RemoveItems(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            using (SuppressEvents())
            {
                foreach (var item in items)
                {
                    Remove(item);
                }
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Removes the current <see cref="MvxObservableCollection{T}"/> instance items of the ones specified in the range, raising the minimum required change events.
        /// </summary>
        /// <param name="start">The start index.</param>
        /// <param name="count">The count of items to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Start index or count incorrect</exception>
        public void RemoveRange(int start, int count)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            var end = start + count - 1;

            if (end > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var removedItems = new List<T>(count);
            for (int i = start; i <= end; i++)
            {
                removedItems.Add(this[i]);
            }

            using (SuppressEvents())
            {
                for (var i = end; i >= start; i--)
                {
                    RemoveAt(i);
                }
            }

            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, start));
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
    }
}