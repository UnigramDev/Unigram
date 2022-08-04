using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Popups;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBackgroundsViewModel : TLViewModelBase
    {
        public SettingsBackgroundsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Background>();

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

            var background = CacheService.SelectedBackground;
            var predefined = new Background(Constants.WallpaperLocalId, true, dark, Constants.WallpaperDefaultFileName, null,
                new BackgroundTypeFill(new BackgroundFillFreeformGradient(freeform)));

            var items = new List<Background>
            {
                predefined
            };

            var response = await ProtoService.SendAsync(new GetBackgrounds(dark));
            if (response is Backgrounds wallpapers)
            {
                items.AddRange(wallpapers.BackgroundsValue);

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
                Items.ReplaceWith(items);
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

                Items.ReplaceWith(items);
            }
        }

        private Background _selectedItem;
        public Background SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public MvxObservableCollection<Background> Items { get; private set; }

        public RelayCommand LocalCommand { get; }
        private async void LocalExecute()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var token = StorageApplicationPermissions.FutureAccessList.Enqueue(file);
                    await new BackgroundPopup(Constants.WallpaperLocalFileName + $"#{token}").ShowQueuedAsync();
                }
            }
            catch { }
        }

        public RelayCommand ColorCommand { get; }
        private async void ColorExecute()
        {
            var confirm = await new BackgroundPopup(Constants.WallpaperColorFileName).ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetChatBackgroundsAlert, Strings.Resources.ResetChatBackgroundsAlertTitle, Strings.Resources.Reset, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new ResetBackgrounds());
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

            var response = await ProtoService.SendAsync(new GetBackgroundUrl(background.Name, background.Type));
            if (response is HttpUrl url)
            {
                await SharePopup.GetForCurrentView().ShowAsync(new Uri(url.Url), null);
            }
        }

        public RelayCommand<Background> DeleteCommand { get; }
        private async void DeleteExecute(Background background)
        {
            if (background == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.DeleteChatBackgroundsAlert, Locale.Declension("DeleteBackground", 1), Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new RemoveBackground(background.Id));
            if (response is Ok)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
            else if (response is Error error)
            {

            }
        }
    }
}
