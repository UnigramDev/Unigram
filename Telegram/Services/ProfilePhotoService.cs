//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public interface IProfilePhotoService
    {
        Task<InputChatPhoto> PreviewSetPhotoAsync(INavigationService navigation);
        Task<bool> SetPhotoAsync(INavigationService navigation, long? chatId, bool isPublic = false, bool isPersonal = false);
        Task<InputChatPhoto> PreviewCreatePhotoAsync(INavigationService navigation);
        Task<bool> CreatePhotoAsync(INavigationService navigation, long? chatId, bool isPublic = false, bool isPersonal = false);
    }

    public partial class ProfilePhotoService : IProfilePhotoService
    {
        private readonly IClientService _clientService;

        public ProfilePhotoService(IClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task<InputChatPhoto> PreviewSetPhotoAsync(INavigationService navigation)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var media = await picker.PickSingleMediaAsync();
                if (media is StoragePhoto or StorageVideo)
                {
                    var popup = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await popup.ShowAsync(navigation.XamlRoot);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        return await EditPhotoAsync(navigation, media);
                    }
                }
                else if (media is StorageInvalid)
                {
                    await navigation.ShowPopupAsync(Strings.OpenImageUnsupported, Strings.AppName, Strings.OK);
                }
            }
            catch { }

            return null;
        }

        public async Task<bool> SetPhotoAsync(INavigationService navigation, long? chatId, bool isPublic, bool isPersonal)
        {
            var inputPhoto = await PreviewSetPhotoAsync(navigation);
            if (inputPhoto != null)
            {
                return await Complete(navigation, chatId, isPublic, isPersonal, inputPhoto);
            }

            return false;
        }

        public async Task<InputChatPhoto> PreviewCreatePhotoAsync(INavigationService navigation)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await navigation.ShowPopupAsync(new CreateChatPhotoPopup(tsc));
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return await tsc.Task as InputChatPhoto;
        }

        public async Task<bool> CreatePhotoAsync(INavigationService navigation, long? chatId, bool isPublic, bool isPersonal)
        {
            var inputPhoto = await PreviewCreatePhotoAsync(navigation);
            if (inputPhoto != null)
            {
                return await Complete(navigation, chatId, isPublic, isPersonal, inputPhoto);
            }

            return false;
        }

        private async Task<InputChatPhoto> EditPhotoAsync(INavigationService navigation, StorageMedia file)
        {
            file.EditState.MinimumSize = 160;

            InputChatPhoto inputPhoto = null;
            if (file is StorageVideo media)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                var seconds = duration.TotalSeconds;

                var generation = new VideoGeneration();
                generation.Mute = true;
                generation.TrimStartTime = media.EditState.TrimStartTime;
                generation.TrimStopTime = media.EditState.TrimStartTime + TimeSpan.FromSeconds(Math.Min(seconds, 9.9));
                generation.Transcode = true;
                generation.Transform = true;
                //conversion.Rotation = file.EditState.Rotation;
                generation.OutputSize = new Size(640, 640);
                //conversion.Mirror = transform.Mirror;
                generation.VideoBitrate = 1000000;
                generation.AudioBitrate = 1000000;
                generation.CropRectangle = new Rect(
                    media.EditState.Rectangle.X * props.Width,
                    media.EditState.Rectangle.Y * props.Height,
                    media.EditState.Rectangle.Width * props.Width,
                    media.EditState.Rectangle.Height * props.Height);

                var rectangle = generation.CropRectangle;
                rectangle.Width = Math.Min(generation.CropRectangle.Width, generation.CropRectangle.Height);
                rectangle.Height = rectangle.Width;

                generation.CropRectangle = rectangle;

                var serialized = JsonSerializer.Serialize(generation, GenerationJsonContext.Default.VideoGeneration);
                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, serialized);
                inputPhoto = new InputChatPhotoAnimation(generated, 0);
            }
            else if (file is StoragePhoto photo)
            {
                var serialized = JsonSerializer.Serialize(photo.EditState, GenerationJsonContext.Default.ImageGeneration);
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, serialized);
                inputPhoto = new InputChatPhotoStatic(generated);
            }

            return inputPhoto;
        }

        private async Task<bool> Complete(INavigationService navigation, long? chatId, bool isPublic, bool isPersonal, InputChatPhoto inputPhoto)
        {
            if (chatId.HasValue && _clientService.TryGetUser(chatId.Value, out User user))
            {
                if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                {
                    _clientService.Send(new SetBotProfilePhoto(user.Id, inputPhoto));
                    return true;
                }
                else if (isPersonal)
                {
                    var confirm = await navigation.ShowPopupAsync(string.Format(Strings.SetUserPhotoAlertMessage, user.FirstName, user.FirstName), Strings.AppName, Strings.SetPhoto, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new SetUserPersonalProfilePhoto(user.Id, inputPhoto));
                        return true;
                    }
                }
                else
                {
                    var confirm = await navigation.ShowPopupAsync(string.Format(Strings.SuggestPhotoAlertMessage, user.FirstName), Strings.AppName, Strings.SuggestPhotoShort, Strings.Cancel);
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
