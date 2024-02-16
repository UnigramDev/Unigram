//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Payments
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
                    label.Style = Navigation.BootStrapper.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    label.Text = price.Label;
                    label.Margin = new Thickness(12, 4, 0, 4);

                    var amount = new TextBlock();
                    amount.Style = Navigation.BootStrapper.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    amount.Text = Formatter.FormatAmount(price.Amount, _currency);
                    amount.Margin = new Thickness(8, 4, 12, 4);
                    amount.TextAlignment = TextAlignment.Right;

                    SetRow(label, i);
                    SetRow(amount, i);
                    SetColumn(amount, 1);

                    Children.Add(label);
                    Children.Add(amount);
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
                }
            }
        }
    }
}
