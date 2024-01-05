//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public interface IIncrementalCollectionOwner
    {
        Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count);

        bool HasMoreItems { get; }
    }

    // TODO: Does it make sense to replace local implementations with this?
    public class IncrementalCollection<T> : MvxObservableCollection<T>, ISupportIncrementalLoading
    {
        private readonly IIncrementalCollectionOwner _owner;

        public IncrementalCollection(IIncrementalCollectionOwner owner)
        {
            _owner = owner;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(token => _owner.LoadMoreItemsAsync(count));
        }

        public bool HasMoreItems => _owner.HasMoreItems;
    }
}
