using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersViewModel : SettingsStickersViewModelBase
    {
        public SettingsStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator, stickersService, StickerType.Image)
        {
        }
    }
}
