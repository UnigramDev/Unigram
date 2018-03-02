using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;

namespace Unigram.ViewModels.Channels
{
    public class ChannelCreateStep3ViewModel : UsersSelectionViewModel
    {
        public ChannelCreateStep3ViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }
    }
}
