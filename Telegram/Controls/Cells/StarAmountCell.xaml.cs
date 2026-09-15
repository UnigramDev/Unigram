//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class StarAmountCell : UserControl
    {
        public StarAmountCell()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => TextLabel.Text;
            set => TextLabel.Text = value;
        }

        public double UsdRate { get; set; }

        public StarAmount Amount
        {
            set => UpdateAmount(value);
        }

        public void UpdateAmount(StarAmount amount)
        {
            if (amount == null)
            {
                return;
            }

            // Nanostars are billionths of a star, so they have to be placed against that
            // exponent: printed raw, 50,000,000 of them read as ".50000000" rather than ".05".
            var integerAmount = Math.Abs(amount.StarCount);
            var split = Formatter.SplitAmount(BigInteger.Abs(Formatter.Nanostars(amount)), 9, 9);

            CryptocurrencyAmountLabel.Text = split.Integer;
            CryptocurrencyDecimalLabel.Text = split.Fraction;

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(integerAmount * UsdRate), "USD"));
        }
    }
}
