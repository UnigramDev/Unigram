//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Revenue;
using Telegram.Td.Api;
using Telegram.ViewModels.Stars;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Stars
{
    public sealed partial class StarsPage : HostedPage
    {
        public StarsViewModel ViewModel => DataContext as StarsViewModel;

        public StarsPage()
        {
            InitializeComponent();
            Title = Strings.TelegramStars;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StarTransaction transaction)
            {
                ViewModel.ShowPopup(new ReceiptPopup(ViewModel.ClientService, ViewModel.NavigationService, transaction));
            }
            else if (e.ClickedItem is StarSubscription subscription)
            {
                ViewModel.ShowPopup(new SubscriptionPopup(ViewModel.ClientService, ViewModel.NavigationService, subscription));
            }
        }

        private readonly ScrollPosition[] _positions = new ScrollPosition[3];
        private int _index;

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedIndex < 0 || Navigation.SelectedIndex == _index)
            {
                return;
            }

            // Each tab is meant to come back where it was left, and the offset it was left at
            // does not survive a tab whose content is shorter: the view clamps it on the way
            // through and there is nothing to clamp back to on the way in.
            _positions[_index] = ScrollingHost.SaveScrollPosition();

            _index = Navigation.SelectedIndex;
            ViewModel.SelectedIndex = _index;

            // The list keeps the same ItemsSource, so the panel holds its place by index through
            // the patch: a tab being opened for the first time would otherwise inherit the place
            // of the one it was switched from.
            var position = _positions[_index];
            if (position.IsEmpty)
            {
                return;
            }

            ScrollingHost.RestoreScrollPosition(position);
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

        private void Buy_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.NavigationService.ShowPopupAsync(new BuyPopup());
        }

        private async void Gift_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var user = await ChooseChatsPopup.PickUserAsync(ViewModel.ClientService, ViewModel.NavigationService, Strings.GiftStarsTitle, false);
            if (user != null)
            {
                ViewModel.NavigationService.ShowPopup(new BuyPopup(), BuyStarsArgs.ForReceiverUser(user.Id));
            }
        }

        private void Footer_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsTOSLink);
        }

        private void Affiliate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(ChatAffiliatePage), new AffiliateTypeCurrentUser());
        }
    }
}
