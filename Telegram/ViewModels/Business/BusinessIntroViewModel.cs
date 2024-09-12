using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessIntroViewModel : BusinessFeatureViewModelBase, IHandle
    {
        public BusinessIntroViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            _cached = cached?.BusinessInfo?.StartPage.ToInput();

            Title = cached?.BusinessInfo?.StartPage?.Title ?? string.Empty;
            Message = cached?.BusinessInfo?.StartPage?.Message ?? string.Empty;
            Sticker = cached?.BusinessInfo?.StartPage?.Sticker;

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

        public int TitleMaxLength => (int)ClientService.Options.BusinessStartPageTitleLengthMax;

        private string _message;
        public string Message
        {
            get => _message;
            set => Invalidate(ref _message, value);
        }

        public int MessageMaxLength => (int)ClientService.Options.BusinessStartPageMessageLengthMax;

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
            _completed = true;

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessStartPage(settings));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private InputBusinessStartPage _cached;
        private InputBusinessStartPage GetSettings()
        {
            if (string.IsNullOrEmpty(Title)
                && string.IsNullOrEmpty(Message)
                && Sticker == null)
            {
                return null;
            }

            return new InputBusinessStartPage
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
