using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Td.Api;

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
