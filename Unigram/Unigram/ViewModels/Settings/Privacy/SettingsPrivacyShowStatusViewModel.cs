using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowStatusViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowStatusViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowStatus())
        {
        }
    }
}
