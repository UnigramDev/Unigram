using Telegram.Services;
using Telegram.ViewModels.Supergroups;

namespace Telegram.ViewModels.Profile
{
    public class ProfileMembersTabViewModel : SupergroupMembersViewModel
    {
        public ProfileMembersTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }
    }
}
