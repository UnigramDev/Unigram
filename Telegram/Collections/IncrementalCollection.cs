//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public interface IGroupSupportIncrementalLoading : ISupportIncrementalLoading
    {

    }

    public abstract class IncrementalCollection<T> : MvxObservableCollection<T>, IGroupSupportIncrementalLoading
    {
        private bool _hasMoreItems = true;
        public bool HasMoreItems
        {
            get => _hasMoreItems;
            set => _hasMoreItems = value;
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (value != _isLoading)
                {
                    _isLoading = value;
                    RaisePropertyChanged("IsLoading");
                }
            }
        }

        //private bool _isRunning;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            //var dispatcher = Window.Current.Dispatcher;

            //return Task.Run(async () =>
            //{
            //    if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            //        return new LoadMoreItemsResult() { Count = 0 };

            //    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //    {
            //        IsLoading = true;
            //    });

            //    IEnumerable<T> result;

            //    try
            //    {
            //        result = await LoadDataAsync();
            //    }
            //    catch
            //    {
            //        result = new T[0];
            //    }

            //    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //    {
            //        var oldCount = Count;
            //        Merge(result);
            //        HasMoreItems = Count > 0 && Count > oldCount && GetHasMoreItems();
            //        IsLoading = false;
            //    });

            //    return new LoadMoreItemsResult() { Count = (uint)result.Count() };

            //}).AsAsyncOperation();

            return AsyncInfo.Run(async token =>
            {
                var result = await LoadDataAsync();
                var oldCount = Count;
                Merge(result);
                HasMoreItems = /*Count > 0 &&*/ /*Count > oldCount ||*/ GetHasMoreItems();

                return new LoadMoreItemsResult { Count = (uint)result.Count };
            });
        }

        public abstract Task<IList<T>> LoadDataAsync();

        protected virtual bool GetHasMoreItems()
        {
            return true;
        }

        protected virtual void Merge(IList<T> result)
        {
            foreach (T item in result)
            {
                Add(item);
            }
        }
    }
}
