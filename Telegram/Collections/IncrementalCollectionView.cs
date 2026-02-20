//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public interface IIncrementalCollection<T> : IList<T>, ISupportIncrementalLoading, INotifyCollectionChanged
    {

    }

    public interface IIncrementalCollection : IEnumerable, IList, ISupportIncrementalLoading, INotifyCollectionChanged
    {

    }

    public partial class IncrementalCollectionView : MvxObservableCollection<object>, IIncrementalCollection
    {
        private IIncrementalCollection _source;

        public IncrementalCollectionView(IIncrementalCollection source)
        {
            SetSource(source);
        }

        public IIncrementalCollection Source => _source;

        public void SetSource(IIncrementalCollection source)
        {
            if (_source == source)
            {
                return;
            }

            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = source;

            if (_source != null)
            {
                _source.CollectionChanged += OnCollectionChanged;
                ReplaceSource(_source);
            }
        }

        public async Task SetSourceAsync(IIncrementalCollection source)
        {
            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = source;

            if (_source != null)
            {
                if (_source.HasMoreItems)
                {
                    await _source.LoadMoreItemsAsync(0);
                }

                if (_source.Equals(source))
                {
                    _source.CollectionChanged += OnCollectionChanged;
                    ReplaceSource(_source);
                }
            }
        }

        private void ReplaceSource(IIncrementalCollection source)
        {
            var destination = this;
            if (destination.Empty())
            {
                destination.AddRangeT(source);
                return;
            }
            else if (source.EmptyT())
            {
                destination.ClearIfNotEmpty();
                return;
            }

            var recycledItems = Math.Min(destination.Count, source.Count);
            var changedItems = Math.Max(destination.Count, source.Count);

            if (destination.Count > source.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.RemoveAt(recycledItems);
                }
            }
            else if (source.Count > destination.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.Insert(i, source[i]);
                }
            }

            for (int i = 0; i < recycledItems; i++)
            {
                var oldItem = destination[i];
                var newItem = source[i];

                if (destination.DefaultDiffHandler == null || !destination.DefaultDiffHandler.CompareItems(oldItem, newItem))
                {
                    destination[i] = newItem;
                }
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertRange(e.NewStartingIndex, e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    this[e.OldStartingIndex] = e.NewItems[0];
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReplaceWithT(_source);
                    break;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return _source.LoadMoreItemsAsync(count);
        }

        public bool HasMoreItems => _source.HasMoreItems;
    }

    public partial class IncrementalCollectionView<T, TSource> : MvxObservableCollection<T>, IIncrementalCollection<T> where TSource : IIncrementalCollection<T>
    {
        private TSource _source;

        public IncrementalCollectionView(TSource source)
        {
            SetSource(source);
        }

        public TSource Source => _source;

        public void SetSource(TSource source)
        {
            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = source;

            if (_source != null)
            {
                _source.CollectionChanged += OnCollectionChanged;
                ReplaceSource(_source);
            }
        }

        public async Task SetSourceAsync(TSource source)
        {
            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            _source = source;

            if (_source != null)
            {
                if (_source.HasMoreItems)
                {
                    await _source.LoadMoreItemsAsync(0);
                }

                if (_source.Equals(source))
                {
                    _source.CollectionChanged += OnCollectionChanged;
                    ReplaceSource(_source);
                }
            }
        }

        private void ReplaceSource(TSource source)
        {
            var destination = this;
            if (destination.Empty())
            {
                destination.AddRange(source);
                return;
            }
            else if (source.Empty())
            {
                destination.ClearIfNotEmpty();
                return;
            }

            var recycledItems = Math.Min(destination.Count, source.Count);
            var changedItems = Math.Max(destination.Count, source.Count);

            if (destination.Count > source.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.RemoveAt(recycledItems);
                }
            }
            else if (source.Count > destination.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.Insert(i, source[i]);
                }
            }

            for (int i = 0; i < recycledItems; i++)
            {
                var oldItem = destination[i];
                var newItem = source[i];

                if (destination.DefaultDiffHandler == null || !destination.DefaultDiffHandler.CompareItems(oldItem, newItem))
                {
                    destination[i] = newItem;
                }
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertRange(e.NewStartingIndex, e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    this[e.OldStartingIndex] = (T)e.NewItems[0];
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReplaceWith(_source);
                    break;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return _source.LoadMoreItemsAsync(count);
        }

        public bool HasMoreItems => _source.HasMoreItems;
    }
}
