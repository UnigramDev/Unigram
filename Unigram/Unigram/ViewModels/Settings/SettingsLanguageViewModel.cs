using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
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

        private object _separator;

        public SettingsLanguageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocaleService localeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _localeService = localeService;

            Items = new MvxObservableCollection<object>();

            ChangeCommand = new RelayCommand<LanguagePackInfo>(ChangeExecute);
            DeleteCommand = new RelayCommand<LanguagePackInfo>(DeleteExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetLocalizationTargetInfo(false));
            if (response is LocalizationTargetInfo pack)
            {
                var results = new List<object>();

                results.AddRange(pack.LanguagePacks.Where(x => x.IsInstalled).OrderBy(k => k.Name));

                if (results.Count > 0)
                {
                    results.Add(_separator = new object());
                }

                results.AddRange(pack.LanguagePacks.Where(x => !x.IsInstalled).OrderBy(k => k.Name));

                Items.ReplaceWith(results);
                SelectedItem = pack.LanguagePacks.FirstOrDefault(x => x.Id == SettingsService.Current.LanguagePackId);
            }
        }

        public MvxObservableCollection<object> Items { get; private set; }

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
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.DeleteLocalization, Strings.Resources.AppName, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new DeleteLanguagePack(info.Id));
            Items.Remove(info);

            var any = Items.OfType<LanguagePackInfo>().FirstOrDefault(x => x.IsInstalled);
            if (any == null)
            {
                Items.Remove(_separator);
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
