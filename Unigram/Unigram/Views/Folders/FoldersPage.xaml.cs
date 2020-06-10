using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Folders
{
    public sealed partial class FoldersPage : HostedPage
    {
        public FoldersViewModel ViewModel => DataContext as FoldersViewModel;

        public FoldersPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<FoldersViewModel>();
        }

        private void Items_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var filter = button.DataContext as ChatFilterInfo;

            button.Content = filter.Title;
            button.Command = ViewModel.EditCommand;
            button.CommandParameter = filter;
        }

        private void Recommended_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as Grid;
            var filter = content.DataContext as RecommendedChatFilter;

            var button = content.Children[0] as BadgeButton;
            var add = content.Children[1] as Button;

            button.Content = filter.Filter.Title;
            button.Badge = filter.Description;

            add.Command = ViewModel.RecommendCommand;
            add.CommandParameter = filter;
        }

        private void Item_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFilterInfo;

            flyout.CreateFlyoutItem(ViewModel.DeleteCommand, chat, Strings.Resources.FilterDeleteItem, new FontIcon { Glyph = Icons.Delete });

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
