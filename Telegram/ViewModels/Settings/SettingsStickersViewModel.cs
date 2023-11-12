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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public enum StickersType
    {
        Installed,
        Archived,
        Trending,
        Masks,
        MasksArchived,
        Emoji,
        EmojiArchived
    }

    public class SettingsStickersViewModel : ViewModelBase, IHandle
    {
        private StickersType _type;

        private bool _needReorder;
        private IList<long> _newOrder;

        public SettingsStickersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<StickerSetInfo>(new StickerSetInfoDiffHandler());

            //StickerSetShareCommand = new RelayCommand<StickerSetInfo>(StickerSetShareExecute);
            //StickerSetCopyCommand = new RelayCommand<StickerSetInfo>(StickerSetCopyExecute);

            var random = new Random();
            EmojiStyleIcon = _emojiStyleIcons[random.Next(0, _emojiStyleIcons.Length)];
        }

        private class StickerSetInfoDiffHandler : IDiffHandler<StickerSetInfo>
        {
            public bool CompareItems(StickerSetInfo oldItem, StickerSetInfo newItem)
            {
                return oldItem.Id == newItem.Id;
            }

            public void UpdateItem(StickerSetInfo oldItem, StickerSetInfo newItem)
            {
                //
            }
        }

        public StickersType Type => _type;

        public string Title => _type switch
        {
            StickersType.Installed => Strings.StickersName,
            StickersType.Archived => Strings.ArchivedStickers,
            StickersType.Trending => Strings.FeaturedStickers,
            StickersType.Masks => Strings.Masks,
            StickersType.MasksArchived => Strings.ArchivedMasks,
            StickersType.Emoji => Strings.Emoji,
            StickersType.EmojiArchived => Strings.ArchivedEmojiPacks,
            _ => Strings.StickersName
        };

        public StickerType StickerType => _type switch
        {
            StickersType.Masks => new StickerTypeMask(),
            StickersType.MasksArchived => new StickerTypeMask(),
            StickersType.Emoji => new StickerTypeCustomEmoji(),
            StickersType.EmojiArchived => new StickerTypeCustomEmoji(),
            _ => new StickerTypeRegular()
        };

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is int flags)
            {
                _type = (StickersType)flags;
            }

            if (_type is StickersType.Installed or StickersType.Masks or StickersType.Emoji)
            {
                Items = new ItemsCollection(ClientService, StickerType);

                ClientService.Send(new GetArchivedStickerSets(StickerType, 0, 1), result =>
                {
                    if (result is StickerSets stickerSets)
                    {
                        BeginOnUIThread(() => ArchivedStickersCount = stickerSets.TotalCount);
                    }
                });

                ClientService.Send(new GetTrendingStickerSets(StickerType, 0, 1), result =>
                {
                    if (result is StickerSets stickerSets)
                    {
                        BeginOnUIThread(() => FeaturedStickersCount = stickerSets.TotalCount);
                    }
                });
            }
            else if (_type is StickersType.Archived or StickersType.MasksArchived or StickersType.EmojiArchived)
            {
                Items = new ArchivedCollection(ClientService, StickerType);
            }
            else if (_type == StickersType.Trending)
            {
                Items = new TrendingCollection(ClientService, StickerType);
            }

            return Task.CompletedTask;
        }

        protected override void OnNavigatedFrom(NavigationState pageState, bool suspending)
        {
            if (_type is StickersType.Installed or StickersType.Masks or StickersType.Emoji)
            {
                if (_needReorder && _newOrder.Count > 0)
                {
                    _needReorder = false;
                    ClientService.Send(new ReorderInstalledStickerSets(StickerType, _newOrder));
                }
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateInstalledStickerSets>(this, Handle)
                .Subscribe<UpdateTrendingStickerSets>(Handle)
                .Subscribe<UpdateRecentStickers>(Handle);
        }

        public async void Handle(UpdateInstalledStickerSets update)
        {
            if (_type is not StickersType.Installed and not StickersType.Masks and not StickersType.Emoji)
            {
                return;
            }

            if (StickerType.GetType() != update.StickerType.GetType())
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetInstalledStickerSets(StickerType));
            if (response is StickerSets stickerSets)
            {
                if (_type is StickersType.Installed or StickersType.Masks)
                {
                    var union = await ClientService.SendAsync(new GetRecentStickers(_type == StickersType.Masks));
                    if (union is Stickers recents && recents.StickersValue.Count > 0)
                    {
                        BeginOnUIThread(() => Items.ReplaceDiff(new[] { new StickerSetInfo(0, Strings.RecentStickers, "tg/recentlyUsed", null, Array.Empty<ClosedVectorPath>(), false, false, false, null, StickerType, false, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
                    }
                    else
                    {
                        BeginOnUIThread(() => Items.ReplaceDiff(stickerSets.Sets));
                    }
                }
                else
                {
                    BeginOnUIThread(() => Items.ReplaceDiff(stickerSets.Sets));
                }
            }
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

            ClientService.Send(new GetInstalledStickerSets(StickerType), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    ClientService.Send(new GetRecentStickers(_type == StickersType.Masks), resultRecent =>
                    {
                        if (resultRecent is Stickers recents && recents.StickersValue.Count > 0)
                        {
                            BeginOnUIThread(() => Items.ReplaceDiff(new[] { new StickerSetInfo(0, Strings.RecentStickers, "tg/recentlyUsed", null, Array.Empty<ClosedVectorPath>(), false, false, false, null, StickerType, false, false, recents.StickersValue.Count, recents.StickersValue) }.Union(stickerSets.Sets)));
                        }
                        else
                        {
                            BeginOnUIThread(() => Items.ReplaceDiff(stickerSets.Sets));
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

        public DiffObservableCollection<StickerSetInfo> Items { get; private set; }

        public void Reorder(StickerSetInfo set)
        {
            _needReorder = true;
            _newOrder = Items.Where(x => x.Id != 0).Select(x => x.Id).ToList();

            DynamicPackOrder = false;
        }

        public bool SuggestCustomEmoji
        {
            get => Settings.Stickers.SuggestCustomEmoji;
            set
            {
                Settings.Stickers.SuggestCustomEmoji = value;
                RaisePropertyChanged();
            }
        }

        public bool LargeEmoji
        {
            get => !ClientService.Options.DisableAnimatedEmoji;
            set
            {
                ClientService.Options.DisableAnimatedEmoji = !value;
                RaisePropertyChanged();
            }
        }

        public bool DynamicPackOrder
        {
            get => Settings.Stickers.DynamicPackOrder;
            set
            {
                Settings.Stickers.DynamicPackOrder = value;
                RaisePropertyChanged();
            }
        }

        private readonly string[] _emojiStyleIcons = new[] { "\U0001F603", "\U0001F604", "\U0001F601", "\U0001F607", "\U0001F60E", "\U0001F913", "\U0001F929", "\U0001F973", "\U0001F920" };
        public string EmojiStyleIcon { get; }

        public int EmojiStyle
        {
            get => Array.IndexOf(_emojiStyleIndexer, Settings.Appearance.EmojiSet);
            set
            {
                if (value >= 0 && value < _emojiStyleIndexer.Length && Settings.Appearance.EmojiSet != _emojiStyleIndexer[value])
                {
                    switch (_emojiStyleIndexer[value])
                    {
                        case "microsoft":
                            Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"XamlAutoFontFamily");
                            break;
                        case "apple":
                        default:
                            Theme.Current["EmojiThemeFontFamily"] = new FontFamily($"ms-appx:///Assets/Emoji/apple.ttf#Segoe UI Emoji");
                            break;
                    }

                    SettingsService.Current.Appearance.EmojiSet = _emojiStyleIndexer[value];
                    SettingsService.Current.Appearance.UpdateNightMode(true);

                    RaisePropertyChanged();
                }
            }
        }

        private readonly string[] _emojiStyleIndexer = new[]
        {
            "apple",
            "microsoft"
        };

        public List<SettingsOptionItem<string>> EmojiStyleOptions { get; } = new()
        {
            new SettingsOptionItem<string>("apple", "Apple"),
            new SettingsOptionItem<string>("microsoft", "Microsoft"),
        };

        public int SuggestStickers
        {
            get => Array.IndexOf(_suggestStickersIndexer, Settings.Stickers.SuggestionMode);
            set
            {
                if (value >= 0 && value < _suggestStickersIndexer.Length && Settings.Stickers.SuggestionMode != _suggestStickersIndexer[value])
                {
                    Settings.Stickers.SuggestionMode = _suggestStickersIndexer[value];
                    RaisePropertyChanged();
                }
            }
        }

        private readonly StickersSuggestionMode[] _suggestStickersIndexer = new[]
        {
            StickersSuggestionMode.All,
            StickersSuggestionMode.Installed,
            StickersSuggestionMode.None
        };

        public List<SettingsOptionItem<StickersSuggestionMode>> SuggestStickersOptions { get; } = new()
        {
            new SettingsOptionItem<StickersSuggestionMode>(StickersSuggestionMode.All, Strings.SuggestStickersAll),
            new SettingsOptionItem<StickersSuggestionMode>(StickersSuggestionMode.Installed, Strings.SuggestStickersInstalled),
            new SettingsOptionItem<StickersSuggestionMode>(StickersSuggestionMode.None, Strings.SuggestStickersNone),
        };

        #region Context menu

        public async void Open(StickerSetInfo stickerSet)
        {
            if (stickerSet.Name.Equals("tg/recentlyUsed"))
            {
                var confirm = await ShowPopupAsync(Strings.ClearRecentEmoji, Strings.AppName, Strings.OK, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                Items.Remove(stickerSet);
                ClientService.Send(new ClearRecentStickers(_type == StickersType.Masks));
            }
            else
            {
                await StickersPopup.ShowAsync(stickerSet.Id);
            }
        }

        public async void Archive(StickerSetInfo stickerSet)
        {
            await ClientService.SendAsync(new ChangeStickerSet(stickerSet.Id, false, true));
            ClientService.Send(new GetArchivedStickerSets(StickerType, 0, 1), result =>
            {
                if (result is StickerSets stickerSets)
                {
                    BeginOnUIThread(() => ArchivedStickersCount = stickerSets.TotalCount);
                }
            });
        }

        public void Remove(StickerSetInfo stickerSet)
        {
            ClientService.Send(new ChangeStickerSet(stickerSet.Id, false, false));
        }

        #endregion

        public void OpenFeaturedStickers()
        {
            Open(StickersType.Trending);
        }

        public void OpenArchivedStickers()
        {
            Open(StickersType.Archived);
        }

        public void OpenArchivedMasks()
        {
            Open(StickersType.MasksArchived);
        }

        public void OpenMasks()
        {
            Open(StickersType.Masks);
        }

        public void OpenEmoji()
        {
            Open(StickersType.Emoji);
        }

        public void OpenArchivedEmoji()
        {
            Open(StickersType.EmojiArchived);
        }

        private void Open(StickersType type)
        {
            NavigationService.Navigate(typeof(SettingsStickersPage), (int)type);
        }

        public void OpenReaction()
        {
            NavigationService.Navigate(typeof(SettingsQuickReactionPage));
        }

        public class ItemsCollection : DiffObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly StickerType _type;

            private bool _hasMoreItems = true;

            public ItemsCollection(IClientService clientService, StickerType type)
                : base(new StickerSetInfoDiffHandler())
            {
                _clientService = clientService;
                _type = type;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    if (_type is StickerTypeRegular or StickerTypeMask)
                    {
                        var recentResponse = await _clientService.SendAsync(new GetRecentStickers(_type is StickerTypeMask));
                        if (recentResponse is Stickers stickers && stickers.StickersValue.Count > 0)
                        {
                            Add(new StickerSetInfo(0, Strings.RecentStickers, "tg/recentlyUsed", null, Array.Empty<ClosedVectorPath>(), false, false, false, null, _type, false, false, stickers.StickersValue.Count, stickers.StickersValue));
                        }
                    }

                    var response = await _clientService.SendAsync(new GetInstalledStickerSets(_type));
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

        public class ArchivedCollection : DiffObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly StickerType _type;

            private bool _hasMoreItems = true;

            public ArchivedCollection(IClientService clientService, StickerType type)
                : base(new StickerSetInfoDiffHandler())
            {
                _clientService = clientService;
                _type = type;
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

                    var response = await _clientService.SendAsync(new GetArchivedStickerSets(_type, offset, 20));
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

        public class TrendingCollection : DiffObservableCollection<StickerSetInfo>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly StickerType _type;

            private bool _hasMoreItems = true;

            public TrendingCollection(IClientService clientService, StickerType type)
                : base(new StickerSetInfoDiffHandler())
            {
                _clientService = clientService;
                _type = type;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _clientService.SendAsync(new GetTrendingStickerSets(_type, Count, 20));
                    if (response is TrendingStickerSets stickerSets)
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
