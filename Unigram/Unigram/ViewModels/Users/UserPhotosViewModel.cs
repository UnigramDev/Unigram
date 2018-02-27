using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Users
{
    public class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly int _userId;

        public UserPhotosViewModel(IProtoService protoService, IEventAggregator aggregator, User user)
            : base(protoService, aggregator)
        {
            //Items = new MvxObservableCollection<GalleryItem>();
            //Initialize(user);

            _userId = user.Id;
            //_user = user;

            Items = new MvxObservableCollection<GalleryItem> { new GalleryProfilePhotoItem(protoService, user.ProfilePhoto) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user);
        }

        private async void Initialize(User user)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_userId, 1, 20));
                if (response is UserProfilePhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        Items.Add(new GalleryPhotoItem(ProtoService, item));
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_userId, Items.Count, 20));
                if (response is UserProfilePhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        Items.Add(new GalleryPhotoItem(ProtoService, item));
                    }
                }
            }
        }

        public override MvxObservableCollection<GalleryItem> Group => this.Items;

        public override bool CanDelete => _user != null && _user.IsSelf;

        protected override async void DeleteExecute()
        {
            //var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureDeletePhoto, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            //if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryPhotoItem item)
            //{
            //    //var response = await ProtoService.UpdateProfilePhotoAsync(new TLInputPhotoEmpty());
            //    var response = await LegacyService.DeletePhotosAsync(new TLVector<TLInputPhotoBase> { new TLInputPhoto { Id = item.Photo.Id, AccessHash = item.Photo.AccessHash } });
            //    if (response.IsSucceeded)
            //    {
            //        var index = Items.IndexOf(item);
            //        if (index < Items.Count - 1)
            //        {
            //            Items.Remove(item);
            //            SelectedItem = Items[index > 0 ? index - 1 : index];
            //            TotalItems--;
            //        }
            //        else
            //        {
            //            NavigationService.GoBack();
            //        }
            //    }
            //}
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
