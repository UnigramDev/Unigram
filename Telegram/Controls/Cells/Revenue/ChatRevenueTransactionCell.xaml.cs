using System;
using System.Globalization;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class ChatRevenueTransactionCell : Grid
    {
        public ChatRevenueTransactionCell()
        {
            InitializeComponent();
        }

        public void UpdateInfo(ChatRevenueTransaction info)
        {
            if (info.Type is ChatRevenueTransactionTypeEarnings earnings)
            {
                Reason.Text = Strings.MonetizationTransactionProceed;
                Date.Text = string.Format("{0} - {1}", Formatter.DateAt(earnings.StartDate), Formatter.DateAt(earnings.EndDate));
                Date.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
            }
            else if (info.Type is ChatRevenueTransactionTypeWithdrawal withdrawal)
            {
                Reason.Text = Strings.MonetizationTransactionWithdraw;

                if (withdrawal.State is RevenueWithdrawalStateSucceeded succeeded)
                {
                    Date.Text = Formatter.DateAt(succeeded.Date);
                    Date.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else if (withdrawal.State is RevenueWithdrawalStatePending)
                {
                    Date.Text = Strings.MonetizationTransactionPending;
                    Date.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
                }
                else if (withdrawal.State is RevenueWithdrawalStateFailed)
                {
                    Date.Text = string.Format("{0} - {1}", Formatter.DateAt(withdrawal.WithdrawalDate), Strings.MonetizationTransactionNotCompleted);
                    Date.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;
                }
            }
            else if (info.Type is ChatRevenueTransactionTypeRefund refund)
            {
                Reason.Text = Strings.MonetizationTransactionRefund;
                Date.Text = Formatter.DateAt(refund.RefundDate);
                Date.Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush;
            }
            else
            {
                Date.Text = "???";
            }

            var doubleAmount = Formatter.Amount(Math.Abs(info.CryptocurrencyAmount), info.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var integerAmount = long.Parse(stringAmount[0]);
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            Symbol.Text = info.CryptocurrencyAmount < 0 ? "-" : "+";
            Amount.Text = integerAmount.ToString("N0");
            Decimal.Text = string.Format(".{0}", decimalAmount.PadRight(2, '0'));

            Value.Foreground = BootStrapper.Current.Resources[info.CryptocurrencyAmount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
        }
    }
}
