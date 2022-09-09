using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Folders
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

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as UserCell;
            var element = content.DataContext as ChatFilterElement;

            content.UpdateChatFilter(ViewModel.ClientService, element);
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
            var chat = element.DataContext as ChatFilterElement;

            flyout.CreateFlyoutItem(viewModel.RemoveIncludeCommand, chat, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });

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
            var chat = element.DataContext as ChatFilterElement;

            flyout.CreateFlyoutItem(viewModel.RemoveExcludeCommand, chat, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiList.ItemsSource = Icons.Filters;
            EmojiList.SelectedItem = ViewModel.Icon;

            var flyout = FlyoutBase.GetAttachedFlyout(EmojiButton);
            flyout.ShowAt(EmojiButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        }

        private void EmojiList_ItemClick(object sender, ItemClickEventArgs e)
        {
            FlyoutBase.GetAttachedFlyout(EmojiButton).Hide();

            if (e.ClickedItem is ChatFilterIcon icon)
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

            if (args.ItemContainer.ContentTemplateRoot is FontIcon textBlock && args.Item is ChatFilterIcon icon)
            {
                textBlock.Glyph = Icons.FilterToGlyph(icon).Item1;
            }
        }

        #region Binding

        private string ConvertTitle(ChatFilter filter)
        {
            return filter == null ? Strings.Resources.FilterNew : filter.Title;
        }

        private string ConvertEmoji(ChatFilterIcon icon)
        {
            return Icons.FilterToGlyph(icon).Item1;
        }

        #endregion
    }
}
