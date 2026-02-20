//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Globalization;
using Telegram.Converters;
using Telegram.Native;
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

            var doubleAmount = Formatter.Amount(value.CryptocurrencyAmount, value.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            var culture = new CultureInfo(NativeUtils.GetCurrentCulture());
            var separator = culture.NumberFormat.NumberDecimalSeparator;

            CryptocurrencyAmountLabel.Text = stringAmount[0];
            CryptocurrencyDecimalLabel.Text = string.Format("{0}{1}", separator, decimalAmount.PadRight(2, '0'));

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(value.CryptocurrencyAmount * value.UsdRate), "USD"));
        }
    }
}
