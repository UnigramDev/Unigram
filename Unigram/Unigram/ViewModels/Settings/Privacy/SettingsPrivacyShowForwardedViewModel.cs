using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowForwardedViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowForwardedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowLinkInForwardedMessages())
        {
        }
    }
}
