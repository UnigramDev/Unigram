using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Unigram.ViewModels.Channels
{
    public class ChannelParticipantsViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelParticipantsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, null)
        {
        }
    }
}
