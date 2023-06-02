//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;

namespace Telegram.ViewModels.Channels
{
    public class ChannelCreateStep2ViewModel : SupergroupEditViewModelBase
    {
        public ChannelCreateStep2ViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public override async void Continue()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var item = ClientService.GetSupergroup(supergroup.SupergroupId);
                var cache = ClientService.GetSupergroupFull(supergroup.SupergroupId);

                if (item == null || cache == null)
                {
                    return;
                }

                var username = _isPublic ? Username?.Trim() ?? string.Empty : string.Empty;

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

                NavigationService.NavigateToChat(chat);
                NavigationService.GoBackAt(0, false);
                //NavigationService.Navigate(typeof(ChannelCreateStep3Page), chat.Id);
            }
        }
    }
}
