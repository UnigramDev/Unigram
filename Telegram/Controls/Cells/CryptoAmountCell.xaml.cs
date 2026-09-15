//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Converters;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public partial class CryptoAmount
    {
        public string Cryptocurrency { get; set; }

        public long CryptocurrencyAmount { get; set; }

        public double UsdRate { get; set; }
    }

    public sealed partial class CryptoAmountCell : UserControl
    {
        public CryptoAmountCell()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => TextLabel.Text;
            set => TextLabel.Text = value;
        }

        public CryptoAmount Amount
        {
            set => UpdateAmount(value);
        }

        public void UpdateAmount(CryptoAmount value)
        {
            if (value == null)
            {
                return;
            }

            // Split on the exact integer TDLib sent rather than on a double: the old round trip
            // through double.ToString could reach scientific notation, and splitting "1E-07" on
            // '.' gives one part and no decimals at all.
            var exponent = Formatter.GetAmountExponent(value.Cryptocurrency);
            var amount = Formatter.SplitAmount(value.CryptocurrencyAmount, exponent, exponent);

            CryptocurrencyAmountLabel.Text = amount.Integer;
            CryptocurrencyDecimalLabel.Text = amount.Fraction;

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(value.CryptocurrencyAmount * value.UsdRate), "USD"));
        }
    }
}
