using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class UserPhotosViewModel : UnigramViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

        public UserPhotosViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLPhotoBase>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var user = parameter as TLUser;
            if (user != null)
            {
                User = user;

                using (await _loadMoreLock.WaitAsync())
                {
                    var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), 0, 0, 0);
                    if (result.IsSucceeded)
                    {
                        if (result.Value is TLPhotosPhotosSlice)
                        {
                            var slice = result.Value as TLPhotosPhotosSlice;
                            TotalItems = slice.Count;
                        }
                        else
                        {
                            TotalItems = result.Value.Photos.Count;
                        }

                        foreach (var photo in result.Value.Photos)
                        {
                            Items.Add(photo);
                        }

                        SelectedItem = Items[0];
                    }
                }
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            return Task.CompletedTask;
        }

        private async void LoadMore()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), Items.Count, 0, 0);
                if (result.IsSucceeded)
                {
                    foreach (var photo in result.Value.Photos)
                    {
                        Items.Add(photo);
                    }
                }
            }
        }

        private TLUser _user;
        public TLUser User
        {
            get
            {
                return _user;
            }
            set
            {
                Set(ref _user, value);
            }
        }

        public int SelectedIndex
        {
            get
            {
                if (Items == null || SelectedItem == null)
                {
                    return 0;
                }
                if (Items.IndexOf(SelectedItem) == Items.Count - 1)
                {
                    LoadMore();
                }

                return Items.IndexOf(SelectedItem) + 1;
            }
        }

        private int _totalItems;
        public int TotalItems
        {
            get
            {
                return _totalItems;
            }
            set
            {
                Set(ref _totalItems, value);
            }
        }

        private TLPhotoBase _selectedItem;
        public TLPhotoBase SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                RaisePropertyChanged(() => SelectedIndex);
            }
        }

        public ObservableCollection<TLPhotoBase> Items { get; private set; }
    }
}
