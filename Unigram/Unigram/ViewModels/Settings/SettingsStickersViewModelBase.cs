using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public abstract class SettingsStickersViewModelBase : UnigramViewModelBase, IHandle<UpdateInstalledStickerSets>, IHandle<UpdateTrendingStickerSets>
    {
        private readonly bool _masks;

        private bool _needReorder;
        private IList<long> _newOrder;

        public SettingsStickersViewModelBase(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, bool masks)
            : base(protoService, cacheService, aggregator)
        {
            _masks = masks;

            Items = new MvxObservableCollection<StickerSetInfo>();
            ReorderCommand = new RelayCommand<StickerSetInfo>(ReorderExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetInstalledStickerSets(_masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                }
            });

            ProtoService.Send(new GetArchivedStickerSets(_masks, 0, 1), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => ArchivedStickersCount = stickerSets.TotalCount);
                }
            });

            if (_masks)
            {
                return Task.CompletedTask;
            }

            ProtoService.Send(new GetTrendingStickerSets(), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => FeaturedStickersCount = stickerSets.TotalCount);
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            if (_needReorder && _newOrder.Count > 0)
            {
                _needReorder = false;
                ProtoService.Send(new ReorderInstalledStickerSets(_masks, _newOrder));

                //_stickersService.CalculateNewHash(_type);

                //var stickers = _stickersService.GetStickerSets(_type);
                //var order = new TLVector<long>(stickers.Select(x => x.Set.Id));

                //LegacyService.ReorderStickerSetsAsync(_type == StickerType.Mask, order, null);
                //Aggregator.Publish(new StickersDidLoadedEventArgs(_type));
            }

            return Task.CompletedTask;
        }

        public void Handle(UpdateInstalledStickerSets e)
        {
            if (e.IsMasks != _masks)
            {
                return;
            }

            ProtoService.Send(new GetInstalledStickerSets(_masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                }
            });
        }

        public void Handle(UpdateTrendingStickerSets e)
        {
            if (_masks)
            {
                return;
            }

            BeginOnUIThread(() => FeaturedStickersCount = e.StickerSets.TotalCount);
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

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }

        public RelayCommand<StickerSetInfo> ReorderCommand { get; }
        private void ReorderExecute(StickerSetInfo set)
        {
            _needReorder = true;
            _newOrder = Items.Select(x => x.Id).ToList();
        }
    }
}
