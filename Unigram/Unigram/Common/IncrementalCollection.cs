﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

    public abstract class IncrementalCollection<T> : ObservableCollection<T>, IGroupSupportIncrementalLoading, INotifyPropertyChanged
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
                    RaisePropertyChanged("IsLoading");
                }
            }
        }

        //private bool _isRunning;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            System.Diagnostics.Debug.WriteLine("LoadMoreItemsAsync called");
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
                HasMoreItems = Count > 0 && Count > oldCount && GetHasMoreItems();

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
            System.Diagnostics.Debug.WriteLine("This count: " + this.Count + ", result count: " + result.Count);
            this.AddRange(result);
            /*this.AddRange(result.Except(this));
            foreach (T item in result)
            {   
                this.Add(item);
            }
            System.Diagnostics.Debug.WriteLine("This count: " + this.Count + ", result count: " + result.Count);*/
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
