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
        public SettingsMasksViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator, true)
        {
        }
    }
}
