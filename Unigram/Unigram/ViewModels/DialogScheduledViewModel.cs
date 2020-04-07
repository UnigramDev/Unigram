using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;
using Unigram.Services.Factories;

namespace Unigram.ViewModels
{
    public class DialogScheduledViewModel : DialogViewModel
    {
        public DialogScheduledViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoIPService voipService, INetworkService networkService, IMessageFactory messageFactory)
            : base(protoService, cacheService, settingsService, aggregator, locationService, pushService, playbackService, voipService, networkService, messageFactory)
        {
        }

        public override bool IsSchedule => true;
    }
}
