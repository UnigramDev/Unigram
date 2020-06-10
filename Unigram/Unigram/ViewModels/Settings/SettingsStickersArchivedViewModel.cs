using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersArchivedViewModel : SettingsStickersArchivedViewModelBase
    {
        public SettingsStickersArchivedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, false)
        {
        }
    }
}
