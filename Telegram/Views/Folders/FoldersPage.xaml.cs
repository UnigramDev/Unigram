//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Folders
{
    public sealed partial class FoldersPage : HostedPage
    {
        public FoldersViewModel ViewModel => DataContext as FoldersViewModel;

        public FoldersPage()
        {
            InitializeComponent();
            Title = Strings.Filters;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Edit(e.ClickedItem as ChatFolderInfo);
        }

        private void Recommended_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as Grid;
            var folder = content.DataContext as RecommendedChatFolder;

            var button = content.Children[0] as BadgeButton;
            var add = content.Children[1] as Button;

            var icon = Icons.ParseFolder(folder.Folder);

            button.Glyph = Icons.FolderToGlyph(icon).Item1;
            button.Content = folder.Folder.Title;
            button.Badge = folder.Description;

            add.CommandParameter = folder;
        }

        private void AddRecommended_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is RecommendedChatFolder folder)
            {
                ViewModel.AddRecommended(folder);
            }
        }

        private void Item_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ScrollingHost.ItemFromContainer(sender) as ChatFolderInfo;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.Delete, chat, Strings.FilterDeleteItem, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Item_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is ChatFolderInfo folder)
            {
                AutomationProperties.SetName(args.ItemContainer, folder.Title);

                var glyph = content.Children[0] as TextBlock;
                var presenter = content.Children[1] as ContentPresenter;
                var badge = content.Children[2] as ContentControl;

                var icon = Icons.ParseFolder(folder.Icon);

                glyph.Text = Icons.FolderToGlyph(icon).Item1;
                presenter.Content = folder.Title;
                badge.Content = args.ItemIndex >= ViewModel.ClientService.Options.ChatFolderCountMax
                    ? Icons.LockClosed 
                    : folder.HasMyInviteLinks ? Icons.Link : string.Empty;

                args.Handled = true;
            }
        }

        #endregion

        #region Binding

        private Visibility ConvertCreate(int count)
        {
            return count < 10 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
