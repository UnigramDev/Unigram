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
        private readonly DisposableMutex _supergroupLock = new();

        private readonly StickerSetViewModel _recentSet;
        private readonly StickerSetViewModel _favoriteSet;
        private readonly SupergroupStickerSetViewModel _groupSet;

        private long _groupSetId;
        private long _groupSetChatId;

        private bool _updated;
        private bool _updating;
        private long _updatedHash;

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

            SavedStickers = new StickerSetCollection();

            Aggregator.Subscribe(this);
        }

        private static readonly Dictionary<int, Dictionary<int, StickerDrawerViewModel>> _windowContext = new Dictionary<int, Dictionary<int, StickerDrawerViewModel>>();
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
                    BeginOnUIThread(() => Merge(_favoriteSet.Stickers, favorite.StickersValue));
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

                    BeginOnUIThread(() => Merge(_recentSet.Stickers, recent.StickersValue));
                }
            });
        }

        private void Merge(IList<StickerViewModel> destination, IList<Sticker> origin)
        {
            if (destination.Count > 0)
            {
                for (int i = 0; i < destination.Count; i++)
                {
                    var user = destination[i];
                    var index = -1;

                    for (int j = 0; j < origin.Count; j++)
                    {
                        if (origin[j].SetId == user.SetId && origin[j].StickerValue.Id == user.StickerValue.Id)
                        {
                            index = j;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        destination.Remove(user);
                        i--;
                    }
                }

                for (int i = 0; i < origin.Count; i++)
                {
                    var filter = origin[i];
                    var index = -1;

                    for (int j = 0; j < destination.Count; j++)
                    {
                        if (destination[j].SetId == filter.SetId && destination[j].StickerValue.Id == filter.StickerValue.Id)
                        {
                            destination[j].Update(filter);

                            index = j;
                            break;
                        }
                    }

                    if (index > -1 && index != i)
                    {
                        destination.RemoveAt(index);
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ProtoService, Aggregator, filter));
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ProtoService, Aggregator, filter));
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin.Select(x => new StickerViewModel(ProtoService, Aggregator, x)));
            }
        }

        public void Handle(UpdateInstalledStickerSets update)
        {
            if (update.IsMasks || _updating || !_updated)
            {
                return;
            }

            long hash = 0;
            foreach (var elem in update.StickerSetIds)
            {
                hash = ((hash * 20261) + 0x80000000L + elem) % 0x80000000L;
            }

            if (_updatedHash == hash)
            {
                return;
            }

            _updated = false;
            Update(null);
        }

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
                RaisePropertyChanged(nameof(Stickers));
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
            using (await _supergroupLock.WaitAsync())
            {
                if ((_groupSetId == fullInfo?.StickerSetId && _groupSetChatId == chat.Id) || fullInfo == null)
                {
                    if (fullInfo == null)
                    {
                        _groupSetId = 0;
                        _groupSetChatId = 0;
                        SavedStickers.Remove(_groupSet);
                    }

                    return;
                }

                _groupSetId = 0;
                _groupSetChatId = 0;
                SavedStickers.Remove(_groupSet);

                var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
                if (appData.Values.TryGetValue("Stickers" + chat.Id, out object stickersObj))
                {
                    var stickersId = (long)stickersObj;
                    if (stickersId == fullInfo.StickerSetId)
                    {
                        return;
                    }
                }

                if (fullInfo.StickerSetId != 0)
                {
                    var response = await ProtoService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
                    if (response is StickerSet stickerSet)
                    {
                        _groupSet.Update(chat.Id, stickerSet);

                        if (_groupSet.Stickers != null && _groupSet.Stickers.Count > 0)
                        {
                            _groupSetId = stickerSet.Id;
                            _groupSetChatId = chat.Id;
                            SavedStickers.Add(_groupSet);
                        }
                    }
                }
            }
        }

        //public void HideGroup(TLChannelFull channelFull)
        //{
        //    var appData = ApplicationData.Current.LocalSettings.CreateContainer("Channels", ApplicationDataCreateDisposition.Always);
        //    appData.Values["Stickers" + channelFull.Id] = channelFull.StickerSet?.Id ?? 0;

        //    SavedStickers.Remove(_groupSet);
        //}

        public void Update(Chat chat)
        {
            if (_updated)
            {
                return;
            }

            _updated = true;
            _updating = true;

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

                            long hash = 0;
                            foreach (var elem in sets.Sets)
                            {
                                hash = ((hash * 20261) + 0x80000000L + elem.Id) % 0x80000000L;
                            }

                            _updatedHash = hash;

                            if (sets.Sets.Count > 0)
                            {
                                ProtoService.Send(new GetStickerSet(sets.Sets[0].Id), result4 =>
                                {
                                    _updating = false;

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
                                _updating = false;
                                BeginOnUIThread(() => SavedStickers.ReplaceWith(stickers.Union(sets.Sets.Select(x => new StickerSetViewModel(ProtoService, Aggregator, x)))));
                            }
                        }
                    });
                });
            });
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
    }

    public class SupergroupStickerSetViewModel : StickerSetViewModel
    {
        public SupergroupStickerSetViewModel(IProtoService protoService, IEventAggregator aggregator, StickerSetInfo info)
            : base(protoService, aggregator, info)
        {
        }

        public void Update(long chatId, StickerSet set, bool reset = true)
        {
            //_info.Id = set.Id;
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

        public override void Update(StickerSet set, bool reset = false) { }

        public override void Update(Stickers stickers, bool raise = false) { }

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
                placeholders.Add(new StickerViewModel(_protoService, _aggregator, info.Id, info.IsAnimated));
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

        public virtual void Update(StickerSet set, bool reset = false)
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

        public virtual void Update(Stickers stickers, bool raise = false)
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

        public override string ToString()
        {
            return Title ?? base.ToString();
        }
    }

    public class StickerViewModel
    {
        private Sticker _sticker;
        private readonly long _setId;
        private readonly bool _animated;

        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        public StickerViewModel(IProtoService protoService, IEventAggregator aggregator, long setId, bool animated)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _setId = setId;
            _animated = animated;
        }

        public StickerViewModel(IProtoService protoService, IEventAggregator aggregator, Sticker sticker)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _sticker = sticker;
            _animated = sticker.IsAnimated;
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
        public IList<ClosedVectorPath> Outline => _sticker?.Outline;
        public MaskPosition MaskPosition => _sticker?.MaskPosition;
        public bool IsAnimated => _sticker?.IsAnimated ?? _animated;
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
                                new StickerSetInfo(0, _query, "emoji", null, new ClosedVectorPath[0], false, false, false, false, false, false, stickers.StickersValue.Count, stickers.StickersValue),
                                new StickerSet(0, _query, "emoji", null, new ClosedVectorPath[0], false, false, false, false, false, false, stickers.StickersValue, new Emojis[0])));
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
                                        new StickerSetInfo(0, emojis.EmojisValue[i], "emoji", null, new ClosedVectorPath[0], false, false, false, false, false, false, stickers.StickersValue.Count, stickers.StickersValue),
                                        new StickerSet(0, emojis.EmojisValue[i], "emoji", null, new ClosedVectorPath[0], false, false, false, false, false, false, stickers.StickersValue, new Emojis[0])));
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
