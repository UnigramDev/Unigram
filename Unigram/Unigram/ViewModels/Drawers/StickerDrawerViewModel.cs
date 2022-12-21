using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public class StickerDrawerViewModel : TLViewModelBase
    //, IHandle<UpdateRecentStickers>
    //, IHandle<UpdateFavoriteStickers>
    //, IHandle<UpdateInstalledStickerSets>
    {
        private readonly DisposableMutex _supergroupLock = new();

        private readonly StickerSetViewModel _recentSet;
        private readonly StickerSetViewModel _favoriteSet;
        private readonly StickerSetViewModel _premiumSet;
        private readonly SupergroupStickerSetViewModel _groupSet;

        private List<StickerSetViewModel> _installedSets;

        private long _groupSetId;
        private long _groupSetChatId;

        private bool _updated;
        private bool _updating;

        public StickerDrawerViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _premiumSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.Resources.PremiumStickers,
                Name = "tg/premiumStickers",
                IsInstalled = true
            });

            _favoriteSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.Resources.FavoriteStickers,
                Name = "tg/favedStickers",
                IsInstalled = true
            });

            _recentSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.Resources.RecentStickers,
                Name = "tg/recentlyUsed",
                IsInstalled = true
            });

            _groupSet = new SupergroupStickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.Resources.GroupStickers,
                Name = "tg/groupStickers",
                IsInstalled = true
            });

            SavedStickers = new MvxObservableCollection<StickerSetViewModel>();

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateRecentStickers>(this, Handle)
                .Subscribe<UpdateFavoriteStickers>(Handle)
                .Subscribe<UpdateInstalledStickerSets>(Handle);
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
            ClientService.Send(new GetFavoriteStickers(), result =>
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

            ClientService.Send(new GetRecentStickers(), result =>
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
                    var sticker = destination[i];
                    var index = -1;

                    for (int j = 0; j < origin.Count; j++)
                    {
                        if (origin[j].SetId == sticker.SetId && origin[j].StickerValue.Id == sticker.StickerValue.Id)
                        {
                            index = j;
                            break;
                        }
                    }

                    if (index == -1)
                    {
                        destination.Remove(sticker);
                        i--;
                    }
                }

                for (int i = 0; i < origin.Count; i++)
                {
                    var sticker = origin[i];
                    var index = -1;

                    for (int j = 0; j < destination.Count; j++)
                    {
                        if (destination[j].SetId == sticker.SetId && destination[j].StickerValue.Id == sticker.StickerValue.Id)
                        {
                            destination[j].Update(sticker);

                            index = j;
                            break;
                        }
                    }

                    if (index > -1 && index != i)
                    {
                        destination.RemoveAt(index);
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ClientService, sticker));
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ClientService, sticker));
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin.Select(x => new StickerViewModel(ClientService, x)));
            }
        }

        public void Handle(UpdateInstalledStickerSets update)
        {
            if (update.StickerType is not StickerTypeRegular || _updating || !_updated)
            {
                return;
            }

            _updated = false;
            BeginOnUIThread(() => Update(null));
        }

        public MvxObservableCollection<StickerSetViewModel> SavedStickers { get; private set; }

        private SearchStickerSetsCollection _searchStickers;
        public SearchStickerSetsCollection SearchStickers
        {
            get => _searchStickers;
            set
            {
                Set(ref _searchStickers, value);
                RaisePropertyChanged(nameof(Stickers));
            }
        }

        public MvxObservableCollection<StickerSetViewModel> Stickers => SearchStickers ?? (MvxObservableCollection<StickerSetViewModel>)SavedStickers;

        public async void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchStickers = null;
            }
            else
            {
                var items = SearchStickers = new SearchStickerSetsCollection(ClientService, new StickerTypeRegular(), query, 0);
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
                    var response = await ClientService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
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

        public async void Update(Chat chat)
        {
            if (_updated)
            {
                return;
            }

            _updated = true;
            _updating = true;

            var result1 = await ClientService.SendAsync(new GetFavoriteStickers());
            var result2 = await ClientService.SendAsync(new GetRecentStickers());
            var result3 = await ClientService.SendAsync(new GetPremiumStickers(60));
            var result4 = await GetInstalledSets();

            if (result1 is Stickers favorite && result2 is Stickers recent && result3 is Stickers premium)
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

                _favoriteSet.Update(favorite.StickersValue);
                _recentSet.Update(recent.StickersValue);
                _premiumSet.Update(premium.StickersValue);

                var stickers = new List<StickerSetViewModel>();
                if (_favoriteSet.Stickers.Count > 0)
                {
                    stickers.Add(_favoriteSet);
                }
                if (_recentSet.Stickers.Count > 0)
                {
                    stickers.Add(_recentSet);
                }
                if (_premiumSet.Stickers.Count > 0 && IsPremium)
                {
                    stickers.Add(_premiumSet);
                }
                if (_groupSet.Stickers.Count > 0 && _groupSet.ChatId == chat?.Id)
                {
                    stickers.Add(_groupSet);
                }

                if (result4.Count > 0)
                {
                    if (result4[0].IsLoaded is false)
                    {
                        result4[0].IsLoaded = true;

                        var response = await ClientService.SendAsync(new GetStickerSet(result4[0].Id));
                        if (response is StickerSet full)
                        {
                            result4[0].Update(full, false);
                        }
                    }

                    stickers.AddRange(result4);

                    if (_premiumSet.Stickers.Count > 0 && IsPremiumAvailable && !IsPremium)
                    {
                        stickers.Add(_premiumSet);
                    }

                    SavedStickers.ReplaceWith(stickers);
                    _updating = false;
                }
                else
                {
                    SavedStickers.ReplaceWith(stickers);
                    _updating = false;
                }
            }
        }

        private async Task<IList<StickerSetViewModel>> GetInstalledSets()
        {
            if (_installedSets != null)
            {
                return _installedSets;
            }

            var result1 = await ClientService.SendAsync(new GetInstalledStickerSets(new StickerTypeRegular()));
            //var result2 = await ClientService.SendAsync(new GetTrendingStickerSets(new StickerTypeRegular(), 0, 100));

            if (result1 is StickerSets sets /*&& result2 is TrendingStickerSets trending*/)
            {
                var stickers = new List<object>();

                var installedSets = new List<StickerSetViewModel>();

                if (sets.Sets.Count > 0)
                {
                    var result3 = await ClientService.SendAsync(new GetStickerSet(sets.Sets[0].Id));
                    if (result3 is StickerSet set)
                    {
                        installedSets.Add(new StickerSetViewModel(ClientService, sets.Sets[0], set));
                        installedSets.AddRange(sets.Sets.Skip(1).Select(x => new StickerSetViewModel(ClientService, x)));
                    }
                    else
                    {
                        installedSets.AddRange(sets.Sets.Select(x => new StickerSetViewModel(ClientService, x)));
                    }

                    //var existing = installedSets.Select(x => x.Id).ToArray();

                    //foreach (var item in trending.Sets)
                    //{
                    //    if (existing.Contains(item.Id))
                    //    {
                    //        continue;
                    //    }

                    //    installedSets.Add(new StickerSetViewModel(ClientService, item));
                    //}
                }
                //else if (trending.Sets.Count > 0)
                //{
                //    installedSets.AddRange(trending.Sets.Select(x => new StickerSetViewModel(ClientService, x)));
                //}

                _installedSets = installedSets;
                return installedSets;
            }

            return Array.Empty<StickerSetViewModel>();
        }

        private int _featuredUnreadCount;
        public int FeaturedUnreadCount
        {
            get => _featuredUnreadCount;
            set => Set(ref _featuredUnreadCount, value);
        }
    }

    public class SupergroupStickerSetViewModel : StickerSetViewModel
    {
        public SupergroupStickerSetViewModel(IClientService clientService, StickerSetInfo info)
            : base(clientService, info)
        {
        }

        public void Update(long chatId, StickerSet set, bool reset = true)
        {
            //_info.Id = set.Id;
            ChatId = chatId;

            if (reset)
            {
                Stickers = new MvxObservableCollection<StickerViewModel>(set.Stickers.Select(x => new StickerViewModel(_clientService, x)));
            }
            else
            {
                Stickers.ReplaceWith(set.Stickers.Select(x => new StickerViewModel(_clientService, x)));
            }
        }

        public override void Update(StickerSet set, bool reset = false) { }

        public override void Update(IEnumerable<Sticker> stickers, bool raise = false) { }

        public long ChatId { get; private set; }
    }

    public class StickerSetViewModel
    {
        protected readonly IClientService _clientService;

        protected readonly StickerSetInfo _info;
        protected StickerSet _set;

        public StickerSetViewModel(IClientService clientService, StickerSetInfo info)
        {
            _clientService = clientService;
            _info = info;

            var placeholders = new List<StickerViewModel>();

            if (info.Covers?.Count > 0 && !info.IsInstalled && info.StickerType is StickerTypeCustomEmoji)
            {
                IsLoaded = true;

                var limit = info.Size > info.Covers.Count;
                var count = limit ? 15 : info.Size;

                for (int i = 0; i < count; i++)
                {
                    placeholders.Add(new StickerViewModel(_clientService, info.Covers[i]));
                }

                if (limit)
                {
                    placeholders.Add(new MoreStickerViewModel(_clientService, info.Id, info.StickerFormat, info.Size - count));
                }
            }
            else
            {
                for (int i = 0; i < info.Size; i++)
                {
                    placeholders.Add(new StickerViewModel(_clientService, info.Id, info.StickerFormat));
                }
            }

            Stickers = new MvxObservableCollection<StickerViewModel>(placeholders);
            Covers = info.Covers;
        }

        public StickerSetViewModel(IClientService clientService, StickerSetInfo info, StickerSet set)
            : this(clientService, info)
        {
            IsLoaded = true;
            Update(set);
        }

        public StickerSetViewModel(IClientService clientService, StickerSet set)
            : this(clientService, set.ToInfo())
        {
            IsLoaded = true;
            Update(set);
        }

        public StickerSetViewModel(IClientService clientService, StickerSetInfo info, IList<Sticker> stickers)
        {
            _clientService = clientService;

            _info = info;

            IsLoaded = true;
            Stickers = new MvxObservableCollection<StickerViewModel>(stickers.Select(x => new StickerViewModel(clientService, x)));
            Covers = info.Covers;
        }

        public virtual void Update(StickerSet set, bool reset = false)
        {
            _set = set;

            for (int i = 0; i < set.Stickers.Count; i++)
            {
                if (i < Stickers.Count)
                {
                    if (Stickers[i] is MoreStickerViewModel)
                    {
                        Stickers[i] = new StickerViewModel(_clientService, set.Stickers[i]);
                    }
                    else
                    {
                        Stickers[i].Update(set.Stickers[i]);
                    }
                }
                else
                {
                    Stickers.Add(new StickerViewModel(_clientService, set.Stickers[i]));
                }
            }

            if (reset)
            {
                Stickers.Reset();
            }
        }

        public virtual void Update(IEnumerable<Sticker> stickers, bool raise = false)
        {
            stickers ??= Enumerable.Empty<Sticker>();

            if (raise)
            {
                Stickers.ReplaceWith(stickers.Select(x => new StickerViewModel(_clientService, x)));
            }
            else
            {
                Stickers = new MvxObservableCollection<StickerViewModel>(stickers.Select(x => new StickerViewModel(_clientService, x)));
            }
        }

        public MvxObservableCollection<StickerViewModel> Stickers { get; protected set; }

        public bool IsLoaded { get; set; }

        public bool IsViewed => _set?.IsViewed ?? _info.IsViewed;
        public StickerType StickerType => _set?.StickerType ?? _info.StickerType;
        public StickerFormat StickerFormat => _set?.StickerFormat ?? _info.StickerFormat;
        public bool IsOfficial => _set?.IsOfficial ?? _info.IsOfficial;
        public bool IsArchived => _set?.IsArchived ?? _info.IsArchived;
        public bool IsInstalled => _set?.IsInstalled ?? _info.IsInstalled;
        public string Name => _set?.Name ?? _info.Name;
        public string Title => _set?.Title ?? _info.Title;
        public long Id => _set?.Id ?? _info.Id;

        public Thumbnail Thumbnail => _set?.Thumbnail ?? _info.Thumbnail;

        public IList<Sticker> Covers { get; private set; }

        public int Size => Covers.Count;

        public Sticker GetThumbnail()
        {
            return _info?.GetThumbnail();
        }

        public override string ToString()
        {
            return Title ?? base.ToString();
        }
    }

    public class StickerViewModel
    {
        private Sticker _sticker;
        private readonly long _setId;
        private readonly StickerFormat _format;

        private readonly IClientService _clientService;

        public StickerViewModel(IClientService clientService, long setId, StickerFormat format)
        {
            _clientService = clientService;

            _setId = setId;
            _format = format;
        }

        public StickerViewModel(IClientService clientService, Sticker sticker)
        {
            _clientService = clientService;

            _sticker = sticker;
            _format = sticker.Format;
        }

        public void Update(Sticker sticker)
        {
            _sticker = sticker;
        }

        public IClientService ClientService => _clientService;

        public static implicit operator Sticker(StickerViewModel viewModel)
        {
            return viewModel._sticker;
        }

        public File StickerValue => _sticker?.StickerValue;
        public IList<ClosedVectorPath> Outline => _sticker?.Outline;
        public StickerTypeFullInfo FullType => _sticker?.FullType;
        public StickerFormat Format => _sticker?.Format ?? _format;
        public string Emoji => _sticker?.Emoji;
        public int Height => _sticker?.Height ?? 0;
        public int Width => _sticker?.Width ?? 0;
        public long SetId => _sticker?.SetId ?? _setId;

        public ReactionType ToReactionType()
        {
            if (FullType is StickerTypeFullInfoCustomEmoji customEmoji)
            {
                return new ReactionTypeCustomEmoji(customEmoji.CustomEmojiId);
            }

            return new ReactionTypeEmoji(Emoji);
        }
    }

    public class MoreStickerViewModel : StickerViewModel
    {
        public MoreStickerViewModel(IClientService clientService, long setId, StickerFormat format, int totalCount)
            : base(clientService, setId, format)
        {
            TotalCount = totalCount;
        }

        public int TotalCount { get; set; }
    }

    public class SearchStickerSetsCollection : MvxObservableCollection<StickerSetViewModel>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly StickerType _type;
        private readonly string _query;
        private readonly string _inputLanguage;
        private readonly long _chatId;

        public SearchStickerSetsCollection(IClientService clientService, StickerType type, string query, long chatId)
        {
            _clientService = clientService;
            _type = type;
            _query = query;
            _inputLanguage = CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag;
            _chatId = chatId;
        }

        public string Query => _query;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint phase)
        {
            return AsyncInfo.Run(async token =>
            {
                if (phase == 0)
                {
                    var response = await _clientService.SendAsync(new SearchInstalledStickerSets(_type, _query, 100));
                    if (response is StickerSets sets)
                    {
                        foreach (var item in sets.Sets.Select(x => new StickerSetViewModel(_clientService, x)))
                        {
                            Add(item);
                        }

                        //AddRange(sets.Sets.Select(x => new StickerSetViewModel(_clientService, _aggregator, x)));
                    }
                }
                else if (phase == 1 && _query.Length > 1)
                {
                    if (Emoji.ContainsSingleEmoji(_query))
                    {
                        var response = await _clientService.SendAsync(new GetStickers(_type, _query, 100, _chatId));
                        if (response is Stickers stickers && stickers.StickersValue.Count > 0)
                        {
                            Add(new StickerSetViewModel(_clientService,
                                new StickerSetInfo(0, _query, "emoji", null, new ClosedVectorPath[0], false, false, false, new StickerFormatWebp(), _type, false, stickers.StickersValue.Count, stickers.StickersValue),
                                new StickerSet(0, _query, "emoji", null, new ClosedVectorPath[0], false, false, false, new StickerFormatWebp(), _type, false, stickers.StickersValue, new Emojis[0])));
                        }
                    }
                    else
                    {
                        var emojis = await _clientService.SendAsync(new SearchEmojis(_query, false, new[] { _inputLanguage })) as Emojis;
                        if (emojis != null)
                        {
                            for (int i = 0; i < Math.Min(10, emojis.EmojisValue.Count); i++)
                            {
                                var response = await _clientService.SendAsync(new GetStickers(_type, emojis.EmojisValue[i], 100, _chatId));
                                if (response is Stickers stickers && stickers.StickersValue.Count > 0)
                                {
                                    Add(new StickerSetViewModel(_clientService,
                                        new StickerSetInfo(0, emojis.EmojisValue[i], "emoji", null, new ClosedVectorPath[0], false, false, false, new StickerFormatWebp(), _type, false, stickers.StickersValue.Count, stickers.StickersValue),
                                        new StickerSet(0, emojis.EmojisValue[i], "emoji", null, new ClosedVectorPath[0], false, false, false, new StickerFormatWebp(), _type, false, stickers.StickersValue, new Emojis[0])));
                                }
                            }
                        }
                    }
                }
                else if (phase == 2)
                {
                    var response = await _clientService.SendAsync(new SearchStickerSets(_query));
                    if (response is StickerSets sets)
                    {
                        foreach (var item in sets.Sets.Select(x => new StickerSetViewModel(_clientService, x, x.Covers)))
                        {
                            Add(item);
                        }

                        //AddRange(sets.Sets.Select(x => new StickerSetViewModel(_clientService, _aggregator, x)));
                    }
                }

                return new LoadMoreItemsResult();
            });
        }

        public bool HasMoreItems => false;
    }
}
