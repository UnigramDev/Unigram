using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Host;
using Unigram.Views.Popups;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsLanguageViewModel : TLViewModelBase
    {
        private readonly ILocaleService _localeService;
        private readonly List<LanguagePackInfo> _officialLanguages = new();

        public SettingsLanguageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocaleService localeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _localeService = localeService;

            Items = new MvxObservableCollection<List<LanguagePackInfo>>();

            DoNotTranslateCommand = new RelayCommand(DoNotTranslateExecute);

            ChangeCommand = new RelayCommand<LanguagePackInfo>(ChangeExecute);
            DeleteCommand = new RelayCommand<LanguagePackInfo>(DeleteExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
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

                _officialLanguages.AddRange(pack.LanguagePacks);

                Items.ReplaceWith(items);
                SelectedItem = pack.LanguagePacks.FirstOrDefault(x => x.Id == SettingsService.Current.LanguagePackId);

                RaisePropertyChanged(nameof(DoNotTranslate));
            }
        }

        public MvxObservableCollection<List<LanguagePackInfo>> Items { get; private set; }

        private LanguagePackInfo _selectedItem;
        public LanguagePackInfo SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public string DoNotTranslate
        {
            get
            {
                var exclude = Settings.DoNotTranslate;
                if (exclude == null)
                {
                    exclude = new[] { Settings.LanguagePackId };
                }

                if (exclude.Length == 1)
                {
                    var item = _officialLanguages.FirstOrDefault(x => x.Id == exclude[0]);
                    if (item != null)
                    {
                        return item.Name;
                    }
                }

                return Locale.Declension("Languages", exclude.Length);
            }
        }

        public bool IsTranslateEnabled
        {
            get => Settings.IsTranslateEnabled;
            set
            {
                Settings.IsTranslateEnabled = value;
                RaisePropertyChanged(nameof(IsTranslateEnabled));
            }
        }

        public RelayCommand DoNotTranslateCommand { get; }
        private async void DoNotTranslateExecute()
        {
            var exclude = Settings.DoNotTranslate;
            if (exclude == null)
            {
                exclude = new[] { Settings.LanguagePackId };
            }

            var popup = new DoNotTranslatePopup(_officialLanguages, exclude);

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && popup.SelectedItems != null)
            {
                var updated = popup.SelectedItems;
                if (updated.Count == 1 && updated[0] == Settings.LanguagePackId)
                {
                    updated = null;
                }

                Settings.DoNotTranslate = updated?.ToArray();
                RaisePropertyChanged(nameof(DoNotTranslate));
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
