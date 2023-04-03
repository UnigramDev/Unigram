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
            var filter = button.DataContext as ChatFilterInfo;

            var icon = Icons.ParseFilter(filter.IconName);

            button.Glyph = Icons.FilterToGlyph(icon).Item1;
            button.Content = filter.Title;
            button.Click += Edit_Click;
            button.CommandParameter = filter;
            button.ChevronGlyph = args.Index < ViewModel.ClientService.Options.ChatFilterCountMax ? Icons.ChevronRight : Icons.LockClosed;
            button.BorderThickness = new Thickness(0, args.Index == 0 ? 0 : 1, 0, 0);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is ChatFilterInfo filter)
            {
                ViewModel.Edit(filter);
            }
        }

        private void Recommended_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as Grid;
            var filter = content.DataContext as RecommendedChatFilter;

            var button = content.Children[0] as BadgeButton;
            var add = content.Children[1] as Button;

            var icon = Icons.ParseFilter(filter.Filter);

            button.Glyph = Icons.FilterToGlyph(icon).Item1;
            button.Content = filter.Filter.Title;
            button.Badge = filter.Description;

            add.Click += AddRecommended_Click;
            add.CommandParameter = filter;
        }

        private void AddRecommended_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is RecommendedChatFilter filter)
            {
                ViewModel.AddRecommended(filter);
            }
        }

        private void Item_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFilterInfo;

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
