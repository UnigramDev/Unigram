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
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
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
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var predefined = new Background(Constants.WallpaperLocalId, true, false, Constants.WallpaperDefaultFileName, null, null);
            var background = CacheService.SelectedBackground;

            var items = new List<Background>
            {
                predefined
            };

            var response = await ProtoService.SendAsync(new GetBackgrounds(Settings.Appearance.IsDarkTheme()));
            if (response is Backgrounds wallpapers)
            {
                items.AddRange(wallpapers.BackgroundsValue);

                var selected = items.FirstOrDefault(x => x.Id == background?.Id);
                if (selected != null)
                {
                    items.Remove(selected);
                    items.Insert(1, selected);
                }
                else if (background == null)
                {
                    selected = predefined;
                }

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
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
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
            await new BackgroundPopup(Constants.WallpaperColorFileName).ShowQueuedAsync();
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetChatBackgroundsAlert, Strings.Resources.ResetChatBackgroundsAlertTitle, Strings.Resources.Reset, Strings.Resources.Cancel);
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
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
    }
}
