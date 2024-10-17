//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
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
    public record FolderPageCreateArgs(long IncludeChatId);

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

        private void Include_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var chat = IncludeHost.ItemFromContainer(sender) as ChatFolderElement;

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

            var chat = ExcludeHost.ItemFromContainer(sender) as ChatFolderElement;

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

        private string ConvertRemanining(int count)
        {
            return Locale.Declension(Strings.R.FilterShowMoreChats, count);
        }

        private Visibility ConvertExcludeVisibility(int linksCount)
        {
            return linksCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        private void Link_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {

        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {

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

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;

                if (sender == IncludeHost)
                {
                    args.ItemContainer.ContextRequested += Include_ContextRequested;
                }
                else if (sender == ExcludeHost)
                {
                    args.ItemContainer.ContextRequested += Exclude_ContextRequested;
                }
                else
                {
                    args.ItemContainer.ContextRequested += Link_ContextRequested;
                }
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell profileCell && args.Item is ChatFolderElement element)
            {
                profileCell.UpdateChatFolder(ViewModel.ClientService, element);
                args.Handled = true;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatInviteLinkCell chatInviteLinkCell && args.Item is ChatFolderInviteLink inviteLink)
            {
                chatInviteLinkCell.UpdateInviteLink(inviteLink);
                args.Handled = true;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatFolderInviteLink link)
            {
                ViewModel.OpenLink(link);
            }
        }
    }
}
