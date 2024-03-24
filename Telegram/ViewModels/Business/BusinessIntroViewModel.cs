using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public class BusinessIntroViewModel : BusinessFeatureViewModelBase, IHandle
    {
        public BusinessIntroViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            _cached = cached?.BusinessInfo?.Intro.ToInput();

            Title = cached?.BusinessInfo?.Intro?.Title ?? string.Empty;
            Message = cached?.BusinessInfo?.Intro?.Message ?? string.Empty;
            Sticker = cached?.BusinessInfo?.Intro?.Sticker;

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateGreetingSticker>(this, Handle);
        }

        private void Handle(UpdateGreetingSticker update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(Sticker)));
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Invalidate(ref _title, value);
        }

        public int TitleMaxLength => (int)ClientService.Options.BusinessIntroTitleLengthMax;

        private string _message;
        public string Message
        {
            get => _message;
            set => Invalidate(ref _message, value);
        }

        public int MessageMaxLength => (int)ClientService.Options.BusinessIntroMessageLengthMax;

        private Sticker _sticker;
        public Sticker Sticker
        {
            get => _sticker;
            set => Invalidate(ref _sticker, value);
        }

        public void Clear()
        {
            Title = null;
            Message = null;
            Sticker = null;

            if (Sticker != null)
            {
                Sticker = null;
            }
            else
            {
                RaisePropertyChanged(nameof(Sticker));
            }

            RaisePropertyChanged(nameof(IsNotDefault));
        }

        private bool _isNotDefault;
        public bool IsNotDefault
        {
            get => _isNotDefault;
            set => Set(ref _isNotDefault, value);
        }

        public override bool HasChanged
        {
            get
            {
                var updated = GetSettings();
                IsNotDefault = updated != null;
                return !_cached.AreTheSame(updated);
            }
        }

        public override async void Continue()
        {
            var settings = GetSettings();
            if (settings != null)
            {
                if (string.IsNullOrEmpty(Title))
                {
                    RaisePropertyChanged("TITLE_INVALID");
                    return;
                }

                if (string.IsNullOrEmpty(Message))
                {
                    RaisePropertyChanged("MESSAGE_INVALID");
                    return;
                }
            }

            _completed = true;

            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessIntro(settings));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private InputBusinessIntro _cached;
        private InputBusinessIntro GetSettings()
        {
            if (string.IsNullOrEmpty(Title)
                && string.IsNullOrEmpty(Message)
                && Sticker == null)
            {
                return null;
            }

            return new InputBusinessIntro
            {
                Title = Title ?? string.Empty,
                Message = Message ?? string.Empty,
                Sticker = Sticker != null
                    ? new InputFileId(Sticker.StickerValue.Id)
                    : null
            };
        }
    }
}
