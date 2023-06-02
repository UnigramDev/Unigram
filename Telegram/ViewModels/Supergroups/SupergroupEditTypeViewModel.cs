//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupEditTypeViewModel : SupergroupEditViewModelBase
    {
        public SupergroupEditTypeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private bool _joinToSendMessages;
        public bool JoinToSendMessages
        {
            get => _joinToSendMessages;
            set => Set(ref _joinToSendMessages, value);
        }

        private bool _joinByRequest;
        public bool JoinByRequest
        {
            get => _joinByRequest;
            set => Set(ref _joinByRequest, value);
        }

        private bool _hasProtectedContent;
        public bool HasProtectedContent
        {
            get => _hasProtectedContent;
            set => Set(ref _hasProtectedContent, value);
        }

        private Usernames _usernames;
        public Usernames Usernames
        {
            get => _usernames;
            set => Set(ref _usernames, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            HasProtectedContent = Chat?.HasProtectedContent ?? false;

            if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                JoinToSendMessages = supergroup.JoinToSendMessages;
                JoinByRequest = supergroup.JoinByRequest;
            }
        }

        public override async void Continue()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.HasProtectedContent != HasProtectedContent)
            {
                await ClientService.SendAsync(new ToggleChatHasProtectedContent(Chat.Id, HasProtectedContent));
            }

            var username = _isPublic ? (Username?.Trim() ?? string.Empty) : string.Empty;

            // If we're editing a basic group and the user wants to set an username to it,
            // then we need to upgrade it to a supergroup first.
            if (chat.Type is ChatTypeBasicGroup && !string.IsNullOrEmpty(username))
            {
                var response = await ClientService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(chat.Id));
                if (response is Chat result && result.Type is ChatTypeSupergroup supergroup)
                {
                    chat = result;
                    await ClientService.SendAsync(new GetSupergroupFullInfo(supergroup.SupergroupId));
                }
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                var item = ClientService.GetSupergroup(chat);
                var cache = ClientService.GetSupergroupFull(chat);

                if (item == null || cache == null)
                {
                    return;
                }

                if (item.JoinToSendMessages != _joinToSendMessages)
                {
                    ClientService.Send(new ToggleSupergroupJoinToSendMessages(item.Id, _joinToSendMessages));
                }

                if (item.JoinByRequest != _joinByRequest)
                {
                    ClientService.Send(new ToggleSupergroupJoinByRequest(item.Id, _joinByRequest));
                }

                if (!string.Equals(username, item.EditableUsername()))
                {
                    var response = await ClientService.SendAsync(new SetSupergroupUsername(item.Id, username));
                    if (response is Error error)
                    {
                        if (error.MessageEquals(ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            HasTooMuchUsernames = true;
                            NavigationService.ShowLimitReached(new PremiumLimitTypeCreatedPublicChatCount());
                        }
                        // TODO:

                        return;
                    }
                }

                NavigationService.GoBack();
            }
        }
    }
}
