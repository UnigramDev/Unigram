//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Users
{
    public partial class UserPhotosViewModel : GalleryViewModelBase
    {
        private readonly User _user;

        // Photos shown by the gallery that getUserProfilePhotos does not return, and so are not
        // part of the offset it is paged by.
        private int _additionalPhotos;

        public UserPhotosViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, User user, UserFullInfo userFull)
            : base(clientService, storageService, aggregator)
        {
            _user = user;

            Items = new RangeObservableCollection<GalleryMedia>();

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

            if (userFull.Photo != null && user.ProfilePhoto?.Id == userFull.Photo.Id)
            {
                var photo = new ChatPhoto()
                {
                    Id = userFull.Photo.Id,
                    AddedDate = userFull.Photo.AddedDate,
                    Minithumbnail = userFull.Photo.Minithumbnail,
                    Sizes = new[]
                    {
                        new PhotoSize("x", user.ProfilePhoto.Small, 160, 160, Array.Empty<int>()),
                        new PhotoSize("y", user.ProfilePhoto.Big, 640, 640, Array.Empty<int>())
                    },
                    Animation = userFull.Photo.Animation,
                    SmallAnimation = userFull.Photo.SmallAnimation,
                    Sticker = userFull.Photo.Sticker
                };

                Items.Add(new GalleryChatPhoto(clientService, user, photo));
            }
            else if (userFull.Photo != null)
            {
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.Photo));
            }

            if (userFull.PublicPhoto != null && (userFull.Photo == null || user.Id != clientService.Options.MyId))
            {
                _additionalPhotos++;
                Items.Add(new GalleryChatPhoto(clientService, user, userFull.PublicPhoto, 0, false, user.Id == clientService.Options.MyId));
            }

            // The personal photo is ours, not one of the user's own, so it sits outside the
            // numbering the server reports.
            _offset = Items.Count > 0 && Items[0].IsPersonal ? -1 : 0;

            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(user);
        }

        private async void Initialize(User user)
        {
            IsLoading = true;

            try
            {
                var response = await ClientService.SendAsync(new GetUserProfilePhotos(_user.Id, _additionalPhotos, 20));
                if (response is not ChatPhotos photos)
                {
                    return;
                }

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
            finally
            {
                IsLoading = false;
            }
        }

        protected override async Task<IncrementalLoadResult> LoadNextAsync()
        {
            var response = await ClientService.SendAsync(new GetUserProfilePhotos(_user.Id, Items.Count - _additionalPhotos, 20));
            if (response is not ChatPhotos photos)
            {
                return new IncrementalLoadResult(0, false);
            }

            TotalItems = photos.TotalCount + _additionalPhotos;

            foreach (var item in photos.Photos)
            {
                Items.Add(new GalleryChatPhoto(ClientService, _user, item));
            }

            return new IncrementalLoadResult((uint)photos.Photos.Count, Items.Count - _additionalPhotos < photos.TotalCount);
        }

        public override RangeObservableCollection<GalleryMedia> Group => Items;

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
