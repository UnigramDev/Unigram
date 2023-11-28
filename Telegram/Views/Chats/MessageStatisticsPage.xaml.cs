//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Charts;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats
{
    public sealed partial class MessageStatisticsPage : HostedPage
    {
        public MessageStatisticsViewModel ViewModel => DataContext as MessageStatisticsViewModel;

        public MessageStatisticsPage()
        {
            InitializeComponent();
            Title = Strings.ViewMessageStatistic;
        }

        #region Binding

        private string ConvertViews(Message message)
        {
            if (message?.InteractionInfo != null)
            {
                return message.InteractionInfo.ViewCount.ToString("N0");
            }

            return string.Empty;
        }

        private string ConvertReactions(Message message)
        {
            if (message?.InteractionInfo != null)
            {
                return message.InteractionInfo.TotalReactions().ToString("N0");
            }

            return string.Empty;
        }

        private string ConvertPublicShares(Message message, int totalCount)
        {
            return totalCount.ToString("N0");
        }

        private string ConvertPrivateShares(Message message, int totalCount)
        {
            if (message?.InteractionInfo != null)
            {
                return string.Format("≈{0:N0}", message.InteractionInfo.ForwardCount - totalCount);
            }

            return string.Empty;
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.OpenPost(e.ClickedItem as Message);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateMessageStatisticsSharer(ViewModel.ClientService, args, OnContainerContentChanging);
            }
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
    }
}
