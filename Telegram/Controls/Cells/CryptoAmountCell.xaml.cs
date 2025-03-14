using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using Telegram.Converters;
using Telegram.Native;

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
            var integerAmount = long.Parse(stringAmount[0]);
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            var culture = new CultureInfo(NativeUtils.GetCurrentCulture());
            var separator = culture.NumberFormat.NumberDecimalSeparator;

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
            CryptocurrencyDecimalLabel.Text = string.Format("{0}{1}", separator, decimalAmount.PadRight(2, '0'));

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(value.CryptocurrencyAmount * value.UsdRate), "USD"));
        }
    }
}
