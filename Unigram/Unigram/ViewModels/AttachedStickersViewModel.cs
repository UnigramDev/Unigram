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
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class AttachedStickersViewModel : UnigramViewModelBase
    {
        private readonly IStickersService _stickersService;

        public AttachedStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IStickersService stickersService)
            : base(protoService, cacheService, aggregator)
        {
            _stickersService = stickersService;

            Items = new ObservableCollection<TLStickerSetMultiCovered>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLInputStickeredMediaBase stickeredMedia)
            {
                IsLoading = true;

                var response = await ProtoService.GetAttachedStickersAsync(stickeredMedia);
                if (response.IsSucceeded)
                {
                    Items.Clear();
                    Items.AddRange(response.Result.Select(
                        set => 
                        {
                            if (set is TLStickerSetCovered covered)
                            {
                                return new TLStickerSetMultiCovered { Set = covered.Set, Covers = new TLVector<TLDocumentBase> { covered.Cover } };
                            }

                            return set as TLStickerSetMultiCovered;
                        }));

                    IsLoading = false;
                }
                else
                {
                    //StickerSet = new TLStickerSet();
                    //Items.Clear();
                    //Items.Add(new TLMessagesStickerSet { Set = new TLStickerSet { Title = "Sticker pack not found." } });

                    //IsLoading = false;
                }
            }
        }

        public ObservableCollection<TLStickerSetMultiCovered> Items { get; private set; }
    }
}
