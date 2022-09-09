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
