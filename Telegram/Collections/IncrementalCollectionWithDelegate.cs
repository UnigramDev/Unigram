//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
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
