using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class AttachedStickersViewModel : TLViewModelBase
    {
        public AttachedStickersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<StickerSetInfo>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is int fileId)
            {
                IsLoading = true;

                var response = await ProtoService.SendAsync(new GetAttachedStickerSets(fileId));
                if (response is StickerSets sets)
                {
                    Items.ReplaceWith(sets.Sets);

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

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }
    }
}
