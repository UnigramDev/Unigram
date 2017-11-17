using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsLanguageViewModel : UnigramViewModelBase
    {
        public SettingsLanguageViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<TLLangPackLanguage>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.GetLanguagesAsync();
            if (response.IsSucceeded)
            {
                Items.ReplaceWith(response.Result.OrderBy(x => x.NativeName));
            }
        }

        public MvxObservableCollection<TLLangPackLanguage> Items { get; private set; }
    }
}
