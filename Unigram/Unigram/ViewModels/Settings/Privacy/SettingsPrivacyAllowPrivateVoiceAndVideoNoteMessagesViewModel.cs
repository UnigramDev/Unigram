using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowPrivateVoiceAndVideoNoteMessages())
        {
        }
    }
}
