//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAddAdministratorViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAddAdministratorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        private SearchMembersAndUsersCollection _search;
        public SearchMembersAndUsersCollection Search
        {
            get => _search;
            set => Set(ref _search, value);
        }
    }
}
