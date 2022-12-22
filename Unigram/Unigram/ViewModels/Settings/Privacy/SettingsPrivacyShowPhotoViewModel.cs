using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using static Unigram.Services.GenerationService;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowPhotoViewModel : SettingsPrivacyViewModelBase, IDelegable<IUserDelegate>, IHandle<UpdateUserFullInfo>
    {
        public IUserDelegate Delegate { get; set; }

        public SettingsPrivacyShowPhotoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowProfilePhoto())
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo userFull))
            {
                Delegate?.UpdateUserFullInfo(null, null, userFull, false, false);
            }
            else
            {
                ClientService.Send(new GetUserFullInfo(ClientService.Options.MyId));
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUserFullInfo>(this, Handle);
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == ClientService.Options.MyId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(null, null, update.UserFullInfo, false, false));
            }
        }

        public async Task EditPhotoAsync(StorageMedia file)
        {
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
                var response = await ClientService.SendAsync(new SetProfilePhoto(new InputChatPhotoAnimation(generated, 0), true));
            }
            else if (file is StoragePhoto photo)
            {
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(photo.EditState));
                var response = await ClientService.SendAsync(new SetProfilePhoto(new InputChatPhotoStatic(generated), true));
            }
        }

        public async void RemovePhoto()
        {
            var popup = new MessagePopup();
            popup.Title = Strings.Resources.RemovePublicPhoto;
            popup.Message = Strings.Resources.RemovePhotoForRestDescription;
            popup.PrimaryButtonText = Strings.Resources.Remove;
            popup.SecondaryButtonText = Strings.Resources.Cancel;
            popup.PrimaryButtonStyle = App.Current.Resources["DangerButtonStyle"] as Style;
            popup.DefaultButton = Windows.UI.Xaml.Controls.ContentDialogButton.None;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo userFull))
                {
                    if (userFull.PublicPhoto == null)
                    {
                        return;
                    }

                    ClientService.Send(new DeleteProfilePhoto(userFull.PublicPhoto.Id));
                }
            }
        }
    }
}
