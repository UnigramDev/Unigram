//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
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
    public partial class SettingsLanguageViewModel : ViewModelBase, IDiffHandler<LanguagePackInfo>
    {
        private readonly ILocaleService _localeService;
        private readonly List<LanguagePackInfo> _officialLanguages = new();
        private readonly List<LanguagePackInfo> _languages = new();

        public SettingsLanguageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocaleService localeService)
            : base(clientService, settingsService, aggregator)
        {
            _localeService = localeService;

            Items = new DiffObservableCollection<LanguagePackInfo>(this, Constants.DiffOptions);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetLocalizationTargetInfo(false));
            if (response is LocalizationTargetInfo pack)
            {
                var customs = pack.LanguagePacks.Where(x => x.IsInstalled).OrderBy(x => x.IsBeta).ThenBy(k => k.Name).ToList();
                var results = pack.LanguagePacks.Where(x => !x.IsInstalled).OrderBy(k => k.IsBeta).ThenBy(x => x.Name).ToList();

                var items = new List<LanguagePackInfo>();

                results.InsertRange(0, customs);

                var english = results.FirstOrDefault(x => x.Id == "en");
                if (english != null)
                {
                    results.Remove(english);
                    results.Insert(0, english);
                }

                var suggested = results.FirstOrDefault(x => x.Id == ClientService.Options.SuggestedLanguagePackId);
                if (suggested != null && suggested != english)
                {
                    results.Remove(suggested);
                    results.Insert(0, suggested);
                }

                var current = results.FirstOrDefault(x => x.Id == ClientService.Options.LanguagePackId);
                if (current != null && current != suggested && current != english)
                {
                    results.Remove(current);
                    results.Insert(0, current);
                }

                _officialLanguages.AddRange(pack.LanguagePacks);
                _languages.AddRange(results);

                SelectedItem = pack.LanguagePacks.FirstOrDefault(x => x.Id == SettingsService.Current.LanguagePackId);
                Items.AddRange(results);

                RaisePropertyChanged(nameof(DoNotTranslate));
            }
        }

        public DiffObservableCollection<LanguagePackInfo> Items { get; private set; }

        private LanguagePackInfo _selectedItem;
        public LanguagePackInfo SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        private string _query;
        public string Query
        {
            get => _query;
            set => SetQuery(value);
        }

        private void SetQuery(string value)
        {
            if (Set(ref _query, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Items.ReplaceDiff(_languages);
                }
                else
                {
                    Items.ReplaceDiff(_languages.Where(FilterByQuery));
                }
            }
        }

        private bool FilterByQuery(LanguagePackInfo language)
        {
            if (ClientEx.SearchByPrefix(language.Name, Query))
            {
                return true;
            }

            return ClientEx.SearchByPrefix(language.NativeName, Query);
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
                ToastPopup.ShowFeaturePromo(NavigationService, new PremiumFeatureRealTimeChatTranslation());
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

            ClientService.Send(new DeleteLanguagePack(info.Id));
            Items.Remove(info);

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

        public bool CompareItems(LanguagePackInfo oldItem, LanguagePackInfo newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(LanguagePackInfo oldItem, LanguagePackInfo newItem)
        {
            // Do nothing
        }
    }
}
