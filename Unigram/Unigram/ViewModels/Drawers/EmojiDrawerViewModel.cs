using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Drawers
{
    public enum EmojiDrawerMode
    {
        Chat,
        Reactions,
        CustomEmojis
    }

    public class EmojiDrawerViewModel : TLViewModelBase
    //IHandle<UpdateInstalledStickerSets>
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
                Title = Strings.Resources.RecentStickers,
                Name = "tg/recentlyUsed",
                IsInstalled = true
            });

            Subscribe();
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateInstalledStickerSets>(this, Handle);
        }

        private static readonly Dictionary<int, Dictionary<int, Dictionary<EmojiDrawerMode, EmojiDrawerViewModel>>> _windowContext = new();
        public static EmojiDrawerViewModel GetForCurrentView(int sessionId, EmojiDrawerMode mode = EmojiDrawerMode.Chat)
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out Dictionary<int, Dictionary<EmojiDrawerMode, EmojiDrawerViewModel>> reference))
            {
                if (reference.TryGetValue(sessionId, out Dictionary<EmojiDrawerMode, EmojiDrawerViewModel> value))
                {
                    if (value.TryGetValue(mode, out EmojiDrawerViewModel viewModel))
                    {
                        return viewModel;
                    }
                    else
                    {
                        var context2 = TLContainer.Current.Resolve<EmojiDrawerViewModel>();
                        value[mode] = context2;

                        context2.Mode = mode;
                        return context2;
                    }
                }
            }
            else
            {
                _windowContext[id] = new Dictionary<int, Dictionary<EmojiDrawerMode, EmojiDrawerViewModel>>();
            }

            var context = TLContainer.Current.Resolve<EmojiDrawerViewModel>();
            _windowContext[id][sessionId] = new Dictionary<EmojiDrawerMode, EmojiDrawerViewModel>
            {
                { mode, context }
            };

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
            if (update.StickerType is not StickerTypeRegular)
            {
                return;
            }

            long hash = 0;
            foreach (var elem in update.StickerSetIds)
            {
                hash = ((hash * 20261) + 0x80000000L + elem) % 0x80000000L;
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

        public async void Update()
        {
            _ = UpdateAsync();
        }

        public async Task UpdateAsync()
        {
            if (_updated && _mode == EmojiDrawerMode.Chat)
            {
                return;
            }

            _updated = true;

            var stickers = new List<object>();

            if (_mode == EmojiDrawerMode.Chat)
            {
                var recents = Emoji.GetRecents(EmojiSkinTone.Default);
                var emojiGroups = Emoji.Get(EmojiSkinTone.Default, true);

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
                            for (int i = 0; i < source.Count; i++)
                            {
                                if (source[i] is long customEmojiId && customEmojiId == sticker.CustomEmojiId)
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
                _reactionRecentSet.Update(_recentStickers);
                _reactionTopSet.Update(_topStickers);

                if (_reactionRecentSet.Stickers.Count > 0)
                {
                    sets.Insert(0, _reactionRecentSet);
                    installedSets.Insert(0, _reactionRecentSet);
                }

                sets.Insert(0, _reactionTopSet);

                if (_mode == EmojiDrawerMode.CustomEmojis)
                {
                    installedSets.Insert(0, _reactionTopSet);
                }
            }

            stickers.AddRange(sets);
            Items.ReplaceWith(stickers);

            InstalledSets.ReplaceWith(installedSets);
        }

        private async Task<IList<StickerSetViewModel>> GetInstalledSets()
        {
            if (_installedSets != null)
            {
                return _installedSets;
            }

            var result1 = await ClientService.SendAsync(new GetInstalledStickerSets(new StickerTypeCustomEmoji()));
            var result2 = await ClientService.SendAsync(new GetTrendingStickerSets(new StickerTypeCustomEmoji(), 0, 100));

            if (result1 is StickerSets sets && result2 is TrendingStickerSets trending)
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

                    var existing = installedSets.Select(x => x.Id).ToArray();

                    foreach (var item in trending.Sets)
                    {
                        if (existing.Contains(item.Id))
                        {
                            continue;
                        }

                        installedSets.Add(new StickerSetViewModel(ClientService, item));
                    }
                }
                else if (trending.Sets.Count > 0)
                {
                    installedSets.AddRange(trending.Sets.Select(x => new StickerSetViewModel(ClientService, x)));
                }

                _installedSets = installedSets;
                return installedSets;
            }

            return Array.Empty<StickerSetViewModel>();
        }

        public async Task UpdateReactions(AvailableReactions available, IList<AvailableReaction> visible)
        {
            available.TopReactions
                .Union(available.PopularReactions)
                .Union(available.RecentReactions)
                .Select(x => x.Type)
                .Discern(out _, out var missingEmoji);

            IDictionary<long, Sticker> assets = null;
            if (missingEmoji != null)
            {
                var response = await ClientService.SendAsync(new GetCustomEmojiStickers(missingEmoji.ToArray()));
                if (response is not Stickers stickers)
                {
                    return;
                }

                assets = stickers.StickersValue.ToDictionary(x => x.CustomEmojiId);
            }

            var reactions = await ClientService.GetAllReactionsAsync();

            var items = new List<Sticker>();
            var top = new List<Sticker>();
            var recent = new List<Sticker>();

            void Populate(IList<AvailableReaction> source, List<Sticker> target)
            {
                foreach (var item in source)
                {
                    if (item.Type is ReactionTypeEmoji emoji && reactions.TryGetValue(emoji.Emoji, out EmojiReaction reaction))
                    {
                        target.Add(reaction.ActivateAnimation);
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmoji && assets.TryGetValue(customEmoji.CustomEmojiId, out Sticker sticker))
                    {
                        target.Add(sticker);
                    }
                }
            }

            if (visible != null)
            {
                Populate(visible, items);
            }

            Populate(available.TopReactions, top);

            if (available.PopularReactions.Count > 0)
            {
                Populate(available.PopularReactions, recent);
            }
            else
            {
                Populate(available.RecentReactions, recent);
            }

            _allowCustomEmoji = available.AllowCustomEmoji;
            _reactionTopSet.Update(items);

            _topStickers = top;
            _recentStickers = recent;

            if (visible != null)
            {
                Items.ReplaceWith(new[] { _reactionTopSet });
            }
            else
            {
                _ = UpdateAsync();
            }
        }

        private bool _allowCustomEmoji;

        private List<Sticker> _topStickers;
        private List<Sticker> _recentStickers;

        private List<StickerSetViewModel> _installedSets;

        public async void UpdateStatuses()
        {
            _ = UpdateAsync();

            var themedResponse = await ClientService.SendAsync(new GetThemedEmojiStatuses()) as EmojiStatuses;
            var recentResponse = await ClientService.SendAsync(new GetRecentEmojiStatuses()) as EmojiStatuses;
            var defaulResponse = await ClientService.SendAsync(new GetDefaultEmojiStatuses()) as EmojiStatuses;

            var themed = themedResponse?.EmojiStatusesValue ?? Array.Empty<EmojiStatus>();
            var recent = recentResponse?.EmojiStatusesValue ?? Array.Empty<EmojiStatus>();
            var defaul = defaulResponse?.EmojiStatusesValue ?? Array.Empty<EmojiStatus>();

            var emoji = new List<long>();
            var delay = new List<long>();

            var i = 0;

            foreach (var status in themed.Union(recent.Union(defaul)))
            {
                if (emoji.Count < 8 * 5 - 1 && !emoji.Contains(status.CustomEmojiId))
                {
                    emoji.Add(status.CustomEmojiId);
                }
                else if (!delay.Contains(status.CustomEmojiId))
                {
                    delay.Add(status.CustomEmojiId);
                }

                i++;
            }

            var response = await ClientService.SendAsync(new GetCustomEmojiStickers(emoji));
            if (response is Stickers stickers)
            {
                _reactionTopSet.Update(stickers.StickersValue.OrderBy(x => emoji.IndexOf(x.CustomEmojiId)), true);
            }
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
            Title = Strings.Resources.RecentStickers;
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
