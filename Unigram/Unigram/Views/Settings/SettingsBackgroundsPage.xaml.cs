//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : HostedPage
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        public SettingsBackgroundsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ChatBackground;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var background = List.ItemFromContainer(sender) as Background;

            if (background == null || background.Id == Constants.WallpaperLocalId)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.ShareCommand, background, Strings.Resources.ShareFile, new FontIcon { Glyph = Icons.Share });
            flyout.CreateFlyoutItem(ViewModel.DeleteCommand, background, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var preview = root.Children[0] as ChatBackgroundRenderer;
            var check = root.Children[1];

            preview.UpdateSource(ViewModel.ClientService, wallpaper, true);
            check.Visibility = wallpaper == ViewModel.SelectedItem ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background wallpaper)
            {
                var confirm = await new BackgroundPopup(wallpaper).ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    await ViewModel.NavigatedToAsync(null, NavigationMode.Refresh, null);
                }
            }
        }
    }
}
