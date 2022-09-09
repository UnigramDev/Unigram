using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowChatInvitesViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowChatInvitesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowChatInvites())
        {
        }
    }
}
