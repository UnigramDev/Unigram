using System;
using Telegram.Converters;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public class CryptoAmount
    {
        public string Currency { get; set; }

        public long Amount { get; set; }

        public string Cryptocurrency { get; set; }

        public long CryptocurrencyAmount { get; set; }
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

            var doubleAmount = value.CryptocurrencyAmount / 100.0;
            var integerAmount = Math.Truncate(doubleAmount);
            var decimalAmount = (int)((decimal)doubleAmount % 1 * 100);

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
            CryptocurrencyDecimalLabel.Text = string.Format(".{0:N0}", decimalAmount);

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount(value.Amount, value.Currency));
        }
    }
}
