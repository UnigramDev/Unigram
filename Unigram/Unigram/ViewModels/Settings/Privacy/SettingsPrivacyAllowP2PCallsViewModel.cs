using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAllowP2PCallsViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowP2PCallsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new UserPrivacySettingAllowPeerToPeerCalls())
        {
        }
    }
}
