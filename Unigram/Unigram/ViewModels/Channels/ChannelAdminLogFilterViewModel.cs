using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminLogFilterViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelAdminLogFilterViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new TLChannelParticipantsAdmins(), null)
        {
            GroupedItems = new ObservableCollection<ChannelAdminLogFilterViewModel> { this };
        }

        public ObservableCollection<ChannelAdminLogFilterViewModel> GroupedItems { get; private set; }
    }
}
