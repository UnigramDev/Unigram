using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickersViewModel : TLViewModelBase
    {
        public StickersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Drawers.StickerSetViewModel>();
            ItemsSource = new CollectionViewSource
            {
                Source = Items,
                IsSourceGrouped = true,
                ItemsPath = new Windows.UI.Xaml.PropertyPath("Stickers")
            };

            SendCommand = new RelayCommand(SendExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsLoading = true;

            if (parameter is long setId)
            {
                UpdateStickerSet(await ProtoService.SendAsync(new GetStickerSet(setId)));
            }
            else if (parameter is string name)
            {
                UpdateStickerSet(await ProtoService.SendAsync(new SearchStickerSet(name)));
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
                UpdateStickerSets(await ProtoService.SendAsync(new GetAttachedStickerSets(inputFile.Id)));
            }
            else if (parameter is HashSet<long> ids)
            {
                var sets = new List<StickerSet>();

                foreach (var id in ids)
                {
                    var response = await ProtoService.SendAsync(new GetStickerSet(id));
                    if (response is StickerSet set)
                    {
                        sets.Add(set);
                    }
                }

                if (sets.Count > 0)
                {
                    IsLoading = false;

                    Title = Strings.Resources.Emoji;
                    IsInstalled = sets.All(x => x.IsInstalled);
                    IsArchived = sets.All(x => x.IsArchived);
                    IsOfficial = sets.All(x => x.IsOfficial);
                    Count = sets.Sum(x => x.Stickers.Count);
                    StickerType = sets[0].StickerType;

                    Items.ReplaceWith(sets.Select(x => new Drawers.StickerSetViewModel(ProtoService, x)));
                }
                else
                {
                    Title = "Sticker pack not found.";
                    Items.Clear();
                }
            }

            RaisePropertyChanged(nameof(ItemsView));
        }

        private void UpdateStickerSets(object response)
        {
            if (response is StickerSets sets && sets.Sets.Count > 0)
            {
                IsLoading = false;

                Title = Strings.Resources.Emoji;
                IsInstalled = sets.Sets.All(x => x.IsInstalled);
                IsArchived = sets.Sets.All(x => x.IsArchived);
                IsOfficial = sets.Sets.All(x => x.IsOfficial);
                Count = sets.Sets.Sum(x => x.Size);
                StickerType = sets.Sets[0].StickerType;

                Items.ReplaceWith(sets.Sets.Select(x => new Drawers.StickerSetViewModel(ProtoService, x)));
            }
            else
            {
                Title = "Sticker pack not found.";
                Items.Clear();
            }
        }

        private void UpdateStickerSet(object response)
        {
            if (response is StickerSet stickerSet)
            {
                IsLoading = false;

                Title = stickerSet.Title;
                IsInstalled = stickerSet.IsInstalled;
                IsArchived = stickerSet.IsArchived;
                IsOfficial = stickerSet.IsOfficial;
                Count = stickerSet.Stickers.Count;
                StickerType = stickerSet.StickerType;

                Items.ReplaceWith(new[] { new Drawers.StickerSetViewModel(ProtoService, stickerSet) });
            }
            else
            {
                Title = "Sticker pack not found.";
                Items.Clear();
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

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            IsLoading = true;

            foreach (var set in Items)
            {
                if (IsInstalled && set.IsInstalled)
                {
                    ProtoService.Send(new ChangeStickerSet(set.Id, set.IsOfficial, set.IsOfficial));
                }
                else if (!IsInstalled && !set.IsInstalled)
                {
                    ProtoService.Send(new ChangeStickerSet(set.Id, true, false));
                }
            }

            //NavigationService.GoBack();
        }
    }
}
