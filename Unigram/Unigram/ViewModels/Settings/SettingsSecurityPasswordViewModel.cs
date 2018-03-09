using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityPasswordViewModel : UnigramViewModelBase
    {
        public SettingsSecurityPasswordViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private async void Update()
        {
            //var response = await LegacyService.GetPasswordAsync();
            //if (response.IsSucceeded)
            //{

            //}
        }
    }
}
