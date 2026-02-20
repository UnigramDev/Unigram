//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Globalization;
using Telegram.Converters;
using Telegram.Native;
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

            var integerAmount = Math.Abs(amount.StarCount);
            var decimalAmount = Math.Abs(amount.NanostarCount);

            var culture = new CultureInfo(NativeUtils.GetCurrentCulture());
            var separator = culture.NumberFormat.NumberDecimalSeparator;

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
            CryptocurrencyDecimalLabel.Text = decimalAmount > 0 ? string.Format("{0}{1}", separator, decimalAmount) : string.Empty;

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(integerAmount * UsdRate), "USD"));
        }
    }
}
