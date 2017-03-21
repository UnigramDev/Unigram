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
            _stickersService.FeaturedStickersDidLoaded += OnFeaturedStickersDidLoaded;
            _stickersService.ArchivedStickersCountDidLoaded += OnArchivedStickersCountDidLoaded;

            Items = new ObservableCollection<TLMessagesStickerSet>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    _stickersService.CheckStickers(_type);
                    _stickersService.CheckArchivedStickersCount(_type);

                    if (_type == StickersService.TYPE_IMAGE)
                    {
                        _stickersService.CheckFeaturedStickers();
                    }
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

        private void OnFeaturedStickersDidLoaded(object sender, FeaturedStickersDidLoadedEventArgs e)
        {
            Execute.BeginOnUIThread(() =>
            {
                FeaturedStickersCount = _stickersService.GetUnreadStickerSets().Count;
            });
        }

        private void OnArchivedStickersCountDidLoaded(object sender, ArchivedStickersCountDidLoadedEventArgs e)
        {
            Execute.BeginOnUIThread(() =>
            {
                ArchivedStickersCount = _stickersService.GetArchivedStickersCount(_type);
            });
        }

        private void ProcessStickerSets(int type)
        {
            var stickers = _stickersService.GetStickerSets(type);
            Execute.BeginOnUIThread(() =>
            {
                Items.AddRange(stickers, true);
            });
        }

        private int _featuredStickersCount;
        public int FeaturedStickersCount
        {
            get
            {
                return _featuredStickersCount;
            }
            set
            {
                Set(ref _featuredStickersCount, value);
            }
        }

        private int _archivedStickersCount;
        public int ArchivedStickersCount
        {
            get
            {
                return _archivedStickersCount;
            }
            set
            {
                Set(ref _archivedStickersCount, value);
            }
        }

        public ObservableCollection<TLMessagesStickerSet> Items { get; private set; }
    }
}
