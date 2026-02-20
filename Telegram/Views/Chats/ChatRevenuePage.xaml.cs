//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Globalization;
using Telegram.Charts;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Cells.Revenue;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatRevenuePage : HostedPage
    {
        public ChatRevenueViewModel ViewModel => DataContext as ChatRevenueViewModel;

        public ChatRevenuePage()
        {
            InitializeComponent();
            Title = Strings.Monetization;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            StarsRoot.DataContext = ViewModel.Stars;
            StarsRoot.OnNavigatedTo(e);
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAmount(ViewModel.AvailableAmount);
            UpdateAvailability(ViewModel.Availability);

            FooterInfo.Text = string.Format(Strings.MonetizationInfo, 50);
            FooterInfo.Visibility = ViewModel.Chat.Type is ChatTypeSupergroup
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            StarsRoot.OnNavigatedFrom(e);
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.AvailableAmount))
            {
                UpdateAmount(ViewModel.AvailableAmount);
            }
            else if (e.PropertyName == nameof(ViewModel.Availability))
            {
                UpdateAvailability(ViewModel.Availability);
            }
        }

        public void UpdateAvailability(ChatRevenueAvailability availability)
        {
            var crypto = availability is ChatRevenueAvailability.Crypto or ChatRevenueAvailability.CryptoAndStars
                ? Visibility.Visible
                : Visibility.Collapsed;
            var stars = availability is ChatRevenueAvailability.Stars or ChatRevenueAvailability.CryptoAndStars
                ? Visibility.Visible
                : Visibility.Collapsed;

            CryptoRoot.Visibility = crypto;
            StarsRoot.Visibility = stars;

            AvailableCrypto.Visibility = crypto;
            AvailableStars.Visibility = stars;

            PreviousCrypto.Visibility = crypto;
            PreviousStars.Visibility = stars;

            TotalCrypto.Visibility = crypto;
            TotalStars.Visibility = stars;

            var column = availability == ChatRevenueAvailability.Stars ? 0 : 1;

            Grid.SetColumn(AvailableStars, column);
            Grid.SetColumn(PreviousStars, column);
            Grid.SetColumn(TotalStars, column);
        }

        public void UpdateAmount(CryptoAmount value)
        {
            if (value == null)
            {
                return;
            }

            var doubleAmount = Formatter.Amount(value.CryptocurrencyAmount, value.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            CryptocurrencyAmountLabel.Text = stringAmount[0];
            CryptocurrencyDecimalLabel.Text = string.Format(".{0}", decimalAmount.PadRight(2, '0'));

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(value.CryptocurrencyAmount * value.UsdRate), "USD"));
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
            if (e.ClickedItem is ChatRevenueTransaction info1)
            {
                ViewModel.ShowTransaction(info1);
            }
            else if (e.ClickedItem is StarTransaction info2)
            {
                ViewModel.Stars.ShowTransaction(info2);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatRevenueTransactionCell cell1 && args.Item is ChatRevenueTransaction info1)
            {
                cell1.UpdateInfo(info1);
                args.Handled = true;
            }
            else if (args.ItemContainer.ContentTemplateRoot is StarTransactionCell cell2 && args.Item is StarTransaction info2)
            {
                cell2.UpdateInfo(ViewModel.ClientService, info2);
                args.Handled = true;
            }
        }

        #region Binding

        private string ConvertRequiredLevel(int value, UIElement element)
        {
            if (value > 0)
            {
                element?.Visibility = Visibility.Visible;

                return Icons.LockClosedFilled12 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
            }
            else
            {
                element?.Visibility = Visibility.Collapsed;

                return string.Empty;
            }
        }

        private string ConvertTransferInfo(bool canWithdraw, bool owner)
        {
            if (owner)
            {
                return canWithdraw
                    ? Strings.MonetizationBalanceInfo
                    : Strings.MonetizationBalanceInfoNotAvailable;
            }

            return string.Empty;
        }

        #endregion
    }

    public partial class ChatRevenueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChatRevenueTransactionTemplate { get; set; }

        public DataTemplate StarTransactionTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                ChatRevenueTransaction => ChatRevenueTransactionTemplate,
                StarTransaction => StarTransactionTemplate,
                _ => null
            };
        }
    }
}
