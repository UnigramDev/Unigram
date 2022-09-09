using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowPhotoViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowPhotoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowProfilePhoto())
        {
        }
    }
}
