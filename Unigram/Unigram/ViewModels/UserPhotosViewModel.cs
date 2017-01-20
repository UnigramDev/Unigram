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
    public class UserPhotosViewModel : PhotosViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

        public UserPhotosViewModel(TLUser user, IMTProtoService protoService)
            : base(protoService, null, null)
        {
            Items = new ObservableCollection<object>();
            Initialize(user);
        }

        private async void Initialize(TLUser user)
        {
            User = user;

            using (await _loadMoreLock.WaitAsync())
            {
                var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), 0, 0, 0);
                if (result.IsSucceeded)
                {
                    if (result.Result is TLPhotosPhotosSlice)
                    {
                        var slice = result.Result as TLPhotosPhotosSlice;
                        TotalItems = slice.Count;
                    }
                    else
                    {
                        TotalItems = result.Result.Photos.Count;
                    }

                    Items.Clear();

                    foreach (var photo in result.Result.Photos)
                    {
                        Items.Add(photo);
                    }

                    SelectedItem = Items[0];
                }
            }
        }

        protected override async void LoadNext()
        {
            if (User != null)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), Items.Count, 0, 0);
                    if (result.IsSucceeded)
                    {
                        foreach (var photo in result.Result.Photos)
                        {
                            Items.Add(photo);
                        }
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
    }
}
