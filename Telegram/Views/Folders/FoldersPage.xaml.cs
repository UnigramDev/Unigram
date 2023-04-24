//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
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

        private void Items_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as BadgeButton;
            var folder = button.DataContext as ChatFolderInfo;

            var icon = Icons.ParseFolder(folder.Icon);

            button.Glyph = Icons.FolderToGlyph(icon).Item1;
            button.Content = folder.Title;
            button.CommandParameter = folder;
            button.BorderThickness = new Thickness(0, args.Index == 0 ? 0 : 1, 0, 0);

            var chevron = button.Badge as TextBlock;
            chevron.Text = args.Index > ViewModel.ClientService.Options.ChatFolderCountMax ? Icons.LockClosed : folder.HasMyInviteLinks ? Icons.Link : string.Empty;
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is ChatFolderInfo folder)
            {
                ViewModel.Edit(folder);
            }
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
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderInfo;

            flyout.CreateFlyoutItem(ViewModel.Delete, chat, Strings.FilterDeleteItem, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        #region Binding

        private Visibility ConvertCreate(int count)
        {
            return count < 10 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
