//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class TonTransactionCell : Grid
    {
        private readonly Brush _received;

        public TonTransactionCell()
        {
            InitializeComponent();

            // Read once: this runs while the list scrolls, and a resource lookup walks the tree.
            // Safe to hold, being made of a colour that follows the theme - unlike the brush the
            // amount is drawn in otherwise, which the theme replaces rather than repaints.
            _received = Resources["AmountReceivedBrush"] as Brush;
        }

        public void UpdateInfo(IClientService clientService, TonTransaction transaction)
        {
            var info = TransactionInfo.FromTonTransaction(clientService, transaction);

            Photo.Source = info.Photo;
            Title.Text = info.Title;

            if (info.Subtitle != null)
            {
                Subtitle.Text = info.Subtitle;
                Subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
            }

            Date.Text = Formatter.DateAt(transaction.Date);

            if (transaction.IsRefund)
            {
                Date.Text += string.Format(" — {0}", Strings.StarsRefunded);
            }
            else if (transaction.Type is TonTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStateFailed })
            {
                Date.Text += string.Format(" — {0}", Strings.StarsFailed);
            }
            else if (transaction.Type is TonTransactionTypeFragmentWithdrawal { WithdrawalState: RevenueWithdrawalStatePending })
            {
                Date.Text += string.Format(" — {0}", Strings.StarsPending);
            }

            UpdateAmount(transaction.GramAmount);
        }

        private void UpdateAmount(long gramAmount)
        {
            // TDLib signs the amount rather than naming a direction: negative is outgoing.
            var sent = gramAmount < 0;
            var amount = Formatter.TonBalance(Math.Abs(gramAmount));

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
