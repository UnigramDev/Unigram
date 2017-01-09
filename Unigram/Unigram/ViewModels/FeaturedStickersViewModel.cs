using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;

namespace Unigram.ViewModels
{
    public class FeaturedStickersViewModel : UnigramViewModelBase
    {
        public FeaturedStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Load();
        }

        private async void Load()
        {
            //var result = await ProtoService.GetFeaturedStickersAsync(true, 0);
            //if (result.IsSucceeded)
            //{
            //    Debugger.Break();
            //}
        }
    }
}
