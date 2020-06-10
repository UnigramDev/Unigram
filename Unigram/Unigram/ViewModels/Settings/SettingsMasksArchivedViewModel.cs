using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsMasksArchivedViewModel : SettingsStickersArchivedViewModelBase
    {
        public SettingsMasksArchivedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, true)
        {
        }
    }
}
