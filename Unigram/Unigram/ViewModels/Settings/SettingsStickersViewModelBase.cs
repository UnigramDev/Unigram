using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public abstract class SettingsStickersViewModelBase : TLViewModelBase, IHandle<UpdateInstalledStickerSets>, IHandle<UpdateTrendingStickerSets>, IHandle<UpdateRecentStickers>
    {
        private readonly bool _masks;

        private bool _needReorder;
        private IList<long> _newOrder;

        public SettingsStickersViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, bool masks)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _masks = masks;

            Items = new MvxObservableCollection<StickerSetInfo>();
            ReorderCommand = new RelayCommand<StickerSetInfo>(ReorderExecute);

            StickerSetOpenCommand = new RelayCommand<StickerSetInfo>(StickerSetOpenExecute);
            StickerSetHideCommand = new RelayCommand<StickerSetInfo>(StickerSetHideExecute);
            StickerSetRemoveCommand = new RelayCommand<StickerSetInfo>(StickerSetRemoveExecute);
            //StickerSetShareCommand = new RelayCommand<StickerSetInfo>(StickerSetShareExecute);
            //StickerSetCopyCommand = new RelayCommand<StickerSetInfo>(StickerSetCopyExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);

            ProtoService.Send(new GetInstalledStickerSets(_masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ProtoService.Send(new GetRecentStickers(_masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(new[] { new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, false, false, false, false, _masks, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
                        }
                        else
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                        }
                    });
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
            Aggregator.Unsubscribe(this);

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

        public void Handle(UpdateInstalledStickerSets update)
        {
            if (update.IsMasks != _masks)
            {
                return;
            }

            ProtoService.Send(new GetInstalledStickerSets(_masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ProtoService.Send(new GetRecentStickers(_masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(new[] { new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, false, false, false, false, _masks, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
                        }
                        else
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                        }
                    });
                }
            });
        }

        public void Handle(UpdateTrendingStickerSets update)
        {
            if (_masks)
            {
                return;
            }

            BeginOnUIThread(() => FeaturedStickersCount = update.StickerSets.TotalCount);
        }

        public void Handle(UpdateRecentStickers update)
        {
            if (update.IsAttached != _masks)
            {
                return;
            }

            ProtoService.Send(new GetInstalledStickerSets(_masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ProtoService.Send(new GetRecentStickers(_masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(new[] { new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, false, false, false, false, _masks, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
                        }
                        else
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(stickerSets.Sets));
                        }
                    });
                }
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

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }

        public RelayCommand<StickerSetInfo> ReorderCommand { get; }
        private void ReorderExecute(StickerSetInfo set)
        {
            _needReorder = true;
            _newOrder = Items.Where(x => x.Id != 0).Select(x => x.Id).ToList();
        }

        #region Context menu

        public RelayCommand<StickerSetInfo> StickerSetOpenCommand { get; }
        private async void StickerSetOpenExecute(StickerSetInfo stickerSet)
        {
            if (stickerSet.Name.Equals("tg/recentlyUsed"))
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.ClearRecentEmoji, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ProtoService.Send(new ClearRecentStickers(_masks));
            }
            else
            {
                await StickerSetPopup.GetForCurrentView().ShowAsync(stickerSet.Id);
            }
        }

        public RelayCommand<StickerSetInfo> StickerSetHideCommand { get; }
        private async void StickerSetHideExecute(StickerSetInfo stickerSet)
        {
            await ProtoService.SendAsync(new ChangeStickerSet(stickerSet.Id, false, true));
            ProtoService.Send(new GetArchivedStickerSets(_masks, 0, 1), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => ArchivedStickersCount = stickerSets.TotalCount);
                }
            });
        }

        public RelayCommand<StickerSetInfo> StickerSetRemoveCommand { get; }
        private void StickerSetRemoveExecute(StickerSetInfo stickerSet)
        {
            ProtoService.Send(new ChangeStickerSet(stickerSet.Id, false, false));
        }

        #endregion
    }
}
