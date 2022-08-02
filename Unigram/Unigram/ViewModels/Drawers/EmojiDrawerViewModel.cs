using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.Text.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Drawers
{
    public class EmojiDrawerViewModel : TLViewModelBase, IHandle<UpdateInstalledStickerSets>
    {
        private bool _updated;

        public EmojiDrawerViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            //Items = new DiffObservableCollection<object>(new EmojiSetDiffHandler());
            Items = new MvxObservableCollection<object>();

            Aggregator.Subscribe(this);
        }

        private static readonly Dictionary<int, Dictionary<int, EmojiDrawerViewModel>> _windowContext = new Dictionary<int, Dictionary<int, EmojiDrawerViewModel>>();
        public static EmojiDrawerViewModel GetForCurrentView(int sessionId)
        {
            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out Dictionary<int, EmojiDrawerViewModel> reference))
            {
                if (reference.TryGetValue(sessionId, out EmojiDrawerViewModel value))
                {
                    return value;
                }
            }
            else
            {
                _windowContext[id] = new Dictionary<int, EmojiDrawerViewModel>();
            }

            var context = TLContainer.Current.Resolve<EmojiDrawerViewModel>();
            _windowContext[id][sessionId] = context;

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
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ProtoService, sticker));
                    }
                    else if (index == -1)
                    {
                        destination.Insert(Math.Min(i, destination.Count), new StickerViewModel(ProtoService, sticker));
                    }
                }
            }
            else
            {
                destination.Clear();
                destination.AddRange(origin.Select(x => new StickerViewModel(ProtoService, x)));
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

        //public MvxObservableCollection<StickerSetViewModel> Stickers => SearchStickers ?? (MvxObservableCollection<StickerSetViewModel>)SavedStickers;

        public async void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchStickers = null;
            }
            else
            {
                var items = SearchStickers = new SearchStickerSetsCollection(ProtoService, Aggregator, new StickerTypeRegular(), query, CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
                await items.LoadMoreItemsAsync(0);
            }
        }

        public async void Update()
        {
            if (_updated)
            {
                return;
            }

            _updated = true;

            var result2 = await ProtoService.SendAsync(new GetTrendingStickerSets(new StickerTypeCustomEmoji(), 0, 100));
            var result3 = await ProtoService.SendAsync(new GetInstalledStickerSets(new StickerTypeCustomEmoji()));
            if (result3 is StickerSets sets)
            {
                //for (int i = 0; i < favorite.StickersValue.Count; i++)
                //{
                //    var favSticker = favorite.StickersValue[i];
                //    for (int j = 0; j < recent.StickersValue.Count; j++)
                //    {
                //        var recSticker = recent.StickersValue[j];
                //        if (recSticker.StickerValue.Id == favSticker.StickerValue.Id)
                //        {
                //            recent.StickersValue.Remove(recSticker);
                //            break;
                //        }
                //    }
                //}

                //for (int i = 20; i < recent.StickersValue.Count; i++)
                //{
                //    recent.StickersValue.RemoveAt(20);
                //    i--;
                //}

                //_recentSet.Update(recent.StickersValue);

                var recents = Emoji.GetRecents(EmojiSkinTone.Default);
                var emojiGroups = Emoji.Get(EmojiSkinTone.Default, true);

                var stickers = new List<object>(emojiGroups);

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
                    var response = await ProtoService.SendAsync(new GetCustomEmojiStickers(customEmoji));
                    if (response is Stickers customEmojiStickers)
                    {
                        foreach (var sticker in customEmojiStickers.StickersValue)
                        {
                            for (int i = 0; i < source.Count; i++)
                            {
                                if (source[i] is long customEmojiId && customEmojiId == sticker.CustomEmojiId)
                                {
                                    source[i] = new StickerViewModel(ProtoService, sticker);
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

                stickers.Insert(0, new RecentEmoji(source));

                var installedSet = new List<StickerSetViewModel>();

                if (sets.Sets.Count > 0)
                {
                    var result4 = await ProtoService.SendAsync(new GetStickerSet(sets.Sets[0].Id));
                    if (result4 is StickerSet set)
                    {
                        installedSet.Add(new StickerSetViewModel(ProtoService, sets.Sets[0], set));
                        installedSet.AddRange(sets.Sets.Skip(1).Select(x => new StickerSetViewModel(ProtoService, x)));
                    }
                    else
                    {
                        installedSet.AddRange(sets.Sets.Select(x => new StickerSetViewModel(ProtoService, x)));
                    }
                }
                else
                {
                    installedSet.AddRange(sets.Sets.Select(x => new StickerSetViewModel(ProtoService, x)));
                }

                stickers.AddRange(installedSet);
                Items.ReplaceWith(stickers);

                StandardSets.ReplaceWith(emojiGroups);
                InstalledSets.ReplaceWith(installedSet);
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
