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
    public class SearchCollection<T> : DiffObservableCollection<T>, ISupportIncrementalLoading
    {
        private readonly Func<string, IEnumerable<T>> _factory;

        private CancellationTokenSource _token;

        private IEnumerable<T> _source;
        private ISupportIncrementalLoading _incrementalSource;

        public SearchCollection(Func<string, IEnumerable<T>> factory, IDiffHandler<T> handler)
            : base(handler, new DiffOptions { AllowBatching = false, DetectMoves = true })
        {
            _factory = factory;
            _query = new PropertyDebouncer<string>(Constants.TypingTimeout, SetQuery);
        }

        private readonly PropertyDebouncer<string> _query;
        public string Query
        {
            get => _query;
            set => _query.Set(value);
        }

        public void SetQuery(string value)
        {
            Update(_factory(value));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Query)));
        }

        public async void Update(IEnumerable<T> source)
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

                await incremental.LoadMoreItemsAsync(0);

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
            if (_token != null)
            {
                _token.Cancel();
            }

            return AsyncInfo.Run(async _ =>
            {
                var token = new CancellationTokenSource();

                _token = token;

                var result = await _incrementalSource?.LoadMoreItemsAsync(count);

                if (token.IsCancellationRequested)
                {
                    return result;
                }

                if (result.Count > 0)
                {
                    ReplaceDiff(_source);
                }

                return result;
            });
        }

        public bool HasMoreItems => _incrementalSource?.HasMoreItems ?? false;
    }
}
