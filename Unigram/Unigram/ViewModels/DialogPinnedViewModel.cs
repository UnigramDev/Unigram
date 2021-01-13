using Unigram.Services;
using Unigram.Services.Factories;

namespace Unigram.ViewModels
{
    public class DialogPinnedViewModel : DialogViewModel
    {
        public DialogPinnedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IGroupCallService groupCallService, INetworkService networkService, IMessageFactory messageFactory)
            : base(protoService, cacheService, settingsService, aggregator, locationService, pushService, playbackService, voipService, groupCallService, networkService, messageFactory)
        {
        }

        public override DialogType Type => DialogType.Pinned;
    }
}
