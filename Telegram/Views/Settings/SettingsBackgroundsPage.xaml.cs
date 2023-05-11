//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsBackgroundsPage : HostedPage
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        public SettingsBackgroundsPage()
        {
            InitializeComponent();
            Title = Strings.ChatBackground;
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
            var background = ScrollingHost.ItemFromContainer(sender) as Background;

            if (background == null || background.Id == Constants.WallpaperLocalId)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.Share, background, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(ViewModel.Delete, background, Strings.Delete, Icons.Delete);

            args.ShowAt(flyout, element);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var background = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var preview = root.Children[0] as ChatBackgroundRenderer;
            var check = root.Children[1];

            preview.UpdateSource(ViewModel.ClientService, background, true);
            check.Visibility = background == ViewModel.SelectedItem ? Visibility.Visible : Visibility.Collapsed;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background background)
            {
                ViewModel.Change(background);
            }
        }
    }
}
