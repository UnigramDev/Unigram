using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Users
{
    public class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly User _user;

        public UserPhotosViewModel(IProtoService protoService, IEventAggregator aggregator, User user)
            : base(protoService, aggregator)
        {
            _user = user;

            Items = new MvxObservableCollection<GalleryItem> { new GalleryProfilePhotoItem(protoService, user) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user);
        }

        private async void Initialize(User user)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_user.Id, 0, 20));
                if (response is UserProfilePhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        if (item.Id == user.ProfilePhoto.Id && Items[0] is GalleryProfilePhotoItem main)
                        {
                            main.SetDate(item.AddedDate);
                        }
                        else
                        {
                            Items.Add(new GalleryUserProfilePhotoItem(ProtoService, _user, item));
                        }
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_user.Id, Items.Count, 20));
                if (response is UserProfilePhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        Items.Add(new GalleryUserProfilePhotoItem(ProtoService, _user, item));
                    }
                }
            }
        }

        public override MvxObservableCollection<GalleryItem> Group => this.Items;

        public override bool CanDelete => _user != null && _user.Id == ProtoService.GetMyId();

        protected override async void DeleteExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureDeletePhoto, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryProfilePhotoItem item)
            {
                var response = await ProtoService.SendAsync(new DeleteProfilePhoto(item.Id));
                if (response is Ok)
                {
                    NavigationService.GoBack();

                    //var index = Items.IndexOf(item);
                    //if (index < Items.Count - 1)
                    //{
                    //    Items.Remove(item);
                    //    SelectedItem = Items[index > 0 ? index - 1 : index];
                    //    TotalItems--;
                    //}
                    //else
                    //{
                    //    NavigationService.GoBack();
                    //}
                }
            }
            else if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryUserProfilePhotoItem profileItem)
            {
                var response = await ProtoService.SendAsync(new DeleteProfilePhoto(profileItem.Id));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                }
            }
        }
    }
}
