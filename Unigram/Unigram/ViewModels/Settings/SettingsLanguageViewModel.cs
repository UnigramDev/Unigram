using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Common;
using Windows.ApplicationModel.Resources.Core;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsLanguageViewModel : TLViewModelBase
    {
        public SettingsLanguageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<object>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (Items.Count > 0 && SelectedItem != null)
            {
                return;
            }

            //var response = await LegacyService.GetLanguagesAsync();
            //if (response.IsSucceeded)
            //{
            //    Items.ReplaceWith(response.Result.OrderBy(x => x.NativeName));

            //    var context = ResourceContext.GetForCurrentView();
            //    if (context.Languages.Count > 0)
            //    {
            //        var key = context.Languages[0];
            //        var already = Items.FirstOrDefault(x => key.StartsWith(x.LangCode, StringComparison.OrdinalIgnoreCase));
            //        if (already != null)
            //        {
            //            SelectedItem = already;
            //        }
            //    }
            //}
        }

        public MvxObservableCollection<object> Items { get; private set; }

        private object _selectedItem;
        public object SelectedItem
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
