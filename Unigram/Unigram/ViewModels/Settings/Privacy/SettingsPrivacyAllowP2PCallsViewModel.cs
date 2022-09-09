using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowP2PCallsViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowP2PCallsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowPeerToPeerCalls())
        {
        }
    }
}
