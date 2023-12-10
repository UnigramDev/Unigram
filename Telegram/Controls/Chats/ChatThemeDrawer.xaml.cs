//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public class ChatThemeChangedEventArgs : EventArgs
    {
        public ChatTheme Theme { get; }

        public ChatThemeChangedEventArgs(ChatTheme theme)
        {
            Theme = theme;
        }
    }

    public class ChatThemeSelectedEventArgs : EventArgs
    {
        public bool Applied { get; }

        public ChatThemeSelectedEventArgs(bool applied)
        {
            Applied = applied;
        }
    }

    public sealed partial class ChatThemeDrawer : UserControl
    {
        private readonly DialogViewModel _viewModel;

        private readonly ChatThemeViewModel _selectedTheme;
        private readonly ChatBackground _background;

        public event EventHandler<ChatThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<ChatThemeSelectedEventArgs> ThemeSelected;

        public ChatThemeDrawer(DialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            var chat = _viewModel.Chat;
            var defaultTheme = new ChatThemeViewModel(viewModel.ClientService, "\U0001F3E0", null, null);
            var themes = viewModel.ClientService.GetChatThemes().Select(x => new ChatThemeViewModel(viewModel.ClientService, x));

            var items = new[] { defaultTheme }.Union(themes).ToList();

            _selectedTheme = string.IsNullOrEmpty(chat.ThemeName) ? items[0] : items.FirstOrDefault(x => x.Name == chat.ThemeName);
            _background = chat.Background;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = _selectedTheme;
            ScrollingHost.SelectionChanged += OnSelectionChanged;

            ApplyButton.Visibility = Visibility.Collapsed;

            WallpaperButton.Visibility = Visibility.Visible;
            WallpaperButton.Content = _background != null
                ? Strings.ChooseANewWallpaper
                : Strings.ChooseBackgroundFromGallery;

            RemoveButton.Content = Strings.RestToDefaultBackground;
            RemoveButton.Visibility = chat.Background != null
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
            else if (args.ItemContainer.ContentTemplateRoot is ChatThemeCell content && args.Item is ChatThemeViewModel theme)
            {
                content.Update(theme);
                args.Handled = true;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                ThemeChanged?.Invoke(this, new ChatThemeChangedEventArgs(theme.LightSettings != null ? theme : null));

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

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                if (theme != _selectedTheme)
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

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is ChatThemeViewModel theme)
            {
                _viewModel.ClientService.Send(new SetChatTheme(_viewModel.Chat.Id, theme.Name));
                ThemeSelected?.Invoke(this, new ChatThemeSelectedEventArgs(true));
            }
        }

        private async void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            var tsc = new TaskCompletionSource<object>();

            var confirm = await _viewModel.ShowPopupAsync(typeof(BackgroundsPopup), _viewModel.Chat.Id, tsc);
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
