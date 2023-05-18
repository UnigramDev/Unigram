//
// Copyright Fela Ameghino 2015-2023
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
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsBackgroundsViewModel : ViewModelBase
    {
        private long? _chatId;

        public SettingsBackgroundsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<Background>(new BackgroundDiffHandler(), Constants.DiffOptions);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                _chatId = chatId;
            }

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

        public void ChangeToLocal()
        {
            _ = ChangeToLocalAsync(true);
        }

        public async Task<ContentDialogResult> ChangeToLocalAsync(bool refresh)
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
                    await file.CopyAsync(ApplicationData.Current.TemporaryFolder, Constants.WallpaperLocalFileName, NameCollisionOption.ReplaceExisting);

                    var confirm = await ShowPopupAsync(typeof(BackgroundPopup), new BackgroundParameters(Constants.WallpaperLocalFileName, _chatId));
                    if (confirm == ContentDialogResult.Primary && refresh)
                    {
                        await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
                    }

                    return confirm;
                }
            }
            catch { }

            return ContentDialogResult.None;
        }

        public void ChangeToColor()
        {
            _ = ChangeToColorAsync(true);
        }

        public async Task<ContentDialogResult> ChangeToColorAsync(bool refresh)
        {
            var confirm = await ShowPopupAsync(typeof(BackgroundPopup), new BackgroundParameters(Constants.WallpaperColorFileName, _chatId));
            if (confirm == ContentDialogResult.Primary && refresh)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }

            return confirm;
        }

        public void Change(Background background)
        {
            _ = ChangeAsync(background, true);
        }

        public async Task<ContentDialogResult> ChangeAsync(Background background, bool refresh)
        {
            var confirm = await ShowPopupAsync(typeof(BackgroundPopup), new BackgroundParameters(background, _chatId));
            if (confirm == ContentDialogResult.Primary && refresh)
            {
                await NavigatedToAsync(null, NavigationMode.Refresh, null);
            }

            return confirm;
        }

        public async void Reset()
        {
            var confirm = await ShowPopupAsync(Strings.ResetChatBackgroundsAlert, Strings.ResetChatBackgroundsAlertTitle, Strings.Reset, Strings.Cancel);
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

        public async void Share(Background background)
        {
            if (background == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetBackgroundUrl(background.Name, background.Type));
            if (response is HttpUrl url)
            {
                await new ChooseChatsPopup().ShowAsync(new Uri(url.Url), null);
            }
        }

        public async void Delete(Background background)
        {
            if (background == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.DeleteChatBackgroundsAlert, Locale.Declension(Strings.R.DeleteBackground, 1), Strings.Delete, Strings.Cancel, dangerous: true);
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
            return ChatBackgroundPresenter.BackgroundEquals(oldItem, newItem, true);
        }

        public void UpdateItem(Background oldItem, Background newItem)
        {

        }
    }
}
