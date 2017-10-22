using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public abstract class SettingsStickersViewModelBase : UnigramViewModelBase, IHandle<StickersDidLoadedEventArgs>, IHandle<FeaturedStickersDidLoadedEventArgs>, IHandle<ArchivedStickersCountDidLoadedEventArgs>
    {
        private readonly IStickersService _stickersService;
        private readonly StickerType _type;

        private bool _needReorder;

        public SettingsStickersViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService, StickerType type)
            : base(protoService, cacheService, aggregator)
        {
            _type = type;
            _stickersService = stickersService;

            Items = new MvxObservableCollection<TLMessagesStickerSet>();

            ReorderCommand = new RelayCommand<TLMessagesStickerSet>(ReorderExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New || mode == NavigationMode.Forward)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    var stickers = _stickersService.CheckStickers(_type);
                    _stickersService.CheckArchivedStickersCount(_type);

                    if (_type == StickerType.Image)
                    {
                        var featured = _stickersService.CheckFeaturedStickers();
                        if (featured) Handle(null as FeaturedStickersDidLoadedEventArgs);
                    }

                    if (stickers) ProcessStickerSets(_type);
                    Handle(null as ArchivedStickersCountDidLoadedEventArgs);
                });
            }

            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            if (_needReorder)
            {
                _needReorder = false;
                _stickersService.CalculateNewHash(_type);

                var stickers = _stickersService.GetStickerSets(_type);
                var order = new TLVector<long>(stickers.Select(x => x.Set.Id));

                ProtoService.ReorderStickerSetsAsync(_type == StickerType.Mask, order, null);
                Aggregator.Publish(new StickersDidLoadedEventArgs(_type));
            }

            return Task.CompletedTask;
        }

        public void Handle(StickersDidLoadedEventArgs e)
        {
            if (e.Type == _type)
            {
                ProcessStickerSets(_type);
            }
        }

        public void Handle(FeaturedStickersDidLoadedEventArgs e)
        {
            BeginOnUIThread(() =>
            {
                FeaturedStickersCount = _stickersService.GetUnreadStickerSets().Count;
            });
        }

        public void Handle(ArchivedStickersCountDidLoadedEventArgs e)
        {
            BeginOnUIThread(() =>
            {
                ArchivedStickersCount = _stickersService.GetArchivedStickersCount(_type);
            });
        }

        private void ProcessStickerSets(StickerType type)
        {
            var stickers = _stickersService.GetStickerSets(type);
            BeginOnUIThread(() =>
            {
                Items.ReplaceWith(stickers);
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

        public MvxObservableCollection<TLMessagesStickerSet> Items { get; private set; }

        public RelayCommand<TLMessagesStickerSet> ReorderCommand { get; }
        private void ReorderExecute(TLMessagesStickerSet set)
        {
            var stickers = _stickersService.GetStickerSets(_type);
            var index = Items.IndexOf(set);
            var old = stickers.IndexOf(set);
            if (old != index)
            {
                stickers.Remove(set);
                stickers.Insert(index, set);

                _needReorder = true;
            }
        }
    }
}
