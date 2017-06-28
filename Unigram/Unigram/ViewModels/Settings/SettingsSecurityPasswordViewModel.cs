using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityPasswordViewModel : UnigramViewModelBase
    {
        public SettingsSecurityPasswordViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private async void Update()
        {
            var response = await ProtoService.GetPasswordAsync();
            if (response.IsSucceeded)
            {

            }
        }
    }
}
