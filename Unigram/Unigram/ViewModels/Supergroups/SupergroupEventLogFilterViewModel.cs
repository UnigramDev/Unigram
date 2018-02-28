using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Services;
using Unigram.ViewModels.Channels;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEventLogFilterViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupEventLogFilterViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new SupergroupMembersFilterAdministrators(), null)
        {
            GroupedItems = new ObservableCollection<SupergroupEventLogFilterViewModel> { this };
        }

        public ObservableCollection<SupergroupEventLogFilterViewModel> GroupedItems { get; private set; }
    }
}
