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
using Telegram.Api.TL;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class UserPhotosViewModel : UnigramViewModelBase, IHandle<DownloadableItem>, IHandle
    {
        public UserPhotosViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLPhotoBase>();
        }

        public void Handle(DownloadableItem item)
        {
            //if (SelectedItem == item.Owner)
            {
                RaisePropertyChanged(() => SelectedItem);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);

            var user = parameter as TLUser;
            if (user != null)
            {
                User = user;

                var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), 0, 0, 0);
                if (result.IsSucceeded)
                {
                    foreach (var photo in result.Value.Photos)
                    {
                        Items.Add(photo);
                    }

                    SelectedItem = Items[0];
                }
            }
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
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

                return Items.IndexOf(SelectedItem) + 1;
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
