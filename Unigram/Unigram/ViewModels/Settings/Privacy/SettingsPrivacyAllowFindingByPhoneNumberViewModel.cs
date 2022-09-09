using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowFindingByPhoneNumberViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowFindingByPhoneNumberViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowFindingByPhoneNumber())
        {
        }
    }
}
