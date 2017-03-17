using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : UnigramViewModelBase
    {
        public StickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLDocument>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputStickerSetBase set)
            {
                var response = await ProtoService.GetStickerSetAsync(set);
                if (response.IsSucceeded)
                {
                    StickerSet = response.Result.Set;
                    Items.AddRange(response.Result.Documents.OfType<TLDocument>(), true);
                }
            }
        }

        private TLStickerSet _stickerSet = new TLStickerSet();
        public TLStickerSet StickerSet
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

        public ObservableCollection<TLDocument> Items { get; private set; }
    }
}
