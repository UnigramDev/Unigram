using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsFeaturedStickersViewModel : UnigramViewModelBase
    {
        public SettingsFeaturedStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLStickerSetCovered>();
        }

        public ObservableCollection<TLStickerSetCovered> Items { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateStickersAsync();
        }

        private async Task UpdateStickersAsync()
        {
            var response = await ProtoService.GetFeaturedStickersAsync(true, 0);
            if (response.IsSucceeded)
            {
                Items.Clear();

                var result = response.Result as TLMessagesFeaturedStickers;
                if (result != null)
                {
                    foreach (var item in result.Sets.OfType<TLStickerSetCovered>())
                    {
                        Items.Add(item);
                    }
                }
            }
        }
    }
}
