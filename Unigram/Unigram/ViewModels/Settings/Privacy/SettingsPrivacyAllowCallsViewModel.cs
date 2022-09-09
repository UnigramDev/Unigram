using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowCallsViewModel : SettingsPrivacyViewModelBase
    {
        private readonly SettingsPrivacyAllowP2PCallsViewModel _allowP2PCallsRules;

        public SettingsPrivacyAllowCallsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, SettingsPrivacyAllowP2PCallsViewModel p2pCall)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowCalls())
        {
            _allowP2PCallsRules = p2pCall;

            Children.Add(_allowP2PCallsRules);
        }

        public SettingsPrivacyAllowP2PCallsViewModel AllowP2PCallsRules => _allowP2PCallsRules;

    }
}
