//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsBackgroundsViewModel : ViewModelBase, IHandle
    {
        private long? _chatId;

        public SettingsBackgroundsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<Background>(new BackgroundDiffHandler(), Constants.DiffOptions);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateDefaultBackground>(this, Handle);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                _chatId = chatId;
            }

            var dark = Settings.Appearance.IsDarkTheme();
            var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };

            var background = ClientService.DefaultBackground;
            var predefined = new Background(Constants.WallpaperColorId, true, dark, string.Empty,
                new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));

            var items = new List<Background>
            {
                predefined
            };

            var response = await ClientService.SendAsync(new GetInstalledBackgrounds(dark));
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

        public void Handle(UpdateDefaultBackground update)
        {
            this.BeginOnUIThread(Refresh);
        }

        private async void Refresh()
        {
            await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
        }

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

                    var tsc = new TaskCompletionSource<object>();
                    await ShowPopupAsync(new BackgroundPopup(tsc), new BackgroundParameters(Constants.WallpaperLocalFileName, _chatId));

                    var delayed = await tsc.Task;
                    var confirm = delayed is bool close && close
                        ? ContentDialogResult.Primary
                        : ContentDialogResult.Secondary;

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
            var tsc = new TaskCompletionSource<object>();
            await ShowPopupAsync(new BackgroundPopup(tsc), new BackgroundParameters(Constants.WallpaperColorFileName, _chatId));

            var delayed = await tsc.Task;
            var confirm = delayed is bool close && close
                ? ContentDialogResult.Primary
                : ContentDialogResult.Secondary;

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
            var tsc = new TaskCompletionSource<object>();
            await ShowPopupAsync(new BackgroundPopup(tsc), new BackgroundParameters(background, _chatId));

            var delayed = await tsc.Task;
            var confirm = delayed is bool close && close
                ? ContentDialogResult.Primary
                : ContentDialogResult.Secondary;

            if (confirm == ContentDialogResult.Primary && refresh)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
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

            var response = await ClientService.SendAsync(new ResetInstalledBackgrounds());
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
                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(url));
            }
        }

        public async void Delete(Background background)
        {
            if (background == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.DeleteChatBackgroundsAlert, Locale.Declension(Strings.R.DeleteBackground, 1), Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new RemoveInstalledBackground(background.Id));
            if (response is Ok)
            {
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
            else if (response is Error error)
            {

            }
        }
    }

    public partial class BackgroundDiffHandler : IDiffHandler<Background>
    {
        public bool CompareItems(Background oldItem, Background newItem)
        {
            return ChatBackgroundControl.BackgroundEquals(oldItem, newItem, true);
        }

        public void UpdateItem(Background oldItem, Background newItem)
        {

        }
    }
}
