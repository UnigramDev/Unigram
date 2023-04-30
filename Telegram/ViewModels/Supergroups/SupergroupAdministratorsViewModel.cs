//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Supergroups;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupAdministratorsViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAdministratorsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterAdministrators(), query => new SupergroupMembersFilterAdministrators())
        {
        }

        private bool _isAggressiveAntiSpamEnabled;
        public bool IsAggressiveAntiSpamEnabled
        {
            get => _isAggressiveAntiSpamEnabled;
            set => SetIsAggressiveAntiSpamEnabled(value);
        }

        public void UpdateIsAggressiveAntiSpamEnabled(bool value)
        {
            Set(ref _isAggressiveAntiSpamEnabled, value, nameof(IsAggressiveAntiSpamEnabled));
        }

        private void SetIsAggressiveAntiSpamEnabled(bool value)
        {
            if (Chat.Type is ChatTypeSupergroup supergroupType && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroup))
            {
                if (supergroup.CanToggleAggressiveAntiSpam)
                {
                    Set(ref _isAggressiveAntiSpamEnabled, value, nameof(IsAggressiveAntiSpamEnabled));
                    ClientService.Send(new ToggleSupergroupHasAggressiveAntiSpamEnabled(supergroupType.SupergroupId, value));
                }
                else
                {
                    Set(ref _isAggressiveAntiSpamEnabled, false, nameof(IsAggressiveAntiSpamEnabled));
                }
            }
        }

        public void EventLog()
        {
            if (_chat is Chat chat)
            {
                NavigationService.Navigate(typeof(ChatEventLogPage), chat.Id);
            }
        }

        public void Add()
        {
            if (_chat is Chat chat)
            {
                NavigationService.Navigate(typeof(SupergroupAddAdministratorPage), chat.Id);
            }
        }
    }
}
