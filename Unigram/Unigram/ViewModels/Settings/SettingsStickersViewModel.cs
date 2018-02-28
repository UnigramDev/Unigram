using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersViewModel : SettingsStickersViewModelBase
    {
        public SettingsStickersViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, false)
        {
        }
    }
}
