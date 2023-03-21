//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Users
{
    public class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly User _user;

        public UserPhotosViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, User user, UserFullInfo userFull)
            : base(clientService, storageService, aggregator)
        {
            _user = user;

            Items = new MvxObservableCollection<GalleryContent>();

            if (userFull.PersonalPhoto != null)
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PersonalPhoto, 0, true, false));
            }
            if (userFull.PublicPhoto != null && user.Id == clientService.Options.MyId)
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PublicPhoto, 0, false, true));
            }

            if (userFull.Photo != null)
            {
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.Photo));
            }

            if (userFull.PublicPhoto != null && userFull.Photo == null && user.Id != clientService.Options.MyId)
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PublicPhoto, 0, false, true));
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

        public override MvxObservableCollection<GalleryContent> Group => Items;

        public override bool CanDelete => _user != null && _user.Id == ClientService.Options.MyId;

        protected override async void DeleteExecute()
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
    }
}
