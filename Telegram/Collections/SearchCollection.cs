//
// Copyright Fela Ameghino 2015-2023
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
    public class SearchCollection<T, TSource> : DiffObservableCollection<T>, ISupportIncrementalLoading where TSource : IEnumerable<T>
    {
        private readonly Func<object, string, TSource> _factory;
        private readonly object _sender;

        private readonly DisposableMutex _mutex = new();
        private CancellationTokenSource _token;

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
            set => _query.Set(value);
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
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Query)));
        }

        public async void Update(TSource source)
        {
            _token?.Cancel();

            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                var token = new CancellationTokenSource();

                _token = token;

                _source = source;
                _incrementalSource = incremental;

                if (_initialized)
                {
                    using (await _mutex.WaitAsync())
                    {
                        await incremental.LoadMoreItemsAsync(0);

                        // 100% redundant
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, source, DefaultDiffHandler, DefaultOptions));
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
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                using (await _mutex.WaitAsync())
                {
                    _initialized = true;
                    _token?.Cancel();

                    var token = _token = new CancellationTokenSource();
                    var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                    // 100% redundant
                    if (token.IsCancellationRequested)
                    {
                        return result;
                    }

                    if (result.Count > 0)
                    {
                        var diff = await Task.Run(() => DiffUtil.CalculateDiff(this, _source, DefaultDiffHandler, DefaultOptions));
                        ReplaceDiff(diff);
                        UpdateEmpty();
                    }

                    return result;
                }
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
