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
using Windows.ApplicationModel.Resources.Core;
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
            if (Items.Count > 0 && SelectedItem != null)
            {
                return;
            }

            var response = await ProtoService.GetLanguagesAsync();
            if (response.IsSucceeded)
            {
                Items.ReplaceWith(response.Result.OrderBy(x => x.NativeName));

                var context = ResourceContext.GetForCurrentView();
                if (context.Languages.Count > 0)
                {
                    var key = context.Languages[0];
                    var already = Items.FirstOrDefault(x => key.StartsWith(x.LangCode, StringComparison.OrdinalIgnoreCase));
                    if (already != null)
                    {
                        SelectedItem = already;
                    }
                }
            }
        }

        public MvxObservableCollection<TLLangPackLanguage> Items { get; private set; }

        private TLLangPackLanguage _selectedItem;
        public TLLangPackLanguage SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }
    }
}
