using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml.Navigation;
using static Unigram.Services.GenerationService;

namespace Unigram.ViewModels.Settings
{
    public class SettingsProfileViewModel : TLViewModelBase, IDelegable<IUserDelegate>, IHandle<UpdateUser>, IHandle<UpdateUserFullInfo>
    {
        public IUserDelegate Delegate { get; set; }

        public SettingsProfileViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(Send, CanSend);
        }

        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (Set(ref _firstName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set
            {
                if (Set(ref _lastName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _bio;
        public string Bio
        {
            get => _bio;
            set
            {
                if (Set(ref _bio, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int BioLengthMax => (int)CacheService.Options.BioLengthMax;

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Aggregator.Subscribe(this);

            if (CacheService.TryGetUser(CacheService.Options.MyId, out User user))
            {
                FirstName = user.FirstName;
                LastName = user.LastName;

                Delegate?.UpdateUser(null, user, false);

                if (CacheService.TryGetUserFull(user.Id, out UserFullInfo userFull))
                {
                    Bio = userFull.Bio.Text;

                    Delegate?.UpdateUserFullInfo(null, user, userFull, false, false);
                }
                else
                {
                    ProtoService.Send(new GetUserFullInfo(user.Id));
                }
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(suspensionState, suspending);
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == CacheService.Options.MyId)
            {
                BeginOnUIThread(() =>
                {
                    FirstName = update.User.FirstName;
                    LastName = update.User.LastName;

                    Delegate?.UpdateUser(null, update.User, false);
                });
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == CacheService.Options.MyId && CacheService.TryGetUser(CacheService.Options.MyId, out User user))
            {
                BeginOnUIThread(() =>
                {
                    Bio = update.UserFullInfo.Bio.Text;

                    Delegate?.UpdateUserFullInfo(null, user, update.UserFullInfo, false, false);
                });
            }
        }

        public RelayCommand SendCommand { get; }
        private async void Send()
        {
            if (CacheService.TryGetUser(CacheService.Options.MyId, out User user) && CacheService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (string.IsNullOrEmpty(_firstName))
                {
                    _firstName = _lastName;
                }

                if (string.IsNullOrEmpty(_firstName))
                {
                    return;
                }

                if (!string.Equals(_firstName, user.FirstName) || !string.Equals(_lastName, user.LastName))
                {
                    var response = await ProtoService.SendAsync(new SetName(_firstName, _lastName));
                    if (response is Error error)
                    {
                        // TODO:
                        return;
                    }
                }

                if (!string.Equals(_bio, userFull.Bio))
                {
                    var response = await ProtoService.SendAsync(new SetBio(_bio));
                    if (response is Error error)
                    {
                        // TODO:
                        return;
                    }
                }

                NavigationService.GoBack();
            }
        }

        private bool CanSend()
        {
            return _firstName.Length > 0
                && _firstName.Length <= 64
                && _lastName.Length <= 64
                && _bio.Length <= CacheService.Options.BioLengthMax;
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
                var response = await ProtoService.SendAsync(new SetProfilePhoto(new InputChatPhotoAnimation(generated, 0)));
            }
            else if (file is StoragePhoto photo)
            {
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(photo.EditState));
                var response = await ProtoService.SendAsync(new SetProfilePhoto(new InputChatPhotoStatic(generated)));
            }
        }
    }
}
