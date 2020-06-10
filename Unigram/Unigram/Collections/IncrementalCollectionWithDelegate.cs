using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class IncrementalCollectionWithDelegate<T> : MvxObservableCollection<T>, ISupportIncrementalLoading
    {
        private readonly ISupportIncrementalLoading _delegate;

        public IncrementalCollectionWithDelegate(ISupportIncrementalLoading delegato)
        {
            _delegate = delegato;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return _delegate.LoadMoreItemsAsync(count);
        }

        public bool HasMoreItems => _delegate.HasMoreItems;
    }
}
