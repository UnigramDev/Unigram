using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels.Channels;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEventLogFilterViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupEventLogFilterViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterAdministrators(), null)
        {
            GroupedItems = new ObservableCollection<SupergroupEventLogFilterViewModel> { this };
        }

        public ObservableCollection<SupergroupEventLogFilterViewModel> GroupedItems { get; private set; }
    }
}
