using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Text.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels.Drawers
{
    public class StickerDrawerViewModel : TLViewModelBase, IHandle<UpdateRecentStickers>, IHandle<UpdateFavoriteStickers>, IHandle<UpdateInstalledStickerSets>
    {
        private StickerSetViewModel _recentSet;
        private StickerSetViewModel _favoriteSet;
        private SupergroupStickerSetViewModel _groupSet;

        private bool _updated;

        public StickerDrawerViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _favoriteSet = new StickerSetViewModel(ProtoService, Aggregator, new StickerSetInfo
            {
                Title = Strings.Resources.FavoriteStickers,
                Name = "tg/favedStickers"
            });

            _recentSet = new StickerSetViewModel(ProtoService, Aggregator, new StickerSetInfo
            {
                Title = Strings.Resources.RecentStickers,
                Name = "tg/recentlyUsed"
            });

            _groupSet = new SupergroupStickerSetViewModel(ProtoService, Aggregator, new StickerSetInfo
            {
                Title = Strings.Resources.GroupStickers,
                Name = "tg/groupStickers"
            });

            //_groupSet = new TLChannelStickerSet
            //{
            //    Set = new TLStickerSet
            //    {
            //        Title = Strings.Resources.GroupStickers,
            //        ShortName = "tg/groupStickers",
            //    },
            //};

            FeaturedStickers = new MvxObservableCollection<TLFeaturedStickerSet>();
            SavedStickers = new StickerSetCollection();

            //SyncStickers();
            //SyncGifs();

            InstallCommand = new RelayCommand<TLFeaturedStickerSet>(InstallExecute);

            Aggregator.Subscribe(this);
        }

        private static Dictionary<int, Dictionary<int, StickerDrawerViewModel>> _windowContext = new Dictionary<int, Dictionary<int, StickerDrawerViewModel>>();
        public static StickerDrawerViewModel GetForCurrentView(int sessionId)
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out Dictionary<int, StickerDrawerViewModel> reference))
            {
                if (reference.TryGetValue(sessionId, out StickerDrawerViewModel value))
                {
                    return value;
                }
            }
            else
            {
                _windowContext[id] = new Dictionary<int, StickerDrawerViewModel>();
            }

            var context = TLContainer.Current.Resolve<StickerDrawerViewModel>();
            _windowContext[id][sessionId] = context;

            return context;
        }

        public void Handle(UpdateFavoriteStickers update)
        {
            ProtoService.Send(new GetFavoriteStickers(), result =>
            {
                if (result is Stickers favorite)
                {
                    BeginOnUIThread(() => _favoriteSet.Update(favorite, true));
                }
            });
        }

        public void Handle(UpdateRecentStickers update)
        {
            if (update.IsAttached)
            {
                return;
            }

            ProtoService.Send(new GetRecentStickers(), result =>
            {
                if (result is Stickers recent)
                {
                    BeginOnUIThread(() =>
                    {
                        for (int i = 0; i < _favoriteSet.Stickers.Count; i++)
                        {
                            var favSticker = _favoriteSet.Stickers[i];
                            for (int j = 0; j < recent.StickersValue.Count; j++)
                            {
                                var recSticker = recent.StickersValue[j];
                                if (recSticker.StickerValue.Id == favSticker.StickerValue.Id)
                                {
                                    recent.StickersValue.Remove(recSticker);
                                    break;
                                }
                            }
                        }

                        for (int i = 20; i < recent.StickersValue.Count; i++)
                        {
                            recent.StickersValue.RemoveAt(20);
                            i--;
                        }

                        _recentSet.Update(recent, true);
                    });
                }
            });
        }

        public void Handle(UpdateInstalledStickerSets update)
        {
            if (update.IsMasks)
            {
                return;
            }

            _updated = false;
            SyncStickers(null);
        }

        private void ProcessRecentGifs()
        {
            //var recent = _stickersService.GetRecentGifs();
            //BeginOnUIThread(() =>
            //{
            //    SavedGifs.ReplaceWith(MosaicMedia.Calculate(recent));
            //});
        }

        private void ProcessRecentStickers()
        {
            //var items = _stickersService.GetRecentStickers(StickerType.Image);
            //BeginOnUIThread(() =>
            //{
            //    _recentSet.Documents = new TLVector<TLDocumentBase>(items);
            //    CheckDocuments();

            //    if (_recentSet.Documents.Count > 0)
            //    {
            //        SavedStickers.Add(_recentSet);
            //    }
            //    else
            //    {
            //        SavedStickers.Remove(_recentSet);
            //    }
            //});
        }

        private void ProcessFavedStickers()
        {
            //var items = _stickersService.GetRecentStickers(StickerType.Fave);
            //BeginOnUIThread(() =>
            //{
            //    _favedSet.Documents = new TLVector<TLDocumentBase>(items);
            //    CheckDocuments();

            //    if (_favedSet.Documents.Count > 0)
            //    {
            //        SavedStickers.Add(_favedSet);
            //    }
            //    else
            //    {
            //        SavedStickers.Remove(_favedSet);
            //    }
            //});
        }

        private void ProcessStickers()
        {
            //_stickers = true;

            //var stickers = _stickersService.GetStickerSets(StickerType.Image);
            //BeginOnUIThread(() =>
            //{
            //    SavedStickers.ReplaceWith(stickers);

            //    //if (_groupSet.Documents != null && _groupSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_groupSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_groupSet);
            //    //}

            //    //if (_recentSet.Documents != null && _recentSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_recentSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_recentSet);
            //    //}

            //    //if (_favedSet.Documents != null && _favedSet.Documents.Count > 0)
            //    //{
            //    //    SavedStickers.Add(_favedSet);
            //    //}
            //    //else
            //    //{
            //    //    SavedStickers.Remove(_favedSet);
            //    //}
            //});
        }

        private void ProcessFeaturedStickers()
        {
            //_featured = true;
            //var stickers = _stickersService.GetFeaturedStickerSets();
            //var unread = _stickersService.GetUnreadStickerSets();
            //BeginOnUIThread(() =>
            //{
            //    FeaturedUnreadCount = unread.Count;
            //    FeaturedStickers.ReplaceWith(stickers.Select(set => new TLFeaturedStickerSet
            //    {
            //        Set = set.Set,
            //        IsUnread = unread.Contains(set.Set.Id),
            //        Covers = new TLVector<TLDocumentBase>(set.Documents.Take(Math.Min(set.Documents.Count, 5)))
            //    }));
            //});
        }

        private void CheckDocuments()
        {
            //if (_recentSet.Documents == null || _favedSet.Documents == null)
            //{
            //    return;
            //}

            //for (int i = 0; i < _favedSet.Documents.Count; i++)
            //{
            //    var favSticker = _favedSet.Documents[i] as TLDocument;
            //    for (int j = 0; j < _recentSet.Documents.Count; j++)
            //    {
            //        var recSticker = _recentSet.Documents[j] as TLDocument;
            //        if (recSticker.DCId == favSticker.DCId && recSticker.Id == favSticker.Id)
            //        {
            //            _recentSet.Documents.Remove(recSticker);
            //            break;
            //        }
            //    }
            //}
        }

        public MvxObservableCollection<TLFeaturedStickerSet> FeaturedStickers { get; private set; }

        public StickerSetCollection SavedStickers { get; private set; }

        private SearchStickerSetsCollection _searchStickers;
        public SearchStickerSetsCollection SearchStickers
        {
            get
            {
                return _searchStickers;
            }
            set
            {
                Set(ref _searchStickers, value);
                RaisePropertyChanged(() => Stickers);
            }
        }

        public MvxObservableCollection<StickerSetViewModel> Stickers => SearchStickers ?? (MvxObservableCollection<StickerSetViewModel>)SavedStickers;

        public async void FindStickers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchStickers = null;
            }
            else
            {
                var items = SearchStickers = new SearchStickerSetsCollection(ProtoService, Aggregator, false, query, CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
                await items.LoadMoreItemsAsync(0);
            }
        }

        public async void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            SavedStickers.Remove(_groupSet);

            var refresh = true;

            var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
            if (appData.Values.TryGetValue("Stickers" + group.Id, out object stickersObj))
            {
                var stickersId = (long)stickersObj;
                if (stickersId == fullInfo.StickerSetId)
                {
                    refresh = false;
                }
            }

            if (fullInfo.StickerSetId != 0 && refresh)
            {
                if (fullInfo.StickerSetId == _groupSet.Id && chat.Id == _groupSet.ChatId)
                {
                    SavedStickers.Add(_groupSet);
                    return;
                }

                var response = await ProtoService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
                if (response is StickerSet stickerSet)
                {
                    BeginOnUIThread(() =>
                    {
                        _groupSet.Update(chat.Id, stickerSet);

                        if (_groupSet.Stickers != null && _groupSet.Stickers.Count > 0)
                        {
                            SavedStickers.Add(_groupSet);
                        }
                        else
                        {
                            SavedStickers.Remove(_groupSet);
                        }
                    });
                }
            }
        }

        //public void HideGroup(TLChannelFull channelFull)
        //{
        //    var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
        //    appData.Values["Stickers" + channelFull.Id] = channelFull.StickerSet?.Id ?? 0;

        //    SavedStickers.Remove(_groupSet);
        //}

        public void SyncStickers(Chat chat)
        {
            if (_updated)
            {
                return;
            }

            _updated = true;

            ProtoService.Send(new GetFavoriteStickers(), result1 =>
            {
                ProtoService.Send(new GetRecentStickers(), result2 =>
                {
                    ProtoService.Send(new GetInstalledStickerSets(false), result3 =>
                    {
                        if (result1 is Stickers favorite && result2 is Stickers recent && result3 is StickerSets sets)
                        {
                            for (int i = 0; i < favorite.StickersValue.Count; i++)
                            {
                                var favSticker = favorite.StickersValue[i];
                                for (int j = 0; j < recent.StickersValue.Count; j++)
                                {
                                    var recSticker = recent.StickersValue[j];
                                    if (recSticker.StickerValue.Id == favSticker.StickerValue.Id)
                                    {
                                        recent.StickersValue.Remove(recSticker);
                                        break;
                                    }
                                }
                            }

                            for (int i = 20; i < recent.StickersValue.Count; i++)
                            {
                                recent.StickersValue.RemoveAt(20);
                                i--;
                            }

                            _favoriteSet.Update(favorite);
                            _recentSet.Update(recent);

                            var stickers = new List<StickerSetViewModel>();
                            if (_favoriteSet.Stickers.Count > 0)
                            {
                                stickers.Add(_favoriteSet);
                            }
                            if (_recentSet.Stickers.Count > 0)
                            {
                                stickers.Add(_recentSet);
                            }
                            if (_groupSet.Stickers.Count > 0 && _groupSet.ChatId == chat?.Id)
                            {
                                stickers.Add(_groupSet);
                            }

                            if (sets.Sets.Count > 0)
                            {
                                ProtoService.Send(new GetStickerSet(sets.Sets[0].Id), result4 =>
                                {
                                    if (result4 is StickerSet set)
                                    {
                                        stickers.Add(new StickerSetViewModel(ProtoService, Aggregator, sets.Sets[0], set));
                                        BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Skip(1).Select(x => new StickerSetViewModel(ProtoService, Aggregator, x)))));
                                    }
                                    else
                                    {
                                        BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Select(x => new StickerSetViewModel(ProtoService, Aggregator, x)))));
                                    }
                                });
                            }
                            else
                            {
                                BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Select(x => new StickerSetViewModel(ProtoService, Aggregator, x)))));
                            }
                        }
                    });
                });
            });




            //ProtoService.Send(new GetSavedAnimations(), result =>
            //{

            //});

            //ProtoService.Send(new GetTrendingStickerSets(), result =>
            //{

            //});
        }

        public void SyncGifs()
        {
            //Execute.BeginOnThreadPool(() =>
            //{
            //    _stickersService.LoadRecents(StickerType.Image, true, true, false);

            //    ProcessRecentGifs();
            //});
        }

        private int _featuredUnreadCount;
        public int FeaturedUnreadCount
        {
            get
            {
                return _featuredUnreadCount;
            }
            set
            {
                Set(ref _featuredUnreadCount, value);
            }
        }

        public RelayCommand<TLFeaturedStickerSet> InstallCommand { get; }
        private async void InstallExecute(TLFeaturedStickerSet featured)
        {
            //if (_stickersService.IsStickerPackInstalled(featured.Set.Id) == false)
            //{
            //    var response = await LegacyService.InstallStickerSetAsync(new TLInputStickerSetID { Id = featured.Set.Id, AccessHash = featured.Set.AccessHash }, false);
            //    if (response.IsSucceeded)
            //    {
            //        _stickersService.LoadStickers(featured.Set.IsMasks ? StickerType.Mask : StickerType.Image, false, true);

            //        featured.Set.IsInstalled = true;
            //        featured.Set.IsArchived = false;
            //    }
            //}
            //else
            //{
            //    _stickersService.RemoveStickersSet(featured.Set, featured.Set.IsOfficial ? 1 : 0, true);

            //    featured.Set.IsInstalled = featured.Set.IsOfficial;
            //    featured.Set.IsArchived = featured.Set.IsOfficial;

            //    NavigationService.GoBack();
            //}
        }

        //protected override void BeginOnUIThread(Action action)
        //{
        //    // This is somehow needed because this viewmodel requires a Dispatcher
        //    // in some situations where base one might be null.
        //    Execute.BeginOnUIThread(action);
        //}
    }

    public class TLChannelStickerSet : System.Object
    {
        //public TLChannel With { get; set; }
        //public TLChannelFull Full { get; set; }
    }

    public class TLFeaturedStickerSet : System.Object
    {
        //public TLStickerSet Set { get; set; }

        //private TLVector<TLDocumentBase> _covers;
        //public TLVector<TLDocumentBase> Covers
        //{
        //    get
        //    {
        //        return _covers;
        //    }
        //    set
        //    {
        //        _covers = new TLVector<TLDocumentBase>();

        //        for (int i = 0; i < 5; i++)
        //        {
        //            if (i < value.Count)
        //            {
        //                _covers.Add(value[i]);
        //            }
        //            else
        //            {
        //                _covers.Add(null);
        //            }
        //        }
        //    }
        //}

        private bool _isUnread;
        public bool IsUnread
        {
            get
            {
                return _isUnread;
            }
            set
            {
                _isUnread = value;
            }
        }

        public string Unread
        {
            get
            {
                return _isUnread ? "\u2022" : string.Empty;
            }
        }
    }

    public class SupergroupStickerSetViewModel : StickerSetViewModel
    {
        public SupergroupStickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info)
            : base(protoService, aggregator, info)
        {
        }

        public SupergroupStickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info, StickerSet set)
            : base(protoService, aggregator, info, set)
        {
        }

        public void Update(long chatId, StickerSet set, bool reset = true)
        {
            _info.Id = set.Id;
            ChatId = chatId;

            if (reset)
            {
                Stickers = new MvxObservableCollection<StickerViewModel>(set.Stickers.Select(x => new StickerViewModel(_protoService, _aggregator, x)));
            }
            else
            {
                Stickers.ReplaceWith(set.Stickers.Select(x => new StickerViewModel(_protoService, _aggregator, x)));
            }
        }

        public long ChatId { get; private set; }
    }

    public class StickerSetViewModel
    {
        protected readonly IProtoService _protoService;
        protected readonly IEventAggregator _aggregator;

        protected readonly StickerSetInfo _info;
        protected StickerSet _set;

        public StickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _info = info;

            var placeholders = new List<StickerViewModel>();
            for (int i = 0; i < (info.IsInstalled ? info.Size : info.Covers?.Count ?? 0); i++)
            {
                placeholders.Add(new StickerViewModel(_protoService, _aggregator, info.Id));
            }

            Stickers = new MvxObservableCollection<StickerViewModel>(placeholders);
            Covers = info.Covers;
        }

        public StickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info, StickerSet set)
            : this(protoService, aggregator, info)
        {
            IsLoaded = true;
            Update(set);
        }

        public StickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info, IList<Sticker> stickers)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _info = info;

            IsLoaded = true;
            Stickers = new MvxObservableCollection<StickerViewModel>(stickers.Select(x => new StickerViewModel(protoService, aggregator, x)));
            Covers = info.Covers;
        }

        public void Update(StickerSet set, bool reset = false)
        {
            _set = set;

            for (int i = 0; i < set.Stickers.Count && i < Stickers.Count; i++)
            {
                Stickers[i].Update(set.Stickers[i]);
            }

            if (reset)
            {
                Stickers.Reset();
            }
        }

        public void Update(Stickers stickers, bool raise = false)
        {
            if (raise)
            {
                Stickers.ReplaceWith(stickers.StickersValue.Select(x => new StickerViewModel(_protoService, _aggregator, x)));
            }
            else
            {
                Stickers = new MvxObservableCollection<StickerViewModel>(stickers.StickersValue.Select(x => new StickerViewModel(_protoService, _aggregator, x)));
            }
        }

        public MvxObservableCollection<StickerViewModel> Stickers { get; protected set; }

        public bool IsLoaded { get; set; }

        //public IList<StickerEmojis> Emojis { get => _set?.Emojis; set => _set?.Emojis = value; }
        //public IList<Sticker> Stickers { get; set; }
        public bool IsViewed => _set?.IsViewed ?? _info.IsViewed;
        public bool IsAnimated => _set?.IsAnimated ?? _info.IsAnimated;
        public bool IsMasks => _set?.IsMasks ?? _info.IsMasks;
        public bool IsOfficial => _set?.IsOfficial ?? _info.IsOfficial;
        public bool IsArchived => _set?.IsArchived ?? _info.IsArchived;
        public bool IsInstalled => _set?.IsInstalled ?? _info.IsInstalled;
        public string Name => _set?.Name ?? _info.Name;
        public string Title => _set?.Title ?? _info.Title;
        public long Id => _set?.Id ?? _info.Id;

        public Thumbnail Thumbnail => _set?.Thumbnail ?? _info.Thumbnail;

        public IList<Sticker> Covers { get; private set; }
    }

    public class StickerViewModel
    {
        private Sticker _sticker;
        private long _setId;

        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        public StickerViewModel(IProtoService protoService, IEventAggregator aggregator, long setId)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _setId = setId;
        }

        public StickerViewModel(IProtoService protoService, IEventAggregator aggregator, Sticker sticker)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _sticker = sticker;
        }

        public void Update(Sticker sticker)
        {
            _sticker = sticker;
        }

        public IProtoService ProtoService => _protoService;
        public IEventAggregator Aggregator => _aggregator;

        public bool UpdateFile(File file)
        {
            if (_sticker == null)
            {
                return false;
            }

            return _sticker.UpdateFile(file);
        }

        public static implicit operator Sticker(StickerViewModel viewModel)
        {
            return viewModel._sticker;
        }

        public File StickerValue => _sticker?.StickerValue;
        public Thumbnail Thumbnail => _sticker?.Thumbnail;
        public MaskPosition MaskPosition => _sticker?.MaskPosition;
        public bool IsAnimated => _sticker?.IsAnimated ?? false;
        public bool IsMask => _sticker?.IsMask ?? false;
        public string Emoji => _sticker?.Emoji;
        public int Height => _sticker?.Height ?? 0;
        public int Width => _sticker?.Width ?? 0;
        public long SetId => _sticker?.SetId ?? _setId;
    }

    public class StickerSetCollection : MvxObservableCollection<StickerSetViewModel>
    {
        private readonly Dictionary<string, int> _indexer = new Dictionary<string, int>
        {
            { "tg/favedStickers", 0 },
            { "tg/recentlyUsed", 1 },
            { "tg/groupStickers", 2 }
        };

        private readonly Dictionary<int, string> _mapper = new Dictionary<int, string>
        {
            { 0, "tg/favedStickers" },
            { 1, "tg/recentlyUsed" },
            { 2, "tg/groupStickers" }
        };

        protected override void InsertItem(int index, StickerSetViewModel item)
        {
            if (_indexer.TryGetValue(item.Name, out int want))
            {
                index = 0;

                for (int i = 0; i < 3; i++)
                {
                    if (Count > i && _mapper[i] == this[i].Name && i < want)
                    {
                        index++;
                    }
                }

                var already = IndexOf(item);
                if (already != index)
                {
                    if (already > -1)
                    {
                        base.RemoveItem(already);
                    }

                    base.InsertItem(index, item);
                }
                else
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, index, index));
                }
            }
            else
            {
                base.InsertItem(index, item);
            }
        }
    }

    public class SearchStickerSetsCollection : MvxObservableCollection<StickerSetViewModel>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;
        private readonly bool _masks;
        private readonly string _query;
        private readonly string _inputLanguage;

        public SearchStickerSetsCollection(IProtoService protoService, IEventAggregator aggregator, bool masks, string query, string inputLanguage)
        {
            _protoService = protoService;
            _aggregator = aggregator;
            _masks = masks;
            _query = query;
            _inputLanguage = inputLanguage;
        }

        public string Query => _query;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                if (phase == 0)
                {
                    var response = await _protoService.SendAsync(new SearchInstalledStickerSets(_masks, _query, 100));
                    if (response is StickerSets sets)
                    {
                        foreach (var item in sets.Sets.Select(x => new StickerSetViewModel(_protoService, _aggregator, x)))
                        {
                            Add(item);
                        }

                        //AddRange(sets.Sets.Select(x => new StickerSetViewModel(_protoService, _aggregator, x)));
                    }
                }
                else if (phase == 1 && _query.Length > 1)
                {
                    if (Emoji.ContainsSingleEmoji(_query))
                    {
                        var response = await _protoService.SendAsync(new GetStickers(_query, 100));
                        if (response is Stickers stickers && stickers.StickersValue.Count > 0)
                        {
                            Add(new StickerSetViewModel(_protoService, _aggregator,
                                new StickerSetInfo(0, _query, "emoji", null, false, false, false, false, false, false, stickers.StickersValue.Count, stickers.StickersValue),
                                new StickerSet(0, _query, "emoji", null, false, false, false, false, false, false, stickers.StickersValue, new Emojis[0])));
                        }
                    }
                    else
                    {
                        var emojis = await _protoService.SendAsync(new SearchEmojis(_query, false, new[] { _inputLanguage })) as Emojis;
                        if (emojis != null)
                        {
                            for (int i = 0; i < Math.Min(10, emojis.EmojisValue.Count); i++)
                            {
                                var response = await _protoService.SendAsync(new GetStickers(emojis.EmojisValue[i], 100));
                                if (response is Stickers stickers && stickers.StickersValue.Count > 0)
                                {
                                    Add(new StickerSetViewModel(_protoService, _aggregator,
                                        new StickerSetInfo(0, emojis.EmojisValue[i], "emoji", null, false, false, false, false, false, false, stickers.StickersValue.Count, stickers.StickersValue),
                                        new StickerSet(0, emojis.EmojisValue[i], "emoji", null, false, false, false, false, false, false, stickers.StickersValue, new Emojis[0])));
                                }
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _protoService.SendAsync(new SearchStickerSets(_query));
                    if (response is StickerSets sets)
                    {
                        foreach (var item in sets.Sets.Select(x => new StickerSetViewModel(_protoService, _aggregator, x, x.Covers)))
                        {
                            Add(item);
                        }

                        //AddRange(sets.Sets.Select(x => new StickerSetViewModel(_protoService, _aggregator, x)));
                    }
                }

                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => false;
    }
}
