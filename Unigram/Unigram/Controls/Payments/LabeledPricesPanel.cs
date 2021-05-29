using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Payments
{
    public class LabeledPricesPanel : Grid
    {
        public LabeledPricesPanel()
        {
            ColumnDefinitions.Add(new ColumnDefinition());
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        }

        private string _currency;
        public string Currency
        {
            get => _currency;
            set
            {
                _currency = value;
                UpdateRowsAndColumns();
            }
        }

        private IList<LabeledPricePart> _prices;
        public IList<LabeledPricePart> Prices
        {
            get => _prices;
            set
            {
                _prices = value;
                UpdateRowsAndColumns();
            }
        }

        private void UpdateRowsAndColumns()
        {
            if (_prices != null && _currency != null)
            {
                Children.Clear();
                RowDefinitions.Clear();

                for (int i = 0; i < _prices.Count; i++)
                {
                    var price = _prices[i];

                    var label = new TextBlock();
                    label.Style = App.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    label.Text = price.Label;
                    label.Margin = new Thickness(12, 4, 0, 4);

                    var amount = new TextBlock();
                    amount.Style = App.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    amount.Text = Converter.FormatAmount(price.Amount, _currency);
                    amount.Margin = new Thickness(8, 4, 12, 4);
                    amount.TextAlignment = TextAlignment.Right;

                    Grid.SetRow(label, i);
                    Grid.SetRow(amount, i);
                    Grid.SetColumn(amount, 1);

                    Children.Add(label);
                    Children.Add(amount);
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
                }
            }
        }
    }
}
