using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Common;

namespace Unigram.ViewModels.Settings
{
    public class SettingsWallPaperViewModel : UnigramViewModelBase
    {
        public SettingsWallPaperViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<TLWallPaperBase>();
            ProtoService.GetWallpapersAsync(result =>
            {
                Items.ReplaceWith(result);
            });
        }

        public MvxObservableCollection<TLWallPaperBase> Items { get; private set; }
    }
}
