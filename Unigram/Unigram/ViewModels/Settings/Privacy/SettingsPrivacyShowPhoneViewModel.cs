using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowPhoneViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowPhoneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowPhoneNumber())
        {
        }
    }
}
