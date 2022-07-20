using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new UserPrivacySettingAllowPrivateVoiceAndVideoNoteMessages())
        {
        }
    }
}
