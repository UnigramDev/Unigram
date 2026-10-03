//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using WinRT;

namespace Telegram.ViewModels.Drawers
{
    public partial class StickerDrawerViewModel : ViewModelBase
    {
        private readonly DisposableMutex _supergroupLock = new();

        private readonly StickerSetViewModel _recentSet;
        private readonly StickerSetViewModel _favoriteSet;
        private readonly SupergroupStickerSetViewModel _groupSet;

        private Dictionary<long, StickerSetViewModel> _installedSets;

        private long _groupSetId;
        private long _groupSetChatId;

        private long _chatId;

        private bool _activated;
        private bool _updated;
        private bool _updating;

        public StickerDrawerViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _favoriteSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.FavoriteStickers,
                Name = "tg/favedStickers",
                IsInstalled = true
            });

            _recentSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.RecentStickers,
                Name = "tg/recentlyUsed",
                IsInstalled = true
            });

            _groupSet = new SupergroupStickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.GroupStickers,
                Name = "tg/groupStickers",
                IsInstalled = true
            });

            SavedStickers = new RangeObservableCollection<StickerSetViewModel>();

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateRecentStickers>(this, Handle)
                .Subscribe<UpdateFavoriteStickers>(Handle)
                .Subscribe<UpdateInstalledStickerSets>(Handle);
        }

        public static StickerDrawerViewModel Create(ISession session)
        {
            var context = session.Resolve<StickerDrawerViewModel>();
            context.Dispatcher = DispatcherContext.Current;
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

            ClientService.Send(new GetRecentStickers(false), result =>
            {
                if (result is Stickers recent)
                {
                    BeginOnUIThread(() => Merge(_recentSet.Stickers, recent.StickersValue
                        .Where(rec => !_favoriteSet.Stickers.Any(fav => fav.StickerValue.Id == rec.StickerValue.Id))
                        .ToVector()));
                }
            });
        }

        private void Merge(IList<StickerViewModel> destination, Vector<Sticker> origin)
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
            if (update.StickerType is not StickerTypeRegular || _updating || !_updated || !_activated)
            {
                return;
            }

            _updated = false;
            _installedSets = null;
            BeginOnUIThread(() => Update(null));
        }

        public RangeObservableCollection<StickerSetViewModel> SavedStickers { get; private set; }

        // The search in flight, kept apart from SearchStickers because that one also holds the
        // plain list the premium category produces, which has no cascade to cancel.
        private SearchStickerSetsCollection _searchCollection;

        private RangeObservableCollection<StickerSetViewModel> _searchStickers;
        public RangeObservableCollection<StickerSetViewModel> SearchStickers
        {
            get => _searchStickers;
            set
            {
                Set(ref _searchStickers, value);
                RaisePropertyChanged(nameof(Stickers));
            }
        }

        public RangeObservableCollection<StickerSetViewModel> Stickers => SearchStickers ?? SavedStickers;

        /// <summary>
        /// Starts a search and hands back the collection it will fill. The cascade runs on its
        /// own; the caller awaits <see cref="SearchStickerSetsCollection.SearchAsync"/> on the
        /// result when it needs to know that it has finished.
        /// </summary>
        public SearchStickerSetsCollection Search(string query, bool emojiOnly)
        {
            // Whatever the previous query still has in flight belongs to a list that is about to
            // be replaced, and must not write into it.
            _searchCollection?.Cancel();
            _searchCollection = null;

            if (string.IsNullOrWhiteSpace(query))
            {
                SearchStickers = null;
                return null;
            }

            var items = new SearchStickerSetsCollection(ClientService, new StickerTypeRegular(), query, _chatId, emojiOnly);

            _searchCollection = items;
            SearchStickers = items;

            return items;
        }

        public async Task SearchAsync(EmojiCategorySource source)
        {
            if (source is EmojiCategorySourceSearch search)
            {
                var items = Search(string.Join(" ", search.Emojis), true);
                if (items != null)
                {
                    await items.SearchAsync();
                }
            }
            else
            {
                _searchCollection?.Cancel();
                _searchCollection = null;

                SearchStickers = new RangeObservableCollection<StickerSetViewModel>();

                var response = await ClientService.SendAsync(new GetPremiumStickers(100));
                if (response is Stickers stickers)
                {
                    // Filled before it is bound: adding the group afterwards would mutate the
                    // grouped source the drawer is already showing.
                    SearchStickers = new RangeObservableCollection<StickerSetViewModel>
                    {
                        new StickerSetViewModel(ClientService,
                            new StickerSetInfo(0, string.Empty, "emoji", null, null, false, false, false, false, new StickerTypeRegular(), false, false, false, stickers.StickersValue.Count, stickers.StickersValue),
                            new StickerSet(0, string.Empty, "emoji", null, null, false, false, false, false, new StickerTypeRegular(), false, false, false, stickers.StickersValue, Array.Empty<Emojis>()))
                    };
                }
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

                if (AppSettings.Stickers.TryGetHiddenGroupStickerSet(chat.Id, out long hiddenSetId)
                    && hiddenSetId == fullInfo.StickerSetId)
                {
                    return;
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
            // Which stickers are available depends on the chat, so the search carries it too.
            // Set before the guard below, which skips everything else on a re-activation.
            if (chat != null)
            {
                _chatId = chat.Id;
            }

            if (_updated)
            {
                return;
            }

            _activated = true;
            _updated = true;
            _updating = true;

            var result1 = await ClientService.SendAsync(new GetFavoriteStickers());
            var result2 = await ClientService.SendAsync(new GetRecentStickers(false));
            var result4 = await GetInstalledSets();

            if (result1 is Stickers favorite && result2 is Stickers recent)
            {
                recent.StickersValue = recent.StickersValue
                    .Where(rec => !favorite.StickersValue.Any(fav => fav.StickerValue.Id == rec.StickerValue.Id))
                    .ToVector();

                _favoriteSet.Update(favorite.StickersValue);
                _recentSet.Update(recent.StickersValue);

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

                stickers.AddRange(result4);

                SavedStickers.ReplaceWith(stickers);
                _updating = false;
            }
        }

        private async Task<IEnumerable<StickerSetViewModel>> GetInstalledSets()
        {
            if (_installedSets != null)
            {
                return _installedSets.Values;
            }

            var result1 = await ClientService.SendAsync(new GetInstalledStickerSets(new StickerTypeRegular()));
            //var result2 = await ClientService.SendAsync(new GetTrendingStickerSets(new StickerTypeRegular(), 0, 100));

            if (result1 is StickerSets sets /*&& result2 is TrendingStickerSets trending*/)
            {
                var stickers = new List<object>();

                var installedSets = new Dictionary<long, StickerSetViewModel>();

                if (sets.Sets.Count > 0)
                {
                    var result3 = await ClientService.SendAsync(new GetStickerSet(sets.Sets[0].Id));
                    if (result3 is StickerSet set)
                    {
                        installedSets[set.Id] = new StickerSetViewModel(ClientService, sets.Sets[0], set);
                    }

                    for (int i = installedSets.Count; i < sets.Sets.Count; i++)
                    {
                        installedSets[sets.Sets[i].Id] = new StickerSetViewModel(ClientService, sets.Sets[i]);
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
                return installedSets.Values;
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

    public partial class SupergroupStickerSetViewModel : StickerSetViewModel
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
                Stickers = new RangeObservableCollection<StickerViewModel>(set.Stickers.Select(x => new StickerViewModel(_clientService, x)));
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

    // Stickers is the one property resolved by name, by CollectionViewSource.ItemsPath; everything
    // else these sets expose is bound through x:Bind. The second array is the parameter types of
    // indexers to expose, not property types.
    [GeneratedBindableCustomProperty(new[] { "Stickers" }, new Type[] { })]
    public partial class StickerSetViewModel : IDrawerGroup
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
                var count = Math.Min(info.Covers.Count, limit ? 15 : info.Size);

                for (int i = 0; i < count; i++)
                {
                    placeholders.Add(new StickerViewModel(_clientService, info.Covers[i]));
                }

                if (limit)
                {
                    placeholders.Add(new MoreStickerViewModel(_clientService, info.Id, info.Size - count));
                }
            }
            else
            {
                for (int i = 0; i < info.Size; i++)
                {
                    placeholders.Add(new StickerViewModel(_clientService, info.Id));
                }
            }

            Stickers = new RangeObservableCollection<StickerViewModel>(placeholders);
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

        public StickerSetViewModel(IClientService clientService, StickerSetInfo info, Vector<Sticker> stickers)
        {
            _clientService = clientService;

            _info = info;

            IsLoaded = true;
            Stickers = new RangeObservableCollection<StickerViewModel>(stickers.Select(x => new StickerViewModel(clientService, x)));
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
                Stickers = new RangeObservableCollection<StickerViewModel>(stickers.Select(x => new StickerViewModel(_clientService, x)));
            }
        }

        public virtual void Update(IEnumerable<StickerViewModel> stickers, bool raise = false)
        {
            stickers ??= Enumerable.Empty<StickerViewModel>();

            if (raise)
            {
                Stickers.ReplaceWith(stickers);
            }
            else
            {
                Stickers = new RangeObservableCollection<StickerViewModel>(stickers);
            }
        }

        public RangeObservableCollection<StickerViewModel> Stickers { get; protected set; }

        public bool IsLoaded { get; set; }

        public bool IsViewed => _set?.IsViewed ?? _info.IsViewed;
        public StickerType StickerType => _set?.StickerType ?? _info.StickerType;
        public bool IsOwned => _set?.IsOwned ?? _info.IsOwned;
        public bool IsOfficial => _set?.IsOfficial ?? _info.IsOfficial;
        public bool IsArchived => _set?.IsArchived ?? _info.IsArchived;
        public bool IsInstalled => _set?.IsInstalled ?? _info.IsInstalled;
        public bool IsAllowedAsChatEmojiStatus => _set?.IsAllowedAsChatEmojiStatus ?? _info.IsAllowedAsChatEmojiStatus;
        public bool NeedsRepainting => _set?.NeedsRepainting ?? _info.NeedsRepainting;
        public string Name => _set?.Name ?? _info.Name;
        public string Title => _set?.Title ?? _info.Title;
        public long Id => _set?.Id ?? _info.Id;

        public Thumbnail Thumbnail => _set?.Thumbnail ?? _info.Thumbnail;
        public Outline ThumbnailOutline => _set?.ThumbnailOutline ?? _info.ThumbnailOutline;

        public Vector<Sticker> Covers { get; private set; }

        public int Size => Covers.Count;

        public override string ToString()
        {
            return Title ?? base.ToString();
        }

        public static implicit operator StickerSetInfo(StickerSetViewModel viewModel)
        {
            return viewModel._info;
        }
    }

    public partial class StickerViewModel
    {
        private readonly IClientService _clientService;

        public StickerViewModel(IClientService clientService, long setId)
        {
            _clientService = clientService;

            SetId = setId;
        }

        public StickerViewModel(IClientService clientService, Sticker sticker, EmojiStatusType emojiStatusType = null, AvailableReaction reaction = null)
        {
            _clientService = clientService;
            Update(sticker, emojiStatusType, reaction);
        }

        public void Update(Sticker sticker, EmojiStatusType emojiStatusType = null, AvailableReaction reaction = null)
        {
            Id = sticker.Id;
            StickerValue = sticker.StickerValue;
            Thumbnail = sticker.Thumbnail;
            FullType = sticker.FullType;
            Format = sticker.Format;
            Emoji = sticker.Emoji;
            Height = sticker.Height;
            Width = sticker.Width;
            SetId = sticker.SetId;

            if (emojiStatusType != null)
            {
                EmojiStatusType ??= emojiStatusType;
            }
            else if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                EmojiStatusType ??= new EmojiStatusTypeCustomEmoji(customEmoji.CustomEmojiId);
            }

            if (reaction != null)
            {
                Reaction ??= reaction;
            }
            else if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                Reaction ??= new AvailableReaction(new ReactionTypeCustomEmoji(customEmoji.CustomEmojiId), true);
            }
        }

        public IClientService ClientService => _clientService;

        public static implicit operator Sticker(StickerViewModel viewModel)
        {
            return new Sticker(viewModel.Id, viewModel.SetId, viewModel.Width, viewModel.Height, viewModel.Emoji ?? string.Empty, viewModel.Format, viewModel.FullType, viewModel.Thumbnail, viewModel.StickerValue); //viewModel._sticker;
        }

        public long Id { get; private set; }
        public File StickerValue { get; private set; }
        public Thumbnail Thumbnail { get; private set; }
        public StickerFullType FullType { get; private set; }
        public StickerFormat Format { get; private set; }
        public string Emoji { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }
        public long SetId { get; private set; }
        public EmojiStatusType EmojiStatusType { get; set; }

        public AvailableReaction Reaction { get; set; }

        public ReactionType ToReactionType()
        {
            if (FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                return new ReactionTypeCustomEmoji(customEmoji.CustomEmojiId);
            }
            else if (!string.IsNullOrEmpty(Emoji))
            {
                return new ReactionTypeEmoji(Emoji);
            }

            return null;
        }

        public override string ToString()
        {
            if (FullType is StickerFullTypeCustomEmoji)
            {
                return Strings.AccDescrCustomEmoji2;
            }

            return Emoji ?? base.ToString();
        }
    }

    public partial class MoreStickerViewModel : StickerViewModel
    {
        public MoreStickerViewModel(IClientService clientService, long setId, int totalCount)
            : base(clientService, setId)
        {
            TotalCount = totalCount;
        }

        public int TotalCount { get; set; }
    }

    /// <summary>
    /// The sticker panel search. Runs the same four sources Telegram Desktop and Android do, in
    /// the order they present them: stickers already installed, then what the server finds, then
    /// the packs whose name matches — locally first, then on the server.
    /// </summary>
    /// <remarks>
    /// Only the cloud sticker search pages; the cascade itself runs once, through
    /// <see cref="SearchAsync"/>, and applies each source as it lands rather than waiting for the
    /// slowest one.
    /// </remarks>
    public partial class SearchStickerSetsCollection : IncrementalCollection<StickerSetViewModel>
    {
        // What one page asks for. It is part of the key TDLib caches the result under, so it has
        // to be the same on every page or none of them ever hit.
        private const int Limit = 100;

        private readonly IClientService _clientService;
        private readonly StickerType _type;
        private readonly string _query;
        private readonly string _inputLanguage;
        private readonly long _chatId;
        private readonly bool _emojiOnly;

        // The emoji half of the query, resolved from the keyword database before the cloud search
        // that takes it alongside the text. A category arrives as emoji already, and sends no text
        // half at all: the server would read a string of emoji as a name to match and find nothing.
        private string _emojis;
        private readonly string _cloudQuery;

        // Shared by the local and the cloud pass, so a sticker the user already has isn't listed
        // twice, and by the pages of the cloud pass, which can overlap.
        private readonly HashSet<int> _ids = new();

        private StickerSetViewModel _cloudSet;
        private int _offset;

        private bool _cancelled;

        public SearchStickerSetsCollection(IClientService clientService, StickerType type, string query, long chatId, bool emojiOnly)
        {
            _clientService = clientService;
            _type = type;
            _query = query;
            _inputLanguage = NativeUtils.GetKeyboardCulture();
            _chatId = chatId;
            _emojiOnly = emojiOnly;
            _emojis = emojiOnly ? query : string.Empty;
            _cloudQuery = emojiOnly ? string.Empty : query;

            // Nothing to page until the first cloud page has come back and said there is.
            HasMoreItems = false;
        }

        /// <summary>
        /// Stops applying anything further. The cascade is not driven by the list, so there is no
        /// version for it to check: a query replaced while its requests are in flight says so here.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
            HasMoreItems = false;
        }

        public async Task SearchAsync()
        {
            // A category stands for a list of emoji and has no text to match a pack name against.
            if (_emojiOnly)
            {
                await SearchCloudStickersAsync(string.Empty);
                return;
            }

            // Local first, so something is on screen while the two cloud requests are out — and
            // the cloud stickers last of all, because that is the group that pages: anything below
            // it would be pushed further down every time another page arrives. Desktop and Android
            // can afford to put the packs underneath only because theirs live in a strip above the
            // grid rather than in it.
            await SearchInstalledSetsAsync();
            await ResolveEmojisAsync();
            await SearchLocalStickersAsync();
            await SearchCloudSetsAsync();
            await SearchCloudStickersAsync(Strings.StickerOrEmojiGlobalSearchResult);
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var before = _cloudSet?.Stickers.Count ?? 0;
            await SearchCloudStickersAsync(Strings.StickerOrEmojiGlobalSearchResult);

            var added = (_cloudSet?.Stickers.Count ?? 0) - before;
            return new IncrementalLoadResult((uint)Math.Max(added, 0), HasMoreItems);
        }

        private async Task ResolveEmojisAsync()
        {
            // The server's sticker search takes the text and the emoji it stands for side by side.
            // Desktop and Android both fill the second from their own keyword database; ours lives
            // in TDLib, so this is the one request that has to happen before the cloud pass.
            if (Emoji.ContainsSingleEmoji(_query))
            {
                _emojis = _query;
                return;
            }

            var response = await _clientService.SendAsync(new SearchEmojis(_query, new[] { _inputLanguage }));
            if (response is EmojiKeywords emojis && !_cancelled)
            {
                _emojis = string.Join(" ", emojis.EmojiKeywordsValue.Select(x => x.Emoji).Distinct());
            }
        }

        private async Task SearchLocalStickersAsync()
        {
            // By the emoji the query resolved to, which is how both other clients look up what is
            // installed. Passing the text instead finds almost nothing: get_stickers drops the
            // whole emoji list if any word in the query isn't an emoji, and falls back to matching
            // per-sticker keywords, which most regular sets don't carry. The text is still worth
            // one attempt when nothing resolved, since those keywords are all there is to go on.
            var query = _emojis.Length > 0 ? _emojis : _query;

            var response = await _clientService.SendAsync(new GetStickers(_type, query, Limit, _chatId));
            if (response is Stickers stickers && !_cancelled)
            {
                var found = Collect(stickers);
                if (found.Count > 0)
                {
                    AddGroup(CreateGroup(Strings.StickerOrEmojiSearchResult, found));
                }
            }
        }

        private async Task SearchCloudStickersAsync(string title)
        {
            var response = await _clientService.SendAsync(new SearchStickers(_type, _emojis, _cloudQuery, new[] { _inputLanguage }, _offset, Limit));
            if (response is not Stickers stickers || _cancelled)
            {
                HasMoreItems = false;
                return;
            }

            _offset += stickers.StickersValue.Count;

            // td_api has no next offset to hand back, so a short page is the end of the results.
            HasMoreItems = stickers.StickersValue.Count >= Limit;

            var found = Collect(stickers);
            if (found.Count == 0)
            {
                return;
            }

            if (_cloudSet == null)
            {
                _cloudSet = CreateGroup(title, found);
                AddGroup(_cloudSet);
            }
            else
            {
                // A page after the first lands inside a group the list is already showing, which
                // is an ordinary item add rather than the group add below.
                _cloudSet.Stickers.AddRange(found.Select(x => new StickerViewModel(_clientService, x)));
            }
        }

        private async Task SearchInstalledSetsAsync()
        {
            var response = await _clientService.SendAsync(new SearchInstalledStickerSets(_type, _query, Limit));
            if (response is StickerSets sets && !_cancelled)
            {
                AddGroups(sets.Sets.Select(x => new StickerSetViewModel(_clientService, x)));
            }
        }

        private async Task SearchCloudSetsAsync()
        {
            var response = await _clientService.SendAsync(new SearchStickerSets(_type, _query));
            if (response is StickerSets sets && !_cancelled)
            {
                AddGroups(sets.Sets.Select(x => new StickerSetViewModel(_clientService, x, x.Covers)));
            }
        }

        private MutableVector<Sticker> Collect(Stickers stickers)
        {
            var result = new MutableVector<Sticker>();

            foreach (var sticker in stickers.StickersValue)
            {
                if (_ids.Add(sticker.StickerValue.Id))
                {
                    result.Add(sticker);
                }
            }

            return result;
        }

        private StickerSetViewModel CreateGroup(string title, MutableVector<Sticker> stickers)
        {
            return new StickerSetViewModel(_clientService,
                new StickerSetInfo(0, title, "emoji", null, null, false, false, false, false, _type, false, false, false, stickers.Count, stickers),
                new StickerSet(0, title, "emoji", null, null, false, false, false, false, _type, false, false, false, stickers, Array.Empty<Emojis>()));
        }

        // Appending a group to a live grouped source takes the GridView through
        // ModernCollectionBasePanel::OnGroupAdded, whose incremental group-cache renewal
        // faults; a single Reset makes it rebuild the cache instead.
        private void AddGroup(StickerSetViewModel group)
        {
            using (SuppressEvents())
            {
                Add(group);
            }

            Reset();
        }

        private void AddGroups(IEnumerable<StickerSetViewModel> groups)
        {
            var count = Count;

            using (SuppressEvents())
            {
                foreach (var group in groups)
                {
                    Add(group);
                }
            }

            if (Count != count)
            {
                Reset();
            }
        }
    }
}
