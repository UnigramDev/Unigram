//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBackgroundsViewModel : TLViewModelBase
    {
        private readonly IStorageService _storageService;

        public SettingsBackgroundsViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = storageService;

            Items = new DiffObservableCollection<Background>(new BackgroundDiffHandler(), Constants.DiffOptions);

            LocalCommand = new RelayCommand(LocalExecute);
            ColorCommand = new RelayCommand(ColorExecute);
            ResetCommand = new RelayCommand(ResetExecute);

            ShareCommand = new RelayCommand<Background>(ShareExecute);
            DeleteCommand = new RelayCommand<Background>(DeleteExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var dark = Settings.Appearance.IsDarkTheme();
            var freeform = dark ? new[] { 0x1B2836, 0x121A22, 0x1B2836, 0x121A22 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };

            var background = ClientService.SelectedBackground;
            var predefined = new Background(Constants.WallpaperColorId, true, dark, string.Empty,
                new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));

            var items = new List<Background>
            {
                predefined
            };

            var response = await ClientService.SendAsync(new GetBackgrounds(dark));
            if (response is Backgrounds wallpapers)
            {
                items.AddRange(wallpapers.BackgroundsValue.Where(x => x.Type is not BackgroundTypePattern || x.Type is BackgroundTypePattern pattern && (pattern.IsInverted == dark || dark)));

                var selected = items.FirstOrDefault(x => x.Id == background?.Id);
                if (selected != null)
                {
                    items.Remove(selected);
                }

                if (background != null)
                {
                    items.Insert(0, background);
                }

                selected = background ?? predefined;

                SelectedItem = selected;
                Items.ReplaceDiff(items);
            }
            else
            {
                if (background != null)
                {
                    items.Add(background);
                    SelectedItem = background;
                }
                else
                {
                    SelectedItem = predefined;
                }

                Items.ReplaceDiff(items);
            }
        }

        private Background _selectedItem;
        public Background SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public DiffObservableCollection<Background> Items { get; private set; }

        public RelayCommand LocalCommand { get; }
        private async void LocalExecute()
        {
            var file = await _storageService.PickSingleFileAsync(XamlRoot, PickerLocationId.PicturesLibrary, Constants.PhotoTypes);
            if (file != null)
            {
                await file.CopyAsync(ApplicationData.Current.TemporaryFolder, Constants.WallpaperLocalFileName, NameCollisionOption.ReplaceExisting);
                await new BackgroundPopup(Constants.WallpaperLocalFileName).ShowQueuedAsync(XamlRoot);
            }
        }

        public RelayCommand ColorCommand { get; }
        private async void ColorExecute()
        {
            var confirm = await new BackgroundPopup(Constants.WallpaperColorFileName).ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.ResetChatBackgroundsAlert, Strings.Resources.ResetChatBackgroundsAlertTitle, Strings.Resources.Reset, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new ResetBackgrounds());
            if (response is Ok)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
            else if (response is Error error)
            {

            }
        }

        public RelayCommand<Background> ShareCommand { get; }
        private async void ShareExecute(Background background)
        {
            if (background == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetBackgroundUrl(background.Name, background.Type));
            if (response is HttpUrl url)
            {
                await SharePopup.Create().ShowAsync(new Uri(url.Url), null);
            }
        }

        public RelayCommand<Background> DeleteCommand { get; }
        private async void DeleteExecute(Background background)
        {
            if (background == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.DeleteChatBackgroundsAlert, Locale.Declension("DeleteBackground", 1), Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new RemoveBackground(background.Id));
            if (response is Ok)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
            else if (response is Error error)
            {

            }
        }
    }

    public class BackgroundDiffHandler : IDiffHandler<Background>
    {
        public bool CompareItems(Background oldItem, Background newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(Background oldItem, Background newItem)
        {

        }
    }
}
