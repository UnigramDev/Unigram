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
using Windows.UI.Xaml.Controls;
using static Unigram.Services.GenerationService;

namespace Unigram.Services
{
    public interface IProfilePhotoService
    {
        Task<bool> SetPhotoAsync(long? chatId, bool isPublic = false, bool isPersonal = false);
        Task<bool> CreatePhotoAsync(INavigationService navigation, long? chatId, bool isPublic = false, bool isPersonal = false);
    }

    public class ProfilePhotoService : IProfilePhotoService
    {
        private readonly IClientService _clientService;

        public ProfilePhotoService(IClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task<bool> SetPhotoAsync(long? chatId, bool isPublic, bool isPersonal)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var media = await picker.PickSingleMediaAsync();
                if (media != null)
                {
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        return await EditPhotoAsync(chatId, isPublic, isPersonal, media);
                    }
                }
            }
            catch { }

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

        private async Task<bool> EditPhotoAsync(long? chatId, bool isPublic, bool isPersonal, StorageMedia file)
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
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.SetUserPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new SetUserPersonalProfilePhoto(user.Id, inputPhoto));
                        return true;
                    }
                }
                else
                {
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.SuggestPhotoAlertMessage, user.FirstName), Strings.Resources.AppName, Strings.Resources.SuggestPhotoShort, Strings.Resources.Cancel);
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
