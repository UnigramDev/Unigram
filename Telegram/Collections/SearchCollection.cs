//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class SearchCollection<T, TSource> : DiffObservableCollection<T>, ISupportIncrementalLoading where TSource : IEnumerable<T>
    {
        private readonly Func<object, string, TSource> _factory;
        private readonly object _sender;

        private readonly DisposableMutex _mutex = new();
        private CancellationTokenSource _cancellation;

        private TSource _source;
        private ISupportIncrementalLoading _incrementalSource;

        private bool _initialized;

        public SearchCollection(Func<object, string, TSource> factory, IDiffHandler<T> handler)
            : this(factory, null, handler)
        {
        }

        public SearchCollection(Func<object, string, TSource> factory, object sender, IDiffHandler<T> handler)
            : base(handler, Constants.DiffOptions)
        {
            _factory = factory;
            _sender = sender;
            _query = new DebouncedProperty<string>(Constants.TypingTimeout, UpdateQuery);
        }

        private readonly DebouncedProperty<string> _query;
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
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Query)));
        }

        public void UpdateQuery(string value)
        {
            _query.Value = value;
            Update(_factory(_sender ?? this, value));
            //OnPropertyChanged(new PropertyChangedEventArgs(nameof(Query)));
        }

        public CancellationTokenSource Cancel()
        {
            _cancellation?.Cancel();
            _cancellation = new();
            return _cancellation;
        }

        public async void Update(TSource source)
        {
            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                _source = source;
                _incrementalSource = incremental;

                if (_initialized)
                {
                    var token = Cancel();

                    await incremental.LoadMoreItemsAsync(0);
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    ReplaceDiff(diff);
                    UpdateEmpty();

                    if (Count < 1 && incremental.HasMoreItems)
                    {
                        // This is 100% illegal and will cause a lot
                        // but really a lot of problems for sure.
                        Add(default);
                    }
                }
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                _initialized = true;

                var token = Cancel();
                var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                if (result.Count > 0 && !token.IsCancellationRequested)
                {
                    var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, _source, DefaultDiffHandler, DefaultOptions));

                    if (token.IsCancellationRequested)
                    {
                        return result;
                    }

                    ReplaceDiff(diff);
                    UpdateEmpty();
                }

                return result;
            });
        }

        public bool HasMoreItems
        {
            get
            {
                if (_incrementalSource != null)
                {
                    return _incrementalSource.HasMoreItems;
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
