//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class ChatRevenueTransactionCell : Grid
    {
        private readonly Brush _received;

        public ChatRevenueTransactionCell()
        {
            InitializeComponent();

            // Read once: this runs while the list scrolls, and a resource lookup walks the tree.
            // Safe to hold, being made of a colour that follows the theme - unlike the brush the
            // amount is drawn in otherwise, which the theme replaces rather than repaints.
            _received = Resources["AmountReceivedBrush"] as Brush;
        }

        public void UpdateInfo(ChatRevenueTransaction info)
        {
            if (info.Type is ChatRevenueTransactionTypeSponsoredMessageEarnings earnings)
            {
                Reason.Text = Strings.MonetizationTransactionProceed;
                Date.Text = string.Format("{0} - {1}", Formatter.DateAt(earnings.StartDate), Formatter.DateAt(earnings.EndDate));
                Date.ClearValue(TextBlock.ForegroundProperty);
            }
            else if (info.Type is ChatRevenueTransactionTypeFragmentWithdrawal withdrawal)
            {
                Reason.Text = Strings.MonetizationTransactionWithdraw;

                if (withdrawal.State is RevenueWithdrawalStateSucceeded succeeded)
                {
                    Date.Text = Formatter.DateAt(succeeded.Date);
                    Date.ClearValue(TextBlock.ForegroundProperty);
                }
                else if (withdrawal.State is RevenueWithdrawalStatePending)
                {
                    Date.Text = Strings.MonetizationTransactionPending;
                    Date.ClearValue(TextBlock.ForegroundProperty);
                }
                else if (withdrawal.State is RevenueWithdrawalStateFailed)
                {
                    Date.Text = string.Format("{0} - {1}", Formatter.DateAt(withdrawal.WithdrawalDate), Strings.MonetizationTransactionNotCompleted);
                    Date.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;
                }
            }
            else if (info.Type is ChatRevenueTransactionTypeFragmentRefund refund)
            {
                Reason.Text = Strings.MonetizationTransactionRefund;
                Date.Text = Formatter.DateAt(refund.RefundDate);
                Date.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                Date.Text = "???";
            }

            UpdateAmount(info);
        }

        private void UpdateAmount(ChatRevenueTransaction info)
        {
            // TDLib signs the amount rather than naming a direction: negative is outgoing.
            var sent = info.CryptocurrencyAmount < 0;

            // Split on the exact integer TDLib sent rather than on a double: the old round trip
            // through double.ToString could reach scientific notation, and splitting "1E-07" on
            // '.' gives one part and no decimals at all.
            var exponent = Formatter.GetAmountExponent(info.Cryptocurrency);
            var amount = Formatter.SplitAmount(BigInteger.Abs(info.CryptocurrencyAmount), exponent, exponent);

            AmountInteger.Text = (sent ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;

            if (sent || _received == null)
            {
                // Back to the colour the style gives it. Only a local value is cleared, so the
                // amount must never be given a Foreground in the markup - that is a local value
                // too, and this would take it away for good.
                Amount.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                Amount.Foreground = _received;
            }
        }
    }
}
