//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.Controls.Chats
{
    public partial class ChatThemeChangedEventArgs : EventArgs
    {
        public ChatTheme Theme { get; }

        public ChatThemeChangedEventArgs(ChatTheme theme)
        {
            Theme = theme;
        }
    }

    public partial class ChatThemeSelectedEventArgs : EventArgs
    {
        public bool Applied { get; }

        public ChatThemeSelectedEventArgs(bool applied)
        {
            Applied = applied;
        }
    }

    public sealed partial class ChatThemeDrawer : UserControl, IIncrementalCollectionOwner
    {
        private readonly DialogViewModel _viewModel;

        private readonly ChatTheme _selectedTheme;
        private readonly ChatBackground _background;

        private readonly IncrementalCollection<ChatThemeViewModel> _items;

        private string _nextOffset = string.Empty;
        private int _nextInsert = 1;

        public event EventHandler<ChatThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<ChatThemeSelectedEventArgs> ThemeSelected;

        public ChatThemeDrawer(DialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            _selectedTheme = viewModel.Chat.Theme;
            _background = viewModel.Chat.Background;

            _items = new IncrementalCollection<ChatThemeViewModel>(this)
            {
                new(viewModel.ClientService, "\u274C", null, null, false)
            };

            if (_selectedTheme is ChatThemeGift selectedGift && !selectedGift.GiftTheme.Gift.OwnerId.IsUser(viewModel.ClientService.Options.MyId))
            {
                _items.Insert(_nextInsert++, new ChatThemeViewModel(viewModel.ClientService, selectedGift.GiftTheme));
            }

            _items.AddRange(viewModel.ClientService.ChatThemes.Select(x => new ChatThemeViewModel(viewModel.ClientService, x, false)));
            _ = _items.LoadMoreItemsAsync(0);

            ScrollingHost.ItemsSource = _items;
            ScrollingHost.SelectedItem = _selectedTheme switch
            {
                ChatThemeEmoji or ChatThemeGift => _items.FirstOrDefault(x => x.AreTheSame(_selectedTheme)),
                _ => _items[0]
            };

            if (ScrollingHost.SelectedItem != null)
            {
                ScrollingHost.SelectionChanged += OnSelectionChanged;
            }

            ApplyButton.Visibility = Visibility.Collapsed;

            WallpaperButton.Visibility = Visibility.Visible;
            WallpaperButton.Content = _background != null
                ? Strings.ChooseANewWallpaper
                : Strings.ChooseBackgroundFromGallery;

            RemoveButton.Content = Strings.RestToDefaultBackground;
            RemoveButton.Visibility = viewModel.Chat.Background != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            var radius = SettingsService.Current.Appearance.CornerRadius;
            var min = Math.Max(4, radius - 4);

            Close.CornerRadius = new CornerRadius(4, min, 4, 4);
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await _viewModel.ClientService.SendAsync(new GetGiftChatThemes(_nextOffset, 10));
            if (response is GiftChatThemes themes)
            {
                foreach (var theme in themes.Themes)
                {
                    var viewModel = new ChatThemeViewModel(_viewModel.ClientService, theme);

                    _items.Insert(_nextInsert++, viewModel);
                    totalCount++;

                    if (_selectedTheme.AreTheSame(new ChatThemeGift(theme)))
                    {
                        ScrollingHost.SelectedItem = viewModel;
                        ScrollingHost.SelectionChanged += OnSelectionChanged;
                    }
                }

                _nextOffset = themes.NextOffset;
                HasMoreItems = themes.NextOffset.Length > 0;
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is not ChatThemeCell content)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                content.Recycle();
                return;
            }
            else if (args.Item is ChatThemeViewModel theme)
            {
                content.Update(args.ItemContainer, theme, _viewModel.ChatId);
                args.Handled = true;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                ThemeChanged?.Invoke(this, new ChatThemeChangedEventArgs(theme.LightSettings != null ? theme.Type : null));

                if (_selectedTheme.AreTheSame(theme.Type))
                {
                    ApplyButton.Visibility = Visibility.Collapsed;

                    WallpaperButton.Visibility = Visibility.Visible;
                    WallpaperButton.Content = _background != null
                        ? Strings.ChooseANewWallpaper
                        : Strings.ChooseBackgroundFromGallery;
                }
                else
                {
                    WallpaperButton.Visibility = Visibility.Collapsed;

                    ApplyButton.Visibility = Visibility.Visible;
                    ApplyButton.Content = theme.LightSettings != null
                         ? Strings.ChatApplyTheme
                         : Strings.ChatResetTheme;
                }
            }
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                if (!_selectedTheme.AreTheSame(theme.Type))
                {
                    var confirm = await _viewModel.ShowPopupAsync(Strings.SaveChangesAlertText, Strings.SaveChangesAlertTitle, Strings.ApplyTheme, Strings.Discard);
                    if (confirm == ContentDialogResult.None)
                    {
                        return;
                    }
                    else if (confirm == ContentDialogResult.Primary)
                    {
                        Apply_Click(null, null);
                        return;
                    }
                }

                ThemeSelected?.Invoke(this, new ChatThemeSelectedEventArgs(false));
            }
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                if (theme.Type is ChatThemeGift gift && gift.GiftTheme.Gift.UsedThemeChatId != _viewModel.ChatId && _viewModel.ClientService.TryGetChat(gift.GiftTheme.Gift.UsedThemeChatId, out Chat usedChat))
                {
                    var confirm = await _viewModel.ShowPopupAsync(string.Format(Strings.GiftThemesSetInReuseInfo, usedChat.Title), Strings.AppName, Strings.GiftThemesSetInReuseConfirm, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                _viewModel.ClientService.Send(new SetChatTheme(_viewModel.Chat.Id, theme.LightSettings == null ? null : theme.Type.ToInput()));
                ThemeSelected?.Invoke(this, new ChatThemeSelectedEventArgs(true));
            }
        }

        private async void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await _viewModel.ShowPopupAsync(new BackgroundsPopup(tsc), _viewModel.Chat.Id);
            var delayed = await tsc.Task;

            if (delayed is bool close && close)
            {
                ThemeSelected?.Invoke(this, new ChatThemeSelectedEventArgs(true));
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClientService.Send(new DeleteChatBackground(_viewModel.Chat.Id, false));
            ThemeSelected?.Invoke(this, new ChatThemeSelectedEventArgs(true));
        }
    }
}
