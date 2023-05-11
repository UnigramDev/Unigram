//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsLanguageViewModel : TLViewModelBase
    {
        private readonly ILocaleService _localeService;
        private readonly List<LanguagePackInfo> _officialLanguages = new();

        public SettingsLanguageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocaleService localeService)
            : base(clientService, settingsService, aggregator)
        {
            _localeService = localeService;

            Items = new MvxObservableCollection<List<LanguagePackInfo>>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetLocalizationTargetInfo(false));
            if (response is LocalizationTargetInfo pack)
            {
                var customs = pack.LanguagePacks.Where(x => x.IsInstalled).OrderBy(k => k.Name).ToList();
                var results = pack.LanguagePacks.Where(x => !x.IsInstalled).OrderBy(k => k.Name).ToList();

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
                exclude ??= new[] { Settings.LanguagePackId };

                if (exclude.Length == 1)
                {
                    var item = _officialLanguages.FirstOrDefault(x => x.Id == exclude[0]);
                    if (item != null)
                    {
                        return item.Name;
                    }
                }

                return Locale.Declension(Strings.R.Languages, exclude.Length);
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

        public async void ChangeDoNotTranslate()
        {
            var exclude = Settings.DoNotTranslate;
            exclude ??= new[] { Settings.LanguagePackId };

            var popup = new DoNotTranslatePopup(_officialLanguages, exclude);

            var confirm = await ShowPopupAsync(popup);
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

        public async void Change(LanguagePackInfo info)
        {
            IsLoading = true;

            var response = await _localeService.SetLanguageAsync(info, true);
            if (response is Ok)
            {
                //ApplicationLanguages.PrimaryLanguageOverride = info.Id;
                //ResourceContext.GetForCurrentView().Reset();
                //ResourceContext.GetForViewIndependentUse().Reset();

                //TLWindowContext.Current.NavigationServices.Remove(NavigationService);
                //BootStrapper.Current.NavigationService.Reset();

                WindowContext.ForEach(window =>
                {
                    ResourceContext.GetForCurrentView().Reset();
                    ResourceContext.GetForViewIndependentUse().Reset();

                    if (window.Content is FrameworkElement frameworkElement)
                    {
                        //window.CoreWindow.FlowDirection = _localeService.FlowDirection == FlowDirection.RightToLeft
                        //    ? CoreWindowFlowDirection.RightToLeft
                        //    : CoreWindowFlowDirection.LeftToRight;

                        frameworkElement.FlowDirection = LocaleService.Current.FlowDirection;
                    }

                    if (window.Content is RootPage root)
                    {
                        root.UpdateComponent();
                    }
                });
            }

            IsLoading = false;
        }

        public async void Delete(LanguagePackInfo info)
        {
            var confirm = await ShowPopupAsync(Strings.DeleteLocalization, Strings.AppName, Strings.Delete, Strings.Cancel, dangerous: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var list = info.IsInstalled ? Items.FirstOrDefault() : Items.LastOrDefault();
            if (list == null)
            {
                return;
            }

            ClientService.Send(new DeleteLanguagePack(info.Id));
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
                Change(fallback);
            }
        }
    }
}
