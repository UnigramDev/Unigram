using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public class BusinessGreetViewModel : BusinessRecipientsViewModelBase, IHandle
    {
        public BusinessGreetViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (Invalidate(ref _isEnabled, value) && value)
                {
                    Replies ??= ClientService.GetQuickReplyShortcut("hello");
                }
            }
        }

        private QuickReplyShortcut _replies;
        public QuickReplyShortcut Replies
        {
            get => _replies;
            set => Invalidate(ref _replies, value);
        }

        private int _inactivityDays;
        public int InactivityDays
        {
            get => _inactivityDays;
            set => Invalidate(ref _inactivityDays, value);
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var settings = cached?.BusinessInfo?.GreetingMessageSettings;
            if (settings != null)
            {
                _cached = settings;

                IsEnabled = true;
                Replies = ClientService.GetQuickReplyShortcut(settings.ShortcutId);

                InactivityDays = settings.InactivityDays / 7 - 1;

                UpdateRecipients(settings.Recipients);
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateQuickReplyShortcut>(this, Handle);
        }

        private void Handle(UpdateQuickReplyShortcut update)
        {
            if (update.Shortcut.Name == "hello")
            {
                BeginOnUIThread(() => Replies = update.Shortcut);
            }
        }

        public void Create()
        {
            NavigationService.Navigate(typeof(ChatBusinessRepliesPage), new ChatBusinessRepliesIdNavigationArgs(ClientService.Options.MyId, "hello"));
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        public override async void Continue()
        {
            _completed = true;

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessGreetingMessageSettings(settings));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private BusinessGreetingMessageSettings _cached;
        private BusinessGreetingMessageSettings GetSettings()
        {
            if (IsEnabled)
            {
                return new BusinessGreetingMessageSettings
                {
                    ShortcutId = Replies?.Id ?? 0,
                    InactivityDays = (InactivityDays + 1) * 7,
                    Recipients = GetRecipients()
                };
            }

            return null;
        }
    }
}
