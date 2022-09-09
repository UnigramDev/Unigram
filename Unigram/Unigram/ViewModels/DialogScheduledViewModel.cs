using Unigram.Services;
using Unigram.Services.Factories;

namespace Unigram.ViewModels
{
    public class DialogScheduledViewModel : DialogViewModel
    {
        public DialogScheduledViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IGroupCallService groupCallService, INetworkService networkService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory)
            : base(clientService, settingsService, aggregator, locationService, pushService, playbackService, voipService, groupCallService, networkService, storageService, translateService, messageFactory)
        {
        }

        public override DialogType Type => DialogType.ScheduledMessages;
    }
}
