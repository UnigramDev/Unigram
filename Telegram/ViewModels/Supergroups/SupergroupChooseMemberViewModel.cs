//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupChooseMemberViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupChooseMemberViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        public SupergroupChooseMemberMode Mode { get; private set; }

        private SearchMembersAndUsersCollection _search;
        public SearchMembersAndUsersCollection Search
        {
            get => _search;
            set => Set(ref _search, value);
        }

        public void UpdateSearch(string query)
        {
            Search = new SearchMembersAndUsersCollection(ClientService, Chat.Id, query, Mode == SupergroupChooseMemberMode.Promote);
        }

        public override Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is SupergroupChooseMemberArgs args)
            {
                Mode = args.Mode;
                return base.NavigatedToAsync(args.ChatId, mode, state);
            }

            return Task.CompletedTask;
        }
    }
}
