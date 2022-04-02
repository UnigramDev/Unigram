using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class SearchCollection<T, TSource> : DiffObservableCollection<T>, ISupportIncrementalLoading where TSource : IEnumerable<T>
    {
        private readonly Func<object, string, TSource> _factory;
        private readonly object _sender;

        private readonly DisposableMutex _mutex = new();
        private CancellationTokenSource _token;

        private TSource _source;
        private ISupportIncrementalLoading _incrementalSource;

        public SearchCollection(Func<object, string, TSource> factory, IDiffHandler<T> handler)
            : this(factory, null, handler)
        {
        }

        public SearchCollection(Func<object, string, TSource> factory, object sender, IDiffHandler<T> handler)
            : base(handler, new DiffOptions { AllowBatching = false, DetectMoves = true })
        {
            _factory = factory;
            _sender = sender;
            _query = new PropertyDebouncer<string>(Constants.TypingTimeout, SetQuery);
        }

        private readonly PropertyDebouncer<string> _query;
        public string Query
        {
            get => _query;
            set => _query.Set(value);
        }

        public TSource Source => _source;

        public void SetQuery(string value)
        {
            Update(_factory(_sender ?? this, value));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Query)));
        }

        public async void Update(TSource source)
        {
            if (_token != null)
            {
                _token.Cancel();
            }

            if (source is ISupportIncrementalLoading incremental && incremental.HasMoreItems)
            {
                var token = new CancellationTokenSource();

                _token = token;

                _source = source;
                _incrementalSource = incremental;

                using (await _mutex.WaitAsync())
                {
                    await incremental.LoadMoreItemsAsync(0);
                }

                // 100% redundant
                if (token.IsCancellationRequested)
                {
                    return;
                }

                ReplaceDiff(source);

                if (Count < 1 && incremental.HasMoreItems)
                {
                    // This is 100% illegal and will cause a lot
                    // but really a lot of problems for sure.
                    Add(default);
                }
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async _ =>
            {
                using (await _mutex.WaitAsync())
                {
                    if (_token != null)
                    {
                        _token.Cancel();
                    }

                    var token = _token = new CancellationTokenSource();
                    var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                    // 100% redundant
                    if (token.IsCancellationRequested)
                    {
                        return result;
                    }

                    if (result.Count > 0)
                    {
                        ReplaceDiff(_source);
                    }

                    return result;
                }
            });
        }

        public bool HasMoreItems => _incrementalSource?.HasMoreItems ?? false;
    }
}
