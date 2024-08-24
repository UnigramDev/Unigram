//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Revenue;
using Telegram.Td.Api;
using Telegram.ViewModels.Stars;
using Telegram.Views.Popups;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class StarsPopup : ContentPopup
    {
        public StarsViewModel ViewModel => DataContext as StarsViewModel;

        public StarsPopup()
        {
            InitializeComponent();
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StarTransaction transaction)
            {
                Hide();
                await ViewModel.ShowPopupAsync(new ReceiptPopup(ViewModel.ClientService, ViewModel.NavigationService, transaction));
                await this.ShowQueuedAsync(XamlRoot);
            }
            else if (e.ClickedItem is StarSubscription subscription)
            {
                Hide();
                await ViewModel.ShowPopupAsync(new SubscriptionPopup(ViewModel.ClientService, ViewModel.NavigationService, subscription));
                await this.ShowQueuedAsync(XamlRoot);
            }
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
            else if (args.ItemContainer.ContentTemplateRoot is StarSubscriptionCell subscriptionCell && args.Item is StarSubscription subscription)
            {
                subscriptionCell.UpdateInfo(ViewModel.ClientService, subscription);
                args.Handled = true;
            }
        }

        public string ConvertCount(long count)
        {
            return count.ToString("N0");
        }

        private void Buy_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Hide();
            ViewModel.NavigationService.ShowPopupAsync(typeof(BuyPopup));
        }

        private async void Gift_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Hide();

            var user = await ChooseChatsPopup.PickUserAsync(ViewModel.ClientService, Strings.GiftStarsTitle, false);
            if (user == null)
            {
                _ = this.ShowQueuedAsync(XamlRoot);
                return;
            }

            await ViewModel.NavigationService.ShowPopupAsync(typeof(BuyPopup), new BuyStarsArgs(user.Id));
        }
    }
}
