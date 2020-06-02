using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : TLViewModelBase
    {
        public StickerSetViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Sticker>();

            SendCommand = new RelayCommand(SendExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            IsLoading = true;

            if (parameter is long setId)
            {
                var response = await ProtoService.SendAsync(new GetStickerSet(setId));
                if (response is StickerSet stickerSet)
                {
                    IsLoading = false;

                    StickerSet = stickerSet;
                    Items.ReplaceWith(stickerSet.Stickers);

                }
                else
                {
                    StickerSet = new StickerSet { Title = "Sticker pack not found." };
                    Items.Clear();
                }
            }
            else if (parameter is string name)
            {
                var response = await ProtoService.SendAsync(new SearchStickerSet(name));
                if (response is StickerSet stickerSet)
                {
                    IsLoading = false;

                    StickerSet = stickerSet;
                    Items.ReplaceWith(stickerSet.Stickers);

                }
                else
                {
                    StickerSet = new StickerSet { Title = "Sticker pack not found." };
                    Items.Clear();
                }
            }
        }

        private StickerSet _stickerSet = new StickerSet();
        public StickerSet StickerSet
        {
            get
            {
                return _stickerSet;
            }
            set
            {
                Set(ref _stickerSet, value);
            }
        }

        public MvxObservableCollection<Sticker> Items { get; private set; }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            IsLoading = true;

            var set = _stickerSet;
            if (set == null)
            {
                return;
            }

            if (set.IsInstalled)
            {
                ProtoService.Send(new ChangeStickerSet(set.Id, set.IsOfficial, set.IsOfficial));
            }
            else
            {
                ProtoService.Send(new ChangeStickerSet(set.Id, true, false));
            }

            //NavigationService.GoBack();
        }
    }
}
