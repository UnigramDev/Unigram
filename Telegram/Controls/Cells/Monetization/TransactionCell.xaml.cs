using System;
using Telegram.Converters;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Monetization
{
    public class TransactionInfo
    {
        public int Date { get; set; }

        public int EndDate { get; set; }

        public string DestinationAddress { get; set; }

        public long CryptocurrencyAmount { get; set; }

        public string Cryptocurrency { get; set; }
    }

    public sealed partial class TransactionCell : Grid
    {
        public TransactionCell()
        {
            InitializeComponent();
        }

        public void UpdateInfo(TransactionInfo info)
        {
            if (true || string.IsNullOrEmpty(info.DestinationAddress))
            {
                Reason.Text = Strings.MonetizationTransactionProceed;
                Address.Visibility = Visibility.Collapsed;
            }
            else
            {
                Reason.Text = Strings.MonetizationTransactionWithdraw;
                Address.Visibility = Visibility.Visible;
                Address.Text = info.DestinationAddress.Substring(0, info.DestinationAddress.Length / 2) + Environment.NewLine + info.DestinationAddress.Substring(info.DestinationAddress.Length / 2);
            }

            if (info.EndDate == 0)
            {
                Date.Text = Formatter.DateAt(info.Date);
            }
            else
            {
                Date.Text = Formatter.DateAt(info.Date) + " - " + Formatter.DateAt(info.EndDate);
            }

            var doubleAmount = Math.Abs(info.CryptocurrencyAmount / 100.0);
            var integerAmount = Math.Truncate(doubleAmount);
            var decimalAmount = (int)((decimal)doubleAmount % 1 * 100);

            Amount.Text = (info.CryptocurrencyAmount < 0 ? "-" : "+") + integerAmount.ToString("N0");
            Decimal.Text = string.Format(".{0:N0} {1}", decimalAmount, info.Cryptocurrency);

            Value.Foreground = BootStrapper.Current.Resources[info.CryptocurrencyAmount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
        }
    }
}
