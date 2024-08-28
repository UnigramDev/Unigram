//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;

namespace Telegram.ViewModels.Users
{
    public partial class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly User _user;

        public UserPhotosViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, User user, UserFullInfo userFull)
            : base(clientService, storageService, aggregator)
        {
            _user = user;

            Items = new MvxObservableCollection<GalleryMedia>();

            if (userFull.PersonalPhoto != null)
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PersonalPhoto, 0, true, false));
            }
            //if (userFull.PublicPhoto != null && user.Id == clientService.Options.MyId)
            //{
            //    _additionalPhotos++;
            //    Items.Add(new GalleryChatPhoto(clientService, user, userFull.PublicPhoto, 0, false, true));
            //}

            if (userFull.Photo != null)
            {
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.Photo));
            }

            if (userFull.PublicPhoto != null && (userFull.Photo == null || user.Id != clientService.Options.MyId))
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PublicPhoto, 0, false, user.Id == clientService.Options.MyId));
            }

            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user);
        }

        private async void Initialize(User user)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ClientService.SendAsync(new GetUserProfilePhotos(_user.Id, 0, 20));
                if (response is ChatPhotos photos)
                {
                    TotalItems = photos.TotalCount + _additionalPhotos;

                    foreach (var item in photos.Photos)
                    {
                        if (item.Id == user.ProfilePhoto.Id)
                        {
                            continue;
                        }

                        Items.Add(new GalleryChatPhoto(ClientService, _user, item));
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ClientService.SendAsync(new GetUserProfilePhotos(_user.Id, Items.Count - _additionalPhotos, 20));
                if (response is ChatPhotos photos)
                {
                    TotalItems = photos.TotalCount + _additionalPhotos;

                    foreach (var item in photos.Photos)
                    {
                        Items.Add(new GalleryChatPhoto(ClientService, _user, item));
                    }
                }
            }
        }

        public override MvxObservableCollection<GalleryMedia> Group => Items;

        public override bool CanDelete => _user != null && _user.Id == ClientService.Options.MyId;

        public override async void Delete()
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureDeletePhoto, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryChatPhoto profileItem)
            {
                var response = await ClientService.SendAsync(new DeleteProfilePhoto(profileItem.Id));
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

        public void SetAsMain()
        {
            var item = _selectedItem as GalleryChatPhoto;
            if (item == null)
            {
                return;
            }

            ClientService.Send(new SetProfilePhoto(new InputChatPhotoPrevious(item.Id), false));
            ShowToast(item.IsVideo ? Strings.MainProfileVideoSetHint : Strings.MainProfilePhotoSetHint);
        }
    }
}
