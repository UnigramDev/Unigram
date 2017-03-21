using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersViewModelBase : UnigramViewModelBase
    {
        private readonly IStickersService _stickersService;
        private readonly int _type;

        public SettingsStickersViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService, int type)
            : base(protoService, cacheService, aggregator)
        {
            _type = type;
            _stickersService = stickersService;
            _stickersService.StickersDidLoaded += OnStickersDidLoaded;

            Items = new ObservableCollection<TLMessagesStickerSet>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    _stickersService.CheckStickers(_type);
                    ProcessStickerSets(_type);
                });
            }

            return Task.CompletedTask;
        }

        private void OnStickersDidLoaded(object sender, StickersDidLoadedEventArgs e)
        {
            if (e.Type == _type)
            {
                ProcessStickerSets(_type);
            }
        }

        private void ProcessStickerSets(int type)
        {
            var stickers = _stickersService.GetStickerSets(type);
            Execute.BeginOnUIThread(() =>
            {
                Items.AddRange(stickers, true);
            });
        }

        public ObservableCollection<TLMessagesStickerSet> Items { get; private set; }
    }
}
