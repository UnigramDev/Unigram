using System;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Users
{
    public class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly User _user;

        public UserPhotosViewModel(IProtoService protoService, IEventAggregator aggregator, User user, UserFullInfo userFull)
            : base(protoService, aggregator)
        {
            _user = user;

            Items = new MvxObservableCollection<GalleryContent> { new GalleryChatPhoto(protoService, user, userFull.Photo) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user);
        }

        private async void Initialize(User user)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_user.Id, 0, 20));
                if (response is ChatPhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        if (item.Id == user.ProfilePhoto.Id)
                        {
                            continue;
                        }

                        Items.Add(new GalleryChatPhoto(ProtoService, _user, item));
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetUserProfilePhotos(_user.Id, Items.Count, 20));
                if (response is ChatPhotos photos)
                {
                    TotalItems = photos.TotalCount;

                    foreach (var item in photos.Photos)
                    {
                        Items.Add(new GalleryChatPhoto(ProtoService, _user, item));
                    }
                }
            }
        }

        public override MvxObservableCollection<GalleryContent> Group => this.Items;

        public override bool CanDelete => _user != null && _user.Id == ProtoService.Options.MyId;

        protected override async void DeleteExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureDeletePhoto, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryChatPhoto profileItem)
            {
                var response = await ProtoService.SendAsync(new DeleteProfilePhoto(profileItem.Id));
                if (response is Ok)
                {
                    var index = Items.IndexOf(profileItem);
                    if (index < Items.Count - 1)
                    {
                        SelectedItem = Items[index > 0 ? index - 1 : index + 1];
                        Items.Remove(profileItem);
                        TotalItems--;
                    }
                    else
                    {
                        NavigationService.GoBack();
                    }
                }
            }
        }
    }
}
