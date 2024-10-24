//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public partial class StickersViewModel : ViewModelBase
    {
        public StickersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Drawers.StickerSetViewModel>();
            ItemsSource = new CollectionViewSource
            {
                Source = Items,
                IsSourceGrouped = true,
                ItemsPath = new Windows.UI.Xaml.PropertyPath("Stickers")
            };
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsLoading = true;

            if (parameter is long setId)
            {
                UpdateStickerSet(await ClientService.SendAsync(new GetStickerSet(setId)));
            }
            else if (parameter is string name)
            {
                UpdateStickerSet(await ClientService.SendAsync(new SearchStickerSet(name, false)));
            }
            else if (parameter is StickerSet stickerSet)
            {
                UpdateStickerSet(stickerSet);
            }
            else if (parameter is StickerSets stickerSets)
            {
                UpdateStickerSets(stickerSets);
            }
            else if (parameter is InputFileId inputFile)
            {
                UpdateStickerSets(await ClientService.SendAsync(new GetAttachedStickerSets(inputFile.Id)));
            }
            else if (parameter is HashSet<long> ids)
            {
                var sets = new List<StickerSet>();

                foreach (var id in ids)
                {
                    var response = await ClientService.SendAsync(new GetStickerSet(id));
                    if (response is StickerSet set)
                    {
                        sets.Add(set);
                    }
                }

                if (sets.Count > 0)
                {
                    IsLoading = false;

                    Items.ReplaceWith(sets.Select(x => new Drawers.StickerSetViewModel(ClientService, x)));

                    Title = sets.Count > 1 ? Strings.Emoji : sets[0].Title;
                    IsInstalled = sets.All(x => x.IsInstalled);
                    IsArchived = sets.All(x => x.IsArchived);
                    IsOfficial = sets.All(x => x.IsOfficial);
                    Count = sets.Sum(x => x.Stickers.Count);
                    StickerType = sets[0].StickerType;
                }
                else
                {
                    RaisePropertyChanged("STICKERSET_INVALID");
                }
            }

            RaisePropertyChanged(nameof(ItemsView));
        }

        private async void UpdateStickerSets(object responses)
        {
            if (responses is StickerSets setss && setss.Sets.Count > 0)
            {
                var sets = new List<StickerSet>();

                foreach (var id in setss.Sets)
                {
                    var response = await ClientService.SendAsync(new GetStickerSet(id.Id));
                    if (response is StickerSet set)
                    {
                        sets.Add(set);
                    }
                }

                if (sets.Count > 0)
                {
                    IsLoading = false;

                    Items.ReplaceWith(sets.Select(x => new Drawers.StickerSetViewModel(ClientService, x)));

                    Title = sets.Count > 1 ? Strings.Emoji : sets[0].Title;
                    IsInstalled = sets.All(x => x.IsInstalled);
                    IsArchived = sets.All(x => x.IsArchived);
                    IsOfficial = sets.All(x => x.IsOfficial);
                    Count = sets.Sum(x => x.Stickers.Count);
                    StickerType = sets[0].StickerType;
                }
                else
                {
                    RaisePropertyChanged("STICKERSET_INVALID");
                }
            }
            else
            {
                RaisePropertyChanged("STICKERSET_INVALID");
            }
        }

        private void UpdateStickerSet(object response)
        {
            if (response is StickerSet stickerSet)
            {
                IsLoading = false;

                Items.ReplaceWith(new[] { new Drawers.StickerSetViewModel(ClientService, stickerSet) });

                Title = stickerSet.Title;
                IsInstalled = stickerSet.IsInstalled;
                IsArchived = stickerSet.IsArchived;
                IsOfficial = stickerSet.IsOfficial;
                Count = stickerSet.Stickers.Count;
                StickerType = stickerSet.StickerType;
            }
            else
            {
                RaisePropertyChanged("STICKERSET_INVALID");
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private bool _installed;
        public bool IsInstalled
        {
            get => _installed;
            set => Set(ref _installed, value);
        }

        private bool _archived;
        public bool IsArchived
        {
            get => _archived;
            set => Set(ref _archived, value);
        }

        private bool _official;
        public bool IsOfficial
        {
            get => _official;
            set => Set(ref _official, value);
        }

        private int _count;
        public int Count
        {
            get => _count;
            set => Set(ref _count, value);
        }

        private StickerType _stickerType;
        public StickerType StickerType
        {
            get => _stickerType;
            set => Set(ref _stickerType, value);
        }

        public MvxObservableCollection<Drawers.StickerSetViewModel> Items { get; private set; }
        public CollectionViewSource ItemsSource { get; }

        public object ItemsView => Items.Count == 1 ? Items[0] : ItemsSource.View;

        public void Execute()
        {
            Sticker thumbnail = null;

            foreach (var set in Items)
            {
                if (IsInstalled && set.IsInstalled)
                {
                    thumbnail ??= set.GetThumbnail();
                    ClientService.Send(new ChangeStickerSet(set.Id, set.IsOfficial, set.IsOfficial));
                }
                else if (!IsInstalled && !set.IsInstalled)
                {
                    thumbnail ??= set.GetThumbnail();
                    ClientService.Send(new ChangeStickerSet(set.Id, true, false));
                }
            }

            var title = IsInstalled
                ? StickerType is StickerTypeCustomEmoji
                ? Strings.EmojiRemoved
                : Strings.StickersRemoved
                : StickerType is StickerTypeCustomEmoji
                ? Strings.AddEmojiInstalled
                : Strings.AddStickersInstalled;

            var message = IsInstalled
                ? StickerType is StickerTypeCustomEmoji
                ? Items.Count > 1
                ? Locale.Declension(Strings.R.EmojiRemovedMultipleInfo, Items.Count(x => x.IsInstalled))
                : string.Format(Strings.EmojiRemovedInfo, Title)
                : string.Format(Strings.StickersRemovedInfo, Title)
                : StickerType is StickerTypeCustomEmoji
                ? Items.Count > 1
                ? Locale.Declension(Strings.R.AddEmojiMultipleInstalledInfo, Items.Count(x => !x.IsInstalled))
                : string.Format(Strings.AddEmojiInstalledInfo, Title)
                : string.Format(Strings.AddStickersInstalledInfo, Title);

            ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", title, message), DelayedFileSource.FromSticker(ClientService, thumbnail));
        }
    }
}
