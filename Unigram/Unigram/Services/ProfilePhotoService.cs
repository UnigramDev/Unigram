//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Navigation.Services;
using Unigram.ViewModels;
using Unigram.Views.Popups;
using Windows.Foundation;
using Windows.Storage.Pickers;
using static Unigram.Services.GenerationService;

namespace Unigram.Services
{
    public interface IProfilePhotoService
    {
        Task<bool> SetPhotoAsync(INavigationService navigation, long? chatId, bool isPublic = false, bool isPersonal = false);
        Task<bool> CreatePhotoAsync(INavigationService navigation, long? chatId, bool isPublic = false, bool isPersonal = false);
    }

    public class ProfilePhotoService : IProfilePhotoService
    {
        private readonly IClientService _clientService;
        private readonly IStorageService _storageService;

        public ProfilePhotoService(IClientService clientService, IStorageService storageService)
        {
            _clientService = clientService;
            _storageService = storageService;
        }

        public async Task<bool> SetPhotoAsync(INavigationService navigation, long? chatId, bool isPublic, bool isPersonal)
        {
            var media = await _storageService.PickSingleMediaAsync(navigation.XamlRoot, PickerLocationId.PicturesLibrary, Constants.MediaTypes);
            if (media != null)
            {
                var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    return await EditPhotoAsync(navigation.XamlRoot, chatId, isPublic, isPersonal, media);
                }
            }

            return false;
        }

        public async Task<bool> CreatePhotoAsync(INavigationService navigation, long? chatId, bool isPublic, bool isPersonal)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await navigation.ShowAsync(typeof(CreateChatPhotoPopup), new CreateChatPhotoParameters(chatId, isPublic, isPersonal), tsc);
            if (confirm != ContentDialogResult.Primary)
            {
                return false;
            }

            var success = await tsc.Task;
            if (success is bool value)
            {
                return value;
            }

            return false;
        }

        private async Task<bool> EditPhotoAsync(XamlRoot xamlRoot, long? chatId, bool isPublic, bool isPersonal, StorageMedia file)
        {
            InputChatPhoto inputPhoto;
            if (file is StorageVideo media)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                var seconds = duration.TotalSeconds;

                var conversion = new VideoConversion();
                conversion.Mute = true;
                conversion.TrimStartTime = media.EditState.TrimStartTime;
                conversion.TrimStopTime = media.EditState.TrimStartTime + TimeSpan.FromSeconds(Math.Min(seconds, 9.9));
                conversion.Transcode = true;
                conversion.Transform = true;
                //conversion.Rotation = file.EditState.Rotation;
                conversion.OutputSize = new Size(640, 640);
                //conversion.Mirror = transform.Mirror;
                conversion.VideoBitrate = 1000000;
                conversion.AudioBitrate = 1000000;
                conversion.CropRectangle = new Rect(
                    media.EditState.Rectangle.X * props.Width,
                    media.EditState.Rectangle.Y * props.Height,
                    media.EditState.Rectangle.Width * props.Width,
                    media.EditState.Rectangle.Height * props.Height);

                var rectangle = conversion.CropRectangle;
                rectangle.Width = Math.Min(conversion.CropRectangle.Width, conversion.CropRectangle.Height);
                rectangle.Height = rectangle.Width;

                conversion.CropRectangle = rectangle;

                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
                inputPhoto = new InputChatPhotoAnimation(generated, 0);
            }
            else if (file is StoragePhoto photo)
            {
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(photo.EditState));
                inputPhoto = new InputChatPhotoStatic(generated);
            }
            else
            {
                return false;
            }

            if (chatId.HasValue && _clientService.TryGetUser(chatId.Value, out User user))
            {
                if (isPersonal)
                {
                    var confirm = await MessagePopup.ShowAsync(xamlRoot, string.Format(Strings.Resources.SetUserPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new SetUserPersonalProfilePhoto(user.Id, inputPhoto));
                        return true;
                    }
                }
                else
                {
                    var confirm = await MessagePopup.ShowAsync(xamlRoot, string.Format(Strings.Resources.SuggestPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new SuggestUserProfilePhoto(user.Id, inputPhoto));
                        return true;
                    }
                }
            }
            else if (chatId.HasValue)
            {
                _clientService.Send(new SetChatPhoto(chatId.Value, inputPhoto));
                return true;
            }
            else
            {
                _clientService.Send(new SetProfilePhoto(inputPhoto, isPublic));
                return true;
            }

            return false;
        }
    }
}
