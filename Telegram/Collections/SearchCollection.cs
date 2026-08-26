//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class SearchCollection<T, TSource> : DiffObservableCollection<T>, ISupportIncrementalLoading where TSource : IList<T>, ISupportIncrementalLoading, INotifyCollectionChanged
    {
        private readonly Func<object, string, TSource> _factory;
        private object _sender;

        // Guards the query pipeline alone: a value waiting out the debounce, and the
        // UpdateQuery it eventually fires. Loading must not touch it, or scrolling the
        // list would drop a search the user has already typed.
        private CancellationTokenSource _cancellation;

        private TSource _source;

        // Bumped whenever the source is replaced or the collection is cancelled. Work in
        // flight compares it after every await: a call that lost the race must not touch
        // the collection, nor clear state the newer call now owns.
        private int _version;

        // The load in flight, if any. A second caller gets this task, never an empty
        // result: Count = 0 while HasMoreItems is true makes the list ask again at once,
        // and that loop never yields the thread these awaits resume on.
        private Task<LoadMoreItemsResult> _loading;

        // The source swap in flight, if any. Loads queue behind it, so nothing mutates
        // the source while DiffUtil reads it off-thread.
        private Task _replace;

        private bool _initialized;

        public SearchCollection(Func<object, string, TSource> factory, IDiffHandler<T> handler)
            : this(factory, null, handler)
        {
        }

        public SearchCollection(Func<object, string, TSource> factory, object sender, IDiffHandler<T> handler)
            : base(handler)
        {
            _factory = factory;
            _sender = sender;
            _query = new DebouncedPropertyWithToken<string>(Constants.TypingTimeout, UpdateQuery);
        }

        private readonly DebouncedPropertyWithToken<string> _query;
        public string Query
        {
            get => _query;
            set
            {
                _cancellation?.Cancel();
                _cancellation = new();

                _query.Set(value, _cancellation.Token);
            }
        }

        public TSource Source => _source;

        public void Reload()
        {
            Update(_factory(_sender ?? this, _query.Value));
        }

        public void UpdateSender(object sender)
        {
            Update(_factory((_sender = sender) ?? this, _query.Value));
        }

        public void UpdateQuery(string value, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            Update(_factory(_sender ?? this, _query.Value = value));
        }

        public void Cancel()
        {
            _cancellation?.Cancel();
            _cancellation = null;

            _query.Cancel();

            // Abandons the load and the source swap in flight, if any.
            _version++;
        }

        public void Update(TSource source)
        {
            var replace = UpdateImpl(source, false);

            // A swap that ran to completion synchronously has already cleared the field,
            // so storing it now would keep every later load waiting on a finished task.
            _replace = replace.IsCompleted ? null : replace;
        }

        private async Task UpdateImpl(TSource source, bool reentrancy)
        {
            var version = reentrancy ? _version : ++_version;

            try
            {
                if (_source != null)
                {
                    _source.CollectionChanged -= OnCollectionChanged;
                }

                if (source == null || !source.HasMoreItems)
                {
                    _source = default;

                    Clear();
                    UpdateEmpty();
                    return;
                }

                _source = source;

                if (!_initialized)
                {
                    source.CollectionChanged += OnCollectionChanged;
                    return;
                }

                await source.LoadMoreItemsAsync(0);

                if (version != _version)
                {
                    return;
                }

                // On the UI thread on purpose. The walk snapshots both sides and then
                // works on the copies, but the indices it reports are positions in this
                // collection, so nothing else may touch it while they are applied.
                // Measured at well under a millisecond up to a thousand items, and a list
                // long enough to cost more than that costs far more than that applying it.
                ReplaceDiff(source);
                UpdateEmpty();

                // Subscribed last on purpose: UpdateItem writes the old items back into
                // the source while the diff is applied, and the handler would echo that
                // straight back into this collection.
                source.CollectionChanged += OnCollectionChanged;

                // I'm not sure in what conditions this can happen, but it happens
                if (Count < 1 && source.HasMoreItems && !reentrancy)
                {
                    await UpdateImpl(source, true);
                }
            }
            catch (Exception ex)
            {
                // A swap that throws must not leave loading wedged: the list would then
                // ask for more items forever and never get any.
                Logger.Error(ex);
            }
            finally
            {
                if (version == _version)
                {
                    _replace = null;
                }
            }
        }

        protected override void UpdateItem(T oldValue, T newValue, int newSeqIndex, IDiffHandler<T> diffHandler)
        {
            // Swap new item with old one to have the same reference in both lists
            _source[newSeqIndex] = oldValue;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return IncrementalLoading.Run(_ => LoadMoreItems(count));
        }

        private Task<LoadMoreItemsResult> LoadMoreItems(uint count)
        {
            if (_loading != null)
            {
                return _loading;
            }

            var loading = LoadMoreItemsImpl(count);

            // As above: a task that completed synchronously has already run its finally.
            if (!loading.IsCompleted)
            {
                _loading = loading;
            }

            return loading;
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsImpl(uint count)
        {
            try
            {
                var replace = _replace;
                if (replace != null)
                {
                    await replace;
                }

                var source = _source;
                if (source == null)
                {
                    return default;
                }

                var version = _version;
                var result = await source.LoadMoreItemsAsync(count);

                if (result.Count > 0 && version == _version)
                {
                    UpdateEmpty();
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return default;
            }
            finally
            {
                _initialized = true;
                _loading = null;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertRangeT(e.NewStartingIndex, e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Move:
                    Move(e.OldStartingIndex, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReplaceWith(_source);
                    break;
            }
        }

        public bool HasMoreItems
        {
            get
            {
                if (_source != null)
                {
                    return _source.HasMoreItems;
                }

                // The list has asked for items, so whatever arrives next replaces
                // something already on screen rather than being the initial fill.
                _initialized = true;
                return false;
            }
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            private set
            {
                if (_isEmpty != value)
                {
                    _isEmpty = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsEmpty)));
                }
            }
        }

        private void UpdateEmpty()
        {
            IsEmpty = Count == 0;
        }
    }
}
