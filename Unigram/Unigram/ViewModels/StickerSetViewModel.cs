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
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class StickerSetViewModel : UnigramViewModelBase
    {
        public StickerSetViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new List<KeyedList<TLStickerSet, TLDocument>>();
            Items.Add(new KeyedList<TLStickerSet, TLDocument>((TLStickerSet)null));
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputStickerSetBase set)
            {
                IsLoading = true;

                var response = await ProtoService.GetStickerSetAsync(set);
                if (response.IsSucceeded)
                {
                    StickerSet = response.Result.Set;
                    Items[0].Key = response.Result.Set;
                    Items[0].AddRange(response.Result.Documents.OfType<TLDocument>(), true);

                    IsLoading = false;
                }
            }
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                Set(ref _isLoading, value);
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

        public List<KeyedList<TLStickerSet, TLDocument>> Items { get; private set; }
    }
}
