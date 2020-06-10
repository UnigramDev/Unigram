using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Host;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsLanguageViewModel : TLViewModelBase
    {
        private readonly ILocaleService _localeService;

        public SettingsLanguageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocaleService localeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _localeService = localeService;

            Items = new MvxObservableCollection<List<LanguagePackInfo>>();

            ChangeCommand = new RelayCommand<LanguagePackInfo>(ChangeExecute);
            DeleteCommand = new RelayCommand<LanguagePackInfo>(DeleteExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetLocalizationTargetInfo(false));
            if (response is LocalizationTargetInfo pack)
            {
                var customs = new List<LanguagePackInfo>();
                var results = new List<LanguagePackInfo>();

                customs.AddRange(pack.LanguagePacks.Where(x => x.IsInstalled).OrderBy(k => k.Name));
                results.AddRange(pack.LanguagePacks.Where(x => !x.IsInstalled).OrderBy(k => k.Name));

                var items = new List<List<LanguagePackInfo>>();

                if (customs.Count > 0)
                {
                    items.Add(customs);
                }
                if (results.Count > 0)
                {
                    items.Add(results);
                }

                Items.ReplaceWith(items);
                SelectedItem = pack.LanguagePacks.FirstOrDefault(x => x.Id == SettingsService.Current.LanguagePackId);
            }
        }

        public MvxObservableCollection<List<LanguagePackInfo>> Items { get; private set; }

        private LanguagePackInfo _selectedItem;
        public LanguagePackInfo SelectedItem
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

        public RelayCommand<LanguagePackInfo> ChangeCommand { get; }
        private async void ChangeExecute(LanguagePackInfo info)
        {
            IsLoading = true;

            var response = await _localeService.SetLanguageAsync(info, true);
            if (response is Ok)
            {
                //ApplicationLanguages.PrimaryLanguageOverride = info.Id;
                //ResourceContext.GetForCurrentView().Reset();
                //ResourceContext.GetForViewIndependentUse().Reset();

                //TLWindowContext.GetForCurrentView().NavigationServices.Remove(NavigationService);
                //BootStrapper.Current.NavigationService.Reset();

                foreach (var window in WindowContext.ActiveWrappers)
                {
                    window.Dispatcher.Dispatch(() =>
                    {
                        ResourceContext.GetForCurrentView().Reset();
                        ResourceContext.GetForViewIndependentUse().Reset();

                        if (window.Content is RootPage root)
                        {
                            window.Dispatcher.Dispatch(() =>
                            {
                                root.UpdateComponent();
                            });
                        }
                    });
                }
            }

            IsLoading = false;
        }

        public RelayCommand<LanguagePackInfo> DeleteCommand { get; }
        private async void DeleteExecute(LanguagePackInfo info)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.DeleteLocalization, Strings.Resources.AppName, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var list = info.IsInstalled ? Items.FirstOrDefault() : Items.LastOrDefault();
            if (list == null)
            {
                return;
            }

            ProtoService.Send(new DeleteLanguagePack(info.Id));
            list.Remove(info);

            if (list.IsEmpty())
            {
                Items.Remove(list);
            }

            if (info.Id != SettingsService.Current.LanguagePackId)
            {
                return;
            }

            var fallback = Items.OfType<LanguagePackInfo>().FirstOrDefault(x => x.Id == ApplicationLanguages.Languages[0]);
            if (fallback != null)
            {
                ChangeExecute(fallback);
            }
        }
    }
}
