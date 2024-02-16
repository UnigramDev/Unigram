//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Controls;
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
    public class SettingsLanguageViewModel : ViewModelBase
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
                var exclude = Settings.Translate.DoNot;
                if (exclude.Count == 1)
                {
                    var first = exclude.First();

                    var item = _officialLanguages.FirstOrDefault(x => x.Id == first);
                    if (item != null)
                    {
                        return item.Name;
                    }

                    return first;
                }
                else if (exclude.Count > 0)
                {
                    return Locale.Declension(Strings.R.Languages, exclude.Count);
                }

                return string.Empty;
            }
        }

        public bool TranslateMessages
        {
            get => Settings.Translate.Messages;
            set
            {
                Settings.Translate.Messages = value;
                RaisePropertyChanged(nameof(TranslateMessages));
            }
        }

        public bool TranslateChats
        {
            get => Settings.Translate.Chats && ClientService.IsPremium;
            set
            {
                Settings.Translate.Chats = value;
                RaisePropertyChanged(nameof(TranslateChats));
            }
        }

        public void ChangeTranslateChat()
        {
            if (ClientService.IsPremium)
            {
                TranslateChats = !TranslateChats;
            }
            else
            {
                ToastPopup.ShowFeature(NavigationService, new PremiumFeatureRealTimeChatTranslation());
            }
        }

        public async void ChangeDoNotTranslate()
        {
            var exclude = Settings.Translate.DoNot;
            var popup = new DoNotTranslatePopup(_officialLanguages, exclude);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.SelectedItems != null)
            {
                Settings.Translate.DoNot = popup.SelectedItems;
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
            var confirm = await ShowPopupAsync(Strings.DeleteLocalization, Strings.AppName, Strings.Delete, Strings.Cancel, destructive: true);
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

            if (list.Empty())
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
