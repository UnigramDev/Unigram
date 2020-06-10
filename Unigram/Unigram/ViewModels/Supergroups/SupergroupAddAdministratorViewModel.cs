using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAddAdministratorViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAddAdministratorViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
        }

        private SearchMembersAndUsersCollection _search;
        public new SearchMembersAndUsersCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }
    }
}
