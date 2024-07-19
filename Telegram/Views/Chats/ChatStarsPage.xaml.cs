using Telegram.Charts;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Cells.Revenue;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatStarsPage : HostedPage
    {
        public ChatStarsViewModel ViewModel => DataContext as ChatStarsViewModel;

        public ChatStarsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Withdraw.DataContext = DataContext;
            Withdraw.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Withdraw.OnNavigatedFrom(e);
        }

        private void OnElementPrepared(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var root = sender as ChartCell;
            var data = args.NewValue as ChartViewData;

            if (root == null)
            {
                return;
            }

            var header = root.Items[0] as ChartHeaderView;
            var border = root.Items[1] as AspectView;
            var checks = root.Items[2] as WrapPanel;

            root.Header = data?.title ?? string.Empty;
            border.Children.Clear();
            border.Constraint = data;

            root.UpdateData(data);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.ShowTransaction(e.ClickedItem as StarTransaction);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is StarTransactionCell cell && args.Item is StarTransaction info)
            {
                cell.UpdateInfo(ViewModel.ClientService, info);
                args.Handled = true;
            }
        }

        #region Binding

        #endregion
    }
}
