using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersArchivedViewModel : SettingsStickersArchivedViewModelBase
    {
        public SettingsStickersArchivedViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService) 
            : base(protoService, cacheService, aggregator, stickersService, StickerType.Image)
        {
        }
    }
}
