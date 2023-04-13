//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatThemeDrawer : UserControl
    {
        private readonly IClientService _clientService;

        private readonly ChatTheme _selectedTheme;
        private readonly ChatBackground _background;

        private readonly Action<bool, ChatTheme, ChatBackground> _action;
        private readonly Action<bool, ChatTheme, ChatBackground> _close;

        public ChatThemeDrawer(IClientService clientService, string selectedTheme, ChatBackground background, Action<bool, ChatTheme, ChatBackground> action, Action<bool, ChatTheme, ChatBackground> close)
        {
            InitializeComponent();

            _clientService = clientService;
            _action = action;
            _close = close;

            var items = new List<ChatTheme>(clientService.GetChatThemes());
            items.Insert(0, new ChatTheme("\u274C", null, null));

            _selectedTheme = string.IsNullOrEmpty(selectedTheme) ? items[0] : items.FirstOrDefault(x => x.Name == selectedTheme);
            _background = background;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = _selectedTheme;

            RemoveButton.Content = Strings.RestToDefaultBackground;
            RemoveButton.Visibility = background != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            var radius = SettingsService.Current.Appearance.BubbleRadius;
            var min = Math.Max(4, radius - 2);

            Close.CornerRadius = new CornerRadius(4, min, 4, 4);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ChatTheme;
            var cell = args.ItemContainer.ContentTemplateRoot as ChatThemeCell;

            if (cell != null && theme != null)
            {
                cell.Update(_clientService, theme);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatTheme theme)
            {
                _action(true, theme.LightSettings != null ? theme : null, _background);

                if (theme == _selectedTheme)
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatTheme theme)
            {
                _close(theme != _selectedTheme, theme.LightSettings != null ? theme : null, _background);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatTheme theme)
            {
                _action(theme != _selectedTheme, theme.LightSettings != null ? theme : null, _background);
            }
        }

        private async void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement
            await MessagePopup.ShowAsync("Next beta, sorry.", Strings.AppName, Strings.OK);
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement
            await MessagePopup.ShowAsync("Next beta, sorry.", Strings.AppName, Strings.OK);
        }
    }
}
