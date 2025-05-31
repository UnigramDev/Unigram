using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupTopicsViewModel : SupergroupViewModelBase
    {
        public SupergroupTopicsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
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
            set => SetIsEnabled(value);
        }

        private async void SetIsEnabled(bool value)
        {
            Set(ref _isEnabled, value, nameof(IsEnabled));

            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, value, UseTabsLayout));
            }
        }

        enum LayoutType
        {
            Tabs,
            List
        }

        private LayoutType _layoutType;

        public bool UseTabsLayout
        {
            get => _layoutType == LayoutType.Tabs;
            set
            {
                if (value)
                {
                    SetLayoutType(LayoutType.Tabs);
                }
            }
        }

        public bool UseListLayout
        {
            get => _layoutType == LayoutType.List;
            set
            {
                if (value)
                {
                    SetLayoutType(LayoutType.List);
                }
            }
        }

        private async void SetLayoutType(LayoutType type)
        {
            if (_layoutType == type)
            {
                return;
            }

            _layoutType = type;

            RaisePropertyChanged(nameof(UseTabsLayout));
            RaisePropertyChanged(nameof(UseListLayout));

            if (Chat.Type is ChatTypeBasicGroup)
            {
                Chat = await UpgradeAsync(Chat);
            }

            if (Chat.Type is ChatTypeSupergroup supergroup)
            {
                ClientService.Send(new ToggleSupergroupIsForum(supergroup.SupergroupId, true, type == LayoutType.Tabs));

                if (type == LayoutType.Tabs)
                {
                    Aggregator.Publish(new UpdateChatViewAsTopics(Chat.Id, false));
                }
            }
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

            if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                Set(ref _isEnabled, supergroup.IsForum, nameof(IsEnabled));

                _layoutType = supergroup.HasForumTabs
                    ? LayoutType.Tabs
                    : LayoutType.List;
            }
            else
            {
                Set(ref _isEnabled, false, nameof(IsEnabled));
            }

            RaisePropertyChanged(nameof(UseTabsLayout));
            RaisePropertyChanged(nameof(UseListLayout));

            return Task.CompletedTask;
        }
    }
}
