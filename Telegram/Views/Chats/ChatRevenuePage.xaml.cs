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
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Withdraw.DataContext = ViewModel.Stars;
            Withdraw.OnNavigatedTo(e);
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAmount(ViewModel.AvailableAmount);

            FooterInfo.Text = string.Format(Strings.MonetizationInfo, 50);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Withdraw.OnNavigatedFrom(e);
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.AvailableAmount))
            {
                UpdateAmount(ViewModel.AvailableAmount);
            }
        }

        public void UpdateAmount(CryptoAmount value)
        {
            if (value == null)
            {
                return;
            }

            var doubleAmount = Formatter.Amount(value.CryptocurrencyAmount, value.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var integerAmount = long.Parse(stringAmount[0]);
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
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
            ViewModel.ShowTransaction(e.ClickedItem as ChatRevenueTransaction);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatRevenueTransactionCell cell && args.Item is ChatRevenueTransaction info)
            {
                cell.UpdateInfo(info);
                args.Handled = true;
            }
        }

        #region Binding

        private string ConvertRequiredLevel(int value, UIElement element)
        {
            if (value > 0)
            {
                element.Visibility = Visibility.Visible;
                return Icons.LockClosedFilled12 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
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
}
