using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Photos;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Common;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Users
{
    public class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

        public UserPhotosViewModel(IMTProtoService protoService, TLUserFull userFull, TLUser user)
            : base(protoService, null, null)
        {
            //Items = new MvxObservableCollection<GalleryItem>();
            //Initialize(user);

            Items = new MvxObservableCollection<GalleryItem> { new GalleryPhotoItem(userFull.ProfilePhoto as TLPhoto, user) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user, userFull.ProfilePhoto.Id);
        }

        private async void Initialize(TLUser user, long maxId)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.GetUserPhotosAsync(user.ToInputUser(), 0, maxId, 0);
                if (response.IsSucceeded)
                {
                    if (response.Result is TLPhotosPhotosSlice slice)
                    {
                        TotalItems = slice.Count;
                    }
                    else
                    {
                        TotalItems = response.Result.Photos.Count;
                    }

                    foreach (var item in response.Result.Photos.Where(x => x.Id < maxId))
                    {
                        Items.Add(new GalleryPhotoItem(item as TLPhoto, user));
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            if (User != null && TotalItems > Items.Count)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    var response = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), Items.Count, 0, 0);
                    if (response.IsSucceeded)
                    {
                        Items.AddRange(response.Result.Photos.OfType<TLPhoto>().Select(x => new GalleryPhotoItem(x, _user)));
                    }
                }
            }
        }

        public override bool CanDelete => _user != null && _user.IsSelf;

        protected override async void DeleteExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync("Do you want to delete this photo?", "Delete", "OK", "Cancel");
            if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryPhotoItem item)
            {
                //var response = await ProtoService.UpdateProfilePhotoAsync(new TLInputPhotoEmpty());
                var response = await ProtoService.DeletePhotosAsync(new TLVector<TLInputPhotoBase> { new TLInputPhoto { Id = item.Photo.Id, AccessHash = item.Photo.AccessHash } });
                if (response.IsSucceeded)
                {
                    var index = Items.IndexOf(item);
                    if (index < Items.Count - 1)
                    {
                        Items.Remove(item);
                        SelectedItem = Items[index - 1];
                        TotalItems--;
                    }
                    else
                    {
                        NavigationService.GoBack();
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
