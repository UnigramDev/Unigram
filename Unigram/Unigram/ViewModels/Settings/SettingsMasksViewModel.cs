using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsMasksViewModel : SettingsStickersViewModelBase
    {
        public SettingsMasksViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, true)
        {
        }
    }
}
