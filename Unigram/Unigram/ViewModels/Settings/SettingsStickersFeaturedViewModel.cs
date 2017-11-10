using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Views;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;
using Unigram.Core.Common;
using Telegram.Api.TL.Messages;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersFeaturedViewModel : UnigramViewModelBase, IHandle<FeaturedStickersDidLoadedEventArgs>
    {
        private readonly IStickersService _stickersService;

        public SettingsStickersFeaturedViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;

            Items = new MvxObservableCollection<TLMessagesStickerSet>();

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    var featured = _stickersService.CheckFeaturedStickers();
                    if (featured) ProcessStickerSets();
                });
            }

            return Task.CompletedTask;
        }

        public void Handle(FeaturedStickersDidLoadedEventArgs e)
        {
            ProcessStickerSets();
        }

        private void ProcessStickerSets()
        {
            var stickers = _stickersService.GetFeaturedStickerSets();
            BeginOnUIThread(() =>
            {
                Items.ReplaceWith(stickers);
            });
        }

        public MvxObservableCollection<TLMessagesStickerSet> Items { get; private set; }
    }
}
