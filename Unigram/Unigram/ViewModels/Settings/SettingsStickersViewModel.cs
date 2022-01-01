using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public enum StickersType
    {
        Installed,
        Archived,
        Trending,
        Masks,
        MasksArchived
    }

    public class SettingsStickersViewModel : TLViewModelBase, IHandle<UpdateInstalledStickerSets>, IHandle<UpdateTrendingStickerSets>, IHandle<UpdateRecentStickers>
    {
        private StickersType _type;

        private bool _needReorder;
        private IList<long> _newOrder;

        public SettingsStickersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<StickerSetInfo>();
            ReorderCommand = new RelayCommand<StickerSetInfo>(ReorderExecute);

            SuggestCommand = new RelayCommand(SuggestExecute);

            StickerSetOpenCommand = new RelayCommand<StickerSetInfo>(StickerSetOpenExecute);
            StickerSetHideCommand = new RelayCommand<StickerSetInfo>(StickerSetHideExecute);
            StickerSetRemoveCommand = new RelayCommand<StickerSetInfo>(StickerSetRemoveExecute);
            //StickerSetShareCommand = new RelayCommand<StickerSetInfo>(StickerSetShareExecute);
            //StickerSetCopyCommand = new RelayCommand<StickerSetInfo>(StickerSetCopyExecute);
        }

        public StickersType Type => _type;

        public string Title => _type switch
        {
            StickersType.Installed => Strings.Resources.StickersName,
            StickersType.Archived => Strings.Resources.ArchivedStickers,
            StickersType.Trending => Strings.Resources.FeaturedStickers,
            StickersType.Masks => Strings.Resources.Masks,
            StickersType.MasksArchived => Strings.Resources.ArchivedMasks,
            _ => Strings.Resources.StickersName
        };

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is int flags)
            {
                _type = (StickersType)flags;
            }

            if (_type is StickersType.Installed or StickersType.Masks)
            {
                Items = new ItemsCollection(ProtoService, _type == StickersType.Masks);

                ProtoService.Send(new GetArchivedStickerSets(_type == StickersType.Masks, 0, 1), result =>
                {
                    if (result is StickerSets stickerSets)
                    {
                        BeginOnUIThread(() => ArchivedStickersCount = stickerSets.TotalCount);
                    }
                });

                ProtoService.Send(new GetTrendingStickerSets(), result =>
                {
                    if (result is StickerSets stickerSets)
                    {
                        BeginOnUIThread(() => FeaturedStickersCount = stickerSets.TotalCount);
                    }
                });
            }
            else if (_type is StickersType.Archived or StickersType.MasksArchived)
            {
                Items = new ArchivedCollection(ProtoService, _type == StickersType.MasksArchived);
            }
            else if (_type == StickersType.Trending)
            {
                Items = new TrendingCollection(ProtoService);
            }

            Aggregator.Subscribe(this);
            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(NavigationState pageState, bool suspending)
        {
            if (_type is StickersType.Installed or StickersType.Masks)
            {
                if (_needReorder && _newOrder.Count > 0)
                {
                    _needReorder = false;
                    ProtoService.Send(new ReorderInstalledStickerSets(_type == StickersType.Masks, _newOrder));

                    //_stickersService.CalculateNewHash(_type);

                    //var stickers = _stickersService.GetStickerSets(_type);
                    //var order = new TLVector<long>(stickers.Select(x => x.Set.Id));

                    //LegacyService.ReorderStickerSetsAsync(_type == StickerType.Mask, order, null);
                    //Aggregator.Publish(new StickersDidLoadedEventArgs(_type));
                }
            }

            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(UpdateInstalledStickerSets update)
        {
            if (_type is not StickersType.Installed and not StickersType.Masks)
            {
                return;
            }

            if (update.IsMasks != (_type == StickersType.Masks))
            {
                return;
            }

            ProtoService.Send(new GetInstalledStickerSets(_type == StickersType.Masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ProtoService.Send(new GetRecentStickers(_type == StickersType.Masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(new[] { new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, new ClosedVectorPath[0], false, false, false, false, _type == StickersType.Masks, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
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
            if (_type != StickersType.Installed)
            {
                return;
            }

            BeginOnUIThread(() => FeaturedStickersCount = update.StickerSets.TotalCount);
        }

        public void Handle(UpdateRecentStickers update)
        {
            if (_type is not StickersType.Installed and not StickersType.Masks)
            {
                return;
            }

            if (update.IsAttached != (_type == StickersType.Masks))
            {
                return;
            }

            ProtoService.Send(new GetInstalledStickerSets(_type == StickersType.Masks), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ProtoService.Send(new GetRecentStickers(_type == StickersType.Masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceWith(new[] { new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, new ClosedVectorPath[0], false, false, false, false, _type == StickersType.Masks, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
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
            get => _featuredStickersCount;
            set => Set(ref _featuredStickersCount, value);
        }

        private int _archivedStickersCount;
        public int ArchivedStickersCount
        {
            get => _archivedStickersCount;
            set => Set(ref _archivedStickersCount, value);
        }

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }

        public RelayCommand<StickerSetInfo> ReorderCommand { get; }
        private void ReorderExecute(StickerSetInfo set)
        {
            _needReorder = true;
            _newOrder = Items.Where(x => x.Id != 0).Select(x => x.Id).ToList();
        }

        public StickersSuggestionMode SuggestStickers
        {
            get => Settings.Stickers.SuggestionMode;
            set
            {
                Settings.Stickers.SuggestionMode = value;
                RaisePropertyChanged();
            }
        }

        public bool IsLoopingEnabled
        {
            get => Settings.Stickers.IsLoopingEnabled;
            set
            {
                Settings.Stickers.IsLoopingEnabled = value;
                RaisePropertyChanged();
            }
        }

        public RelayCommand SuggestCommand { get; }
        private async void SuggestExecute()
        {
            var items = new[]
            {
                new SelectRadioItem(StickersSuggestionMode.All, Strings.Resources.SuggestStickersAll, SuggestStickers == StickersSuggestionMode.All),
                new SelectRadioItem(StickersSuggestionMode.Installed, Strings.Resources.SuggestStickersInstalled, SuggestStickers == StickersSuggestionMode.Installed),
                new SelectRadioItem(StickersSuggestionMode.None, Strings.Resources.SuggestStickersNone, SuggestStickers == StickersSuggestionMode.None),
            };

            var dialog = new ChooseRadioPopup(items);
            dialog.Title = Strings.Resources.SuggestStickers;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is StickersSuggestionMode index)
            {
                SuggestStickers = index;
            }
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

                ProtoService.Send(new ClearRecentStickers(_type == StickersType.Masks));
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
            ProtoService.Send(new GetArchivedStickerSets(_type == StickersType.Masks, 0, 1), result =>
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

        public class ItemsCollection : MvxObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly bool _masks;

            private bool _hasMoreItems = true;

            public ItemsCollection(IProtoService protoService, bool masks)
            {
                _protoService = protoService;
                _masks = masks;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var recentResponse = await _protoService.SendAsync(new GetRecentStickers(_masks));
                    if (recentResponse is Stickers stickers)
                    {
                        Add(new StickerSetInfo(0, Strings.Resources.RecentStickers, "tg/recentlyUsed", null, new ClosedVectorPath[0], false, false, false, false, _masks, false, stickers.StickersValue.Count, stickers.StickersValue));
                    }

                    var response = await _protoService.SendAsync(new GetInstalledStickerSets(_masks));
                    if (response is StickerSets stickerSets)
                    {
                        foreach (var set in stickerSets.Sets)
                        {
                            Add(set);
                        }

                        _hasMoreItems = false;
                        return new LoadMoreItemsResult { Count = (uint)stickerSets.Sets.Count };
                    }

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }

        public class ArchivedCollection : MvxObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly bool _masks;

            private bool _hasMoreItems = true;

            public ArchivedCollection(IProtoService protoService, bool masks)
            {
                _protoService = protoService;
                _masks = masks;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var offset = 0L;

                    var last = this.LastOrDefault();
                    if (last != null)
                    {
                        offset = last.Id;
                    }

                    var response = await _protoService.SendAsync(new GetArchivedStickerSets(_masks, offset, 20));
                    if (response is StickerSets stickerSets)
                    {
                        foreach (var set in stickerSets.Sets)
                        {
                            Add(set);
                        }

                        _hasMoreItems = stickerSets.Sets.Count > 0;
                        return new LoadMoreItemsResult { Count = (uint)stickerSets.Sets.Count };
                    }

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }

        public class TrendingCollection : MvxObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private bool _hasMoreItems = true;

            public TrendingCollection(IProtoService protoService)
            {
                _protoService = protoService;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _protoService.SendAsync(new GetTrendingStickerSets(Count, 20));
                    if (response is StickerSets stickerSets)
                    {
                        foreach (var set in stickerSets.Sets)
                        {
                            Add(set);
                        }

                        _hasMoreItems = stickerSets.Sets.Count > 0;
                        return new LoadMoreItemsResult { Count = (uint)stickerSets.Sets.Count };
                    }

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }
}
