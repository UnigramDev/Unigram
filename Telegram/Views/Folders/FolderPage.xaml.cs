//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Folders
{
    public sealed partial class FolderPage : HostedPage
    {
        public FolderViewModel ViewModel => DataContext as FolderViewModel;

        public FolderPage()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TitleField.Focus(FocusState.Keyboard);
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as ProfileCell;
            var element = content.DataContext as ChatFolderElement;

            content.UpdateChatFolder(ViewModel.ClientService, element);
        }

        private void Include_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            flyout.CreateFlyoutItem(viewModel.RemoveIncluded, chat, Strings.StickersRemove, Icons.Delete);

            args.ShowAt(flyout, element);
        }

        private void Exclude_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            flyout.CreateFlyoutItem(viewModel.RemoveExcluded, chat, Strings.StickersRemove, Icons.Delete);

            args.ShowAt(flyout, element);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiList.ItemsSource = Icons.Folders;
            EmojiList.SelectedItem = ViewModel.Icon;

            var flyout = FlyoutBase.GetAttachedFlyout(EmojiButton);
            flyout.ShowAt(EmojiButton, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void EmojiList_ItemClick(object sender, ItemClickEventArgs e)
        {
            FlyoutBase.GetAttachedFlyout(EmojiButton).Hide();

            if (e.ClickedItem is ChatFolderIcon2 icon)
            {
                ViewModel.SetIcon(icon);
            }
        }

        private void EmojiList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is FontIcon textBlock && args.Item is ChatFolderIcon2 icon)
            {
                textBlock.Glyph = Icons.FolderToGlyph(icon).Item1;
                args.Handled = true;
            }
        }

        #region Binding

        private string ConvertTitle(ChatFolder folder)
        {
            return folder == null ? Strings.FilterNew : folder.Title;
        }

        private string ConvertEmoji(ChatFolderIcon2 icon)
        {
            return Icons.FolderToGlyph(icon).Item1;
        }

        private Visibility ConvertExcludeVisibility(int linksCount)
        {
            return linksCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        private void Link_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var link = button.DataContext as ChatFolderInviteLink;

            var content = button.Content as Grid;
            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            title.Text = string.IsNullOrEmpty(link.Name) ? link.InviteLink : link.Name;
            subtitle.Text = Locale.Declension(Strings.R.FilterInviteChats, link.ChatIds.Count);
        }

        private void Link_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {

        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ChatFolderInviteLink link)
            {
                ViewModel.OpenLink(link);
            }
        }
    }
}
