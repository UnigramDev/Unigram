using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsMasksArchivedViewModel : SettingsStickersArchivedViewModelBase
    {
        public SettingsMasksArchivedViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, true)
        {
        }
    }
}
