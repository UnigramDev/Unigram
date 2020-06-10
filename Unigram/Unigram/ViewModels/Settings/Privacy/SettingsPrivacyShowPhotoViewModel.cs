using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowPhotoViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowPhotoViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new UserPrivacySettingShowProfilePhoto())
        {
        }
    }
}
