//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

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

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(viewModel.RemoveIncluded, chat, Strings.StickersRemove, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        private void Exclude_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(viewModel.RemoveExcluded, chat, Strings.StickersRemove, Icons.Delete);
            flyout.ShowAt(sender, args);
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

        private void NameColor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is GridViewItem selector)
            {
                var width = e.NewSize.Width;
                var content = selector.ContentTemplateRoot as Grid;

                //content.Width = width;
                content.Height = width;

                content.CornerRadius = new CornerRadius(width / 2);
                selector.CornerRadius = new CornerRadius(width / 2);
                selector.FocusVisualMargin = new Thickness(2);
            }
        }

        private void Tag_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.SizeChanged += NameColor_SizeChanged;
            }

            args.IsContainerPrepared = true;
        }

        private void Tag_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content
                && content.Children[0] is TextBlock textBlock
                && args.Item is NameColor colors)
            {
                content.Background = new SolidColorBrush(colors.LightThemeColors[0]);
                textBlock.Text = colors.Id == -1
                    ? ViewModel.IsPremium ? Icons.Dismiss : Icons.LockClosed
                    : string.Empty;

                args.Handled = true;
            }
        }

        private void Tag_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tags.SelectedItem is NameColor colors)
            {
                var foreground = ViewModel.ClientService.GetAccentBrush(colors.Id);

                TagPreview.Foreground = foreground;
                TagPreview.Background = foreground.WithOpacity(0.2);
                TagPreview.Visibility = colors.Id != -1
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                TagDisabled.Text = ViewModel.IsPremium
                    ? Strings.FolderTagNoColor
                    : Strings.FolderTagNoColorPremium;

                TagDisabled.Visibility = colors.Id != -1
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureAdvancedChatManagement()));
        }
    }
}
