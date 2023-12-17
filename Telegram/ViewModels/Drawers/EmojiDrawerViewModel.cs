//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;

namespace Telegram.ViewModels.Drawers
{
    public enum EmojiDrawerMode
    {
        Chat,
        Reactions,
        EmojiStatus,
        ChatPhoto,
        UserPhoto,
        Background,
        Topics
    }

    public class EmojiDrawerViewModel : ViewModelBase
    {
        private bool _updated;

        private readonly StickerSetViewModel _reactionTopSet;
        private readonly StickerSetViewModel _reactionRecentSet;

        public EmojiDrawerViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            //Items = new DiffObservableCollection<object>(new EmojiSetDiffHandler());
            Items = new MvxObservableCollection<object>();

            _reactionTopSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = string.Empty,
                Name = "tg/recentlyUsed",
                IsInstalled = true
            });

            _reactionRecentSet = new StickerSetViewModel(ClientService, new StickerSetInfo
            {
                Title = Strings.RecentStickers,
                Name = "tg/recentlyUsed",
                IsInstalled = true
            });

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateInstalledStickerSets>(this, Handle);
        }

        public static EmojiDrawerViewModel Create(int sessionId, EmojiDrawerMode mode = EmojiDrawerMode.Chat)
        {
            var context = TypeResolver.Current.Resolve<EmojiDrawerViewModel>(sessionId);
            context.Dispatcher = WindowContext.Current.Dispatcher;
            context.Mode = mode;
            return context;
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
            if (update.StickerType is not StickerTypeCustomEmoji)
            {
                return;
            }

            BeginOnUIThread(() => Update());
        }

        public MvxObservableCollection<EmojiGroup> StandardSets { get; } = new MvxObservableCollection<EmojiGroup>();
        public MvxObservableCollection<StickerSetViewModel> InstalledSets { get; } = new MvxObservableCollection<StickerSetViewModel>();

        public MvxObservableCollection<object> Items { get; private set; }

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

        private EmojiDrawerMode _mode;
        public EmojiDrawerMode Mode
        {
            get => _mode;
            set => Set(ref _mode, value);
        }

        //public MvxObservableCollection<StickerSetViewModel> Stickers => SearchStickers ?? (MvxObservableCollection<StickerSetViewModel>)SavedStickers;

        public async void Search(string query, bool emojiOnly)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchStickers = null;
            }
            else
            {
                var items = SearchStickers = new SearchStickerSetsCollection(ClientService, new StickerTypeCustomEmoji(), query, 0, emojiOnly);
                await items.LoadMoreItemsAsync(0);
            }
        }

        public async void Update()
        {
            if (_updated && _mode == EmojiDrawerMode.Chat)
            {
                return;
            }

            _updated = true;

            var stickers = new List<object>();

            if (_mode == EmojiDrawerMode.Chat)
            {
                var recents = Emoji.GetRecents(SettingsService.Current.Stickers.SkinTone);
                var emojiGroups = Emoji.Get(SettingsService.Current.Stickers.SkinTone);

                var source = new List<object>();
                var customEmoji = new List<long>();

                foreach (var item in recents)
                {
                    if (item.Value.Contains(';'))
                    {
                        var split = item.Value.Split(';');
                        if (split.Length == 2 && long.TryParse(split[1], out long customEmojiId))
                        {
                            customEmoji.Add(customEmojiId);
                            source.Add(customEmojiId);
                        }
                    }
                    else
                    {
                        source.Add(item);
                    }
                }

                if (customEmoji.Count > 0)
                {
                    var response = await ClientService.SendAsync(new GetCustomEmojiStickers(customEmoji));
                    if (response is Stickers customEmojiStickers)
                    {
                        foreach (var sticker in customEmojiStickers.StickersValue)
                        {
                            if (sticker.FullType is not StickerFullTypeCustomEmoji customEmojiType)
                            {
                                continue;
                            }

                            for (int i = 0; i < source.Count; i++)
                            {
                                if (source[i] is long customEmojiId && customEmojiId == customEmojiType.CustomEmojiId)
                                {
                                    source[i] = new StickerViewModel(ClientService, sticker);
                                }
                            }
                        }
                    }
                }

                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] is long)
                    {
                        source.RemoveAt(i);
                        i--;
                    }
                }

                stickers.Add(new RecentEmoji(source));
                stickers.AddRange(emojiGroups);

                StandardSets.ReplaceWith(emojiGroups);
            }

            var installedSets = _allowCustomEmoji || _mode != EmojiDrawerMode.Reactions
                ? new List<StickerSetViewModel>(await GetInstalledSets())
                : new List<StickerSetViewModel>(0);

            var sets = new List<StickerSetViewModel>(installedSets);

            if (_mode != EmojiDrawerMode.Chat)
            {
                if (_mode == EmojiDrawerMode.Reactions)
                {
                    _reactionRecentSet.Update(_recentStickers);
                    _reactionTopSet.Update(_topStickers);

                    if (_reactionRecentSet.Stickers.Count > 0)
                    {
                        sets.Insert(0, _reactionRecentSet);
                        installedSets.Insert(0, _reactionRecentSet);
                    }

                    sets.Insert(0, _reactionTopSet);

                    if (_mode is EmojiDrawerMode.EmojiStatus or EmojiDrawerMode.ChatPhoto or EmojiDrawerMode.UserPhoto or EmojiDrawerMode.EmojiStatus or EmojiDrawerMode.Topics)
                    {
                        installedSets.Insert(0, _reactionTopSet);
                    }
                }
                else
                {
                    var response = await GetDefaultStickersAsync(_mode);
                    if (response is Stickers defaultStickers)
                    {
                        _reactionTopSet.Update(defaultStickers.StickersValue);

                        sets.Insert(0, _reactionTopSet);
                        installedSets.Insert(0, _reactionTopSet);
                    }
                }
            }

            stickers.AddRange(sets);
            Items.ReplaceWith(stickers);

            InstalledSets.ReplaceWith(installedSets);
        }

        private Task<BaseObject> GetDefaultStickersAsync(EmojiDrawerMode mode)
        {
            if (mode == EmojiDrawerMode.EmojiStatus)
            {
                return GetDefaultStatusAsync();
            }

            Function func = _mode switch
            {
                EmojiDrawerMode.ChatPhoto => new GetDefaultChatPhotoCustomEmojiStickers(),
                EmojiDrawerMode.UserPhoto => new GetDefaultChatPhotoCustomEmojiStickers(),
                EmojiDrawerMode.Background => new GetDefaultBackgroundCustomEmojiStickers(),
                EmojiDrawerMode.Topics => new GetForumTopicDefaultIcons(),
                _ => null
            };

            if (func != null)
            {
                return ClientService.SendAsync(func);
            }

            return Task.FromResult<BaseObject>(null);
        }

        private async Task<BaseObject> GetDefaultStatusAsync()
        {
            var themedResponse = await ClientService.SendAsync(new GetThemedEmojiStatuses()) as EmojiStatuses;
            var recentResponse = await ClientService.SendAsync(new GetRecentEmojiStatuses()) as EmojiStatuses;
            var defaulResponse = await ClientService.SendAsync(new GetDefaultEmojiStatuses()) as EmojiStatuses;

            var themed = themedResponse?.CustomEmojiIds ?? Array.Empty<long>();
            var recent = recentResponse?.CustomEmojiIds ?? Array.Empty<long>();
            var defaul = defaulResponse?.CustomEmojiIds ?? Array.Empty<long>();

            var emoji = new List<long>();
            var delay = new List<long>();

            foreach (var status in themed.Union(recent.Union(defaul)))
            {
                if (emoji.Count < 8 * 5 - 1 && !emoji.Contains(status))
                {
                    emoji.Add(status);
                }
                else if (!delay.Contains(status))
                {
                    delay.Add(status);
                }
            }

            // TODO: why the order by???
            //if (response is Stickers stickers)
            //{
            //    _reactionTopSet.Update(stickers.StickersValue.OrderBy(x => emoji.IndexOf(x.FullType is StickerFullTypeCustomEmoji customEmoji ? customEmoji.CustomEmojiId : 0)), true);
            //}

            return await ClientService.SendAsync(new GetCustomEmojiStickers(emoji));
        }

        private async Task<IEnumerable<StickerSetViewModel>> GetInstalledSets()
        {
            if (_installedSets != null)
            {
                return _installedSets.Values;
            }

            var result1 = await ClientService.SendAsync(new GetInstalledStickerSets(new StickerTypeCustomEmoji()));
            var result2 = await ClientService.SendAsync(new GetTrendingStickerSets(new StickerTypeCustomEmoji(), 0, 100));

            if (result1 is StickerSets sets && result2 is TrendingStickerSets trending)
            {
                var installedSets = new Dictionary<long, StickerSetViewModel>();

                var filtered = sets.Sets.Where(x => _mode != EmojiDrawerMode.Background || x.NeedsRepainting).ToList();
                if (filtered.Count > 0)
                {
                    var result3 = await ClientService.SendAsync(new GetStickerSet(filtered[0].Id));
                    if (result3 is StickerSet set)
                    {
                        installedSets[set.Id] = new StickerSetViewModel(ClientService, sets.Sets[0], set);
                    }

                    for (int i = installedSets.Count; i < filtered.Count; i++)
                    {
                        installedSets[filtered[i].Id] = new StickerSetViewModel(ClientService, filtered[i]);
                    }
                }

                foreach (var item in trending.Sets)
                {
                    if (installedSets.ContainsKey(item.Id))
                    {
                        continue;
                    }

                    if (_mode == EmojiDrawerMode.Background && !item.NeedsRepainting)
                    {
                        continue;
                    }

                    installedSets[item.Id] = new StickerSetViewModel(ClientService, item);
                }

                _installedSets = installedSets;
                return installedSets.Values;
            }

            return Array.Empty<StickerSetViewModel>();
        }

        public async Task<List<(AvailableReaction, Sticker)>> UpdateReactions(AvailableReactions available)
        {
            if (available == null)
            {
                return null;
            }

            IList<AvailableReaction> source = available.TopReactions.Take(6).ToList();
            IList<AvailableReaction> additional = available.RecentReactions.Count > 0
                ? available.RecentReactions
                : available.PopularReactions;

            if (source.Count < 6)
            {
                available.TopReactions
                    .Select(x => x.Type)
                    .Discern(out var emoji, out var customEmoji);

                foreach (var item in additional)
                {
                    if (item.Type is ReactionTypeEmoji emojii
                        && emoji != null
                        && emoji.Contains(emojii.Emoji))
                    {
                        continue;
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmojii
                        && customEmoji != null
                        && customEmoji.Contains(customEmojii.CustomEmojiId))
                    {
                        continue;
                    }

                    source.Add(item);

                    if (source.Count == 6)
                    {
                        break;
                    }
                }
            }

            ContinueReactions(available, available.TopReactions, additional);

            source
                .Select(x => x.Type)
                .Discern(out var missingReactions, out var missingEmoji);

            IDictionary<long, Sticker> assets = null;
            if (missingEmoji != null)
            {
                var response = await ClientService.SendAsync(new GetCustomEmojiStickers(missingEmoji.ToArray()));
                if (response is not Stickers stickers)
                {
                    return null;
                }

                assets = stickers.StickersValue.ToDictionary(x => x.FullType is StickerFullTypeCustomEmoji customEmoji ? customEmoji.CustomEmojiId : 0);
            }

            var reactions = missingReactions != null
                ? await ClientService.GetReactionsAsync(missingReactions)
                : null;

            var visible = new List<(AvailableReaction, Sticker)>();

            void Populate(IList<AvailableReaction> source, List<(AvailableReaction, Sticker)> target)
            {
                foreach (var item in source)
                {
                    if (item.Type is ReactionTypeEmoji emoji && reactions.TryGetValue(emoji.Emoji, out EmojiReaction reaction))
                    {
                        // Some times the sticker has a different emoji
                        // and in that case reaction won't work
                        reaction.ActivateAnimation.Emoji = emoji.Emoji;

                        target.Add((item, reaction.ActivateAnimation));
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmoji && assets.TryGetValue(customEmoji.CustomEmojiId, out Sticker sticker))
                    {
                        target.Add((item, sticker));
                    }
                }
            }

            Populate(source, visible);
            return visible;
        }

        private async void ContinueReactions(AvailableReactions available, IList<AvailableReaction> source, IList<AvailableReaction> sourceRecent)
        {
            if (available == null)
            {
                return;
            }

            available.TopReactions
                .Union(available.PopularReactions)
                .Union(available.RecentReactions)
                .Select(x => x.Type)
                .Discern(out var missingReactions, out var missingEmoji);

            IDictionary<long, Sticker> assets = null;
            if (missingEmoji != null)
            {
                var response = await ClientService.SendAsync(new GetCustomEmojiStickers(missingEmoji.ToArray()));
                if (response is not Stickers stickers)
                {
                    return;
                }

                assets = stickers.StickersValue.ToDictionary(x => x.FullType is StickerFullTypeCustomEmoji customEmoji ? customEmoji.CustomEmojiId : 0);
            }

            var reactions = missingReactions != null
                ? await ClientService.GetReactionsAsync(missingReactions)
                : await ClientService.GetAllReactionsAsync();

            var top = new List<Sticker>();
            var recent = new List<Sticker>();

            void Populate(IList<AvailableReaction> source, List<Sticker> target)
            {
                foreach (var item in source)
                {
                    if (item.Type is ReactionTypeEmoji emoji && reactions.TryGetValue(emoji.Emoji, out EmojiReaction reaction))
                    {
                        // Some times the sticker has a different emoji
                        // and in that case reaction won't work
                        reaction.ActivateAnimation.Emoji = emoji.Emoji;

                        target.Add(reaction.ActivateAnimation);
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmoji && assets.TryGetValue(customEmoji.CustomEmojiId, out Sticker sticker))
                    {
                        target.Add(sticker);
                    }
                }
            }

            Populate(source, top);
            Populate(sourceRecent, recent);

            _allowCustomEmoji = available.AllowCustomEmoji;
            _reactionTopSet.Update(Enumerable.Empty<Sticker>());

            _topStickers = top;
            _recentStickers = recent;

            Update();
        }

        private bool _allowCustomEmoji;

        private List<Sticker> _topStickers;
        private List<Sticker> _recentStickers;

        //private List<StickerSetViewModel> _installedSets;
        private Dictionary<long, StickerSetViewModel> _installedSets;

        public bool TryGetInstalledSet(long id, out StickerSetViewModel value)
        {
            return _installedSets.TryGetValue(id, out value);
        }

        private int _featuredUnreadCount;
        public int FeaturedUnreadCount
        {
            get => _featuredUnreadCount;
            set => Set(ref _featuredUnreadCount, value);
        }
    }

    public class RecentEmoji
    {
        public string Title { get; }

        public bool IsInstalled { get; } = true;

        public MvxObservableCollection<object> Stickers { get; }

        public RecentEmoji(List<object> items)
        {
            Title = Strings.RecentStickers;
            Stickers = new MvxObservableCollection<object>(items);
        }
    }

    public class EmojiSetDiffHandler : IDiffHandler<object>
    {
        public bool CompareItems(object oldItem, object newItem)
        {
            if (oldItem is RecentEmoji oldRecent && newItem is RecentEmoji newRecent)
            {
                return true;
            }
            else if (oldItem is EmojiGroup oldGroup && newItem is EmojiGroup newGroup)
            {
                return oldGroup.Glyph == newGroup.Glyph;
            }
            else if (oldItem is StickerSetViewModel oldSet && newItem is StickerSetViewModel newSet)
            {
                return oldSet.Id == newSet.Id;
            }

            return false;
        }

        public void UpdateItem(object oldItem, object newItem)
        {
            if (oldItem is RecentEmoji oldRecent && newItem is RecentEmoji newRecent)
            {
                oldRecent.Stickers.ReplaceWith(newRecent.Stickers);
            }
            else if (oldItem is EmojiGroup oldGroup && newItem is EmojiGroup newGroup)
            {
                if (oldGroup.SkinTone != newGroup.SkinTone)
                {
                    oldGroup.SkinTone = newGroup.SkinTone;

                    foreach (var item in oldGroup.Stickers.OfType<EmojiSkinData>())
                    {
                        item.SetValue(newGroup.SkinTone);
                    }
                }
            }
        }
    }
}
