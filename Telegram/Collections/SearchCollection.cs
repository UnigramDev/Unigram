//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
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
    public partial class SearchCollection<T, TSource> : MvxObservableCollection<T>, ISupportIncrementalLoading where TSource : IList<T>, ISupportIncrementalLoading, INotifyCollectionChanged
    {
        private readonly Func<object, string, TSource> _factory;
        private object _sender;

        private CancellationTokenSource _cancellation;

        private TSource _source;

        private bool _initialized;
        private bool _loading;
        private bool _replacing;

        public SearchCollection(Func<object, string, TSource> factory, IDiffHandler<T> handler)
            : this(factory, null, handler)
        {
        }

        public SearchCollection(Func<object, string, TSource> factory, object sender, IDiffHandler<T> handler)
            : base(handler, Constants.DiffOptions)
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

        public CancellationTokenSource Cancel()
        {
            _cancellation?.Cancel();
            _cancellation = new();
            return _cancellation;
        }

        public void Update(TSource source)
        {
            UpdateImpl(source, false);
        }

        private async void UpdateImpl(TSource source, bool reentrancy)
        {
            if (_source != null)
            {
                _source.CollectionChanged -= OnCollectionChanged;
            }

            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                _source = source;

                if (_initialized)
                {
                    _loading = true;
                    _replacing = true;

                    var token = Cancel();

                    await incremental.LoadMoreItemsAsync(0);
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        _loading = false;
                        _replacing = false;
                        return;
                    }

                    ReplaceDiff(diff);
                    UpdateEmpty();

                    _loading = false;
                    _replacing = false;

                    _source.CollectionChanged += OnCollectionChanged;

                    // I'm not sure in what conditions this can happen, but it happens
                    if (Count < 1 && incremental.HasMoreItems && !reentrancy)
                    {
                        UpdateImpl(source, true);
                    }
                }
                else
                {
                    _source.CollectionChanged += OnCollectionChanged;
                }
            }
            else
            {
                _source = default;

                Cancel();

                Clear();
                UpdateEmpty();
            }
        }

        protected override void UpdateItems(IReadOnlyList<DiffItem<T>> items, IDiffHandler<T> diffHandler)
        {
            foreach (DiffItem<T> item in items)
            {
                // Swap new item with old one to have the same reference in both lists
                _source[item.NewSeqIndex] = item.OldValue;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                if (_loading || _source == null)
                {
                    return new LoadMoreItemsResult
                    {
                        Count = 0
                    };
                }

                _loading = true;

                var token = Cancel();
                var result = await _source?.LoadMoreItemsAsync(count);

                if (result.Count > 0 && !token.IsCancellationRequested)
                {
                    //var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, _source, DefaultDiffHandler, DefaultOptions));

                    //if (token.IsCancellationRequested)
                    //{
                    //    _loading = false;
                    //    return result;
                    //}

                    //_replacingDiff = true;
                    //ReplaceDiff(diff);

                    //_replacingDiff = false;
                    UpdateEmpty();
                }

                _initialized = true;
                _loading = false;

                return result;
            });
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_replacing)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertRange(e.NewStartingIndex, e.NewItems);
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
