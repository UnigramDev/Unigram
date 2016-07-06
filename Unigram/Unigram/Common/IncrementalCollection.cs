using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Common
{
    public interface IGroupSupportIncrementalLoading : ISupportIncrementalLoading
    {

    }

    public abstract class IncrementalCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading, INotifyPropertyChanged
    {
        private bool _hasMoreItems = true;
        public bool HasMoreItems
        {
            get
            {
                return _hasMoreItems;
            }
            set
            {
                _hasMoreItems = value;
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                if (value != _isLoading)
                {
                    _isLoading = value;
                    NotifyPropertyChanged("IsLoading");
                }
            }
        }

        //private bool _isRunning;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var dispatcher = Window.Current.Dispatcher;

            return Task.Run(async () =>
            {
                if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
                    return new LoadMoreItemsResult() { Count = 0 };

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    IsLoading = true;
                });

                IEnumerable<T> result;

                try
                {
                    result = await LoadDataAsync();
                }
                catch
                {
                    result = new T[0];
                }

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var oldCount = Count;
                    Merge(result);
                    HasMoreItems = Count > 0 && Count > oldCount && GetHasMoreItems();
                    IsLoading = false;
                });

                return new LoadMoreItemsResult() { Count = (uint)result.Count() };

            }).AsAsyncOperation();
        }

        public abstract Task<IEnumerable<T>> LoadDataAsync();

        protected virtual bool GetHasMoreItems()
        {
            return true;
        }

        protected virtual void Merge(IEnumerable<T> result)
        {
            foreach (T item in result)
            {
                this.Add(item);
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
