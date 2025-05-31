using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupFeedbackGroupViewModel : SupergroupViewModelBase
    {
        public SupergroupFeedbackGroupViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Invalidate(ref _isEnabled, value);
        }

        private int _paidMessageStarCount = 0;
        public int PaidMessageStarCount
        {
            get => _paidMessageStarCount;
            set => Invalidate(ref _paidMessageStarCount, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup) && ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                if (ClientService.TryGetChat(fullInfo.FeedbackChatId, out Chat feedbackChat))
                {
                    _cached = new SetChatFeedbackGroup(chat.Id, true, ClientService.PaidMessageStarCount(feedbackChat));
                }
                else
                {
                    _cached = new SetChatFeedbackGroup(chat.Id, false, 20);
                }
            }
            else
            {
                _cached = new SetChatFeedbackGroup(chat.Id, false, 20);
            }

            IsEnabled = _cached.IsEnabled;
            PaidMessageStarCount = (int)_cached.PaidMessageStarCount;

            return Task.CompletedTask;
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (!_completed && HasChanged)
            {
                args.Cancel = true;

                // TODO: translate
                var confirm = await ShowPopupAsync("You have changed direct message settings. Apply changes?", Strings.UnsavedChanges, Strings.ChatThemeSaveDialogApply, Strings.ChatThemeSaveDialogDiscard);
                if (confirm == ContentDialogResult.Primary)
                {
                    Continue();
                }
                else if (confirm == ContentDialogResult.Secondary)
                {
                    _completed = true;
                    NavigationService.GoBack();
                }
            }
        }

        protected bool _completed;
        public bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected bool Invalidate<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Set(ref storage, value, propertyName))
            {
                RaisePropertyChanged(nameof(HasChanged));
                return true;
            }

            return false;
        }

        public async void Continue()
        {
            _completed = true;

            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(settings);
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else
            {
                // TODO
            }
        }

        private SetChatFeedbackGroup _cached;
        private SetChatFeedbackGroup GetSettings()
        {
            return new SetChatFeedbackGroup
            {
                ChatId = Chat.Id,
                IsEnabled = IsEnabled,
                PaidMessageStarCount = PaidMessageStarCount
            };
        }
    }
}
