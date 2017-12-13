using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Views;
using Unigram.ViewModels.Payments;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentReceiptPage : Page
    {
        public PaymentReceiptViewModel ViewModel => DataContext as PaymentReceiptViewModel;

        public BindConvert Convert => BindConvert.Current;

        public PaymentReceiptPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentReceiptViewModel>();
        }

        private string ConvertTitle(bool test)
        {
            return (test ? "Test " : string.Empty) + Strings.Android.PaymentReceipt;
        }

        private string ConvertAddress(TLPostAddress address)
        {
            if (address == null)
            {
                return string.Empty;
            }

            var result = string.Empty;
            if (!string.IsNullOrEmpty(address.StreetLine1))
            {
                result += address.StreetLine1 + ", ";
            }
            if (!string.IsNullOrEmpty(address.StreetLine2))
            {
                result += address.StreetLine2 + ", ";
            }
            if (!string.IsNullOrEmpty(address.City))
            {
                result += address.City + ", ";
            }
            if (!string.IsNullOrEmpty(address.State))
            {
                result += address.State + ", ";
            }
            if (!string.IsNullOrEmpty(address.CountryIso2))
            {
                result += address.CountryIso2 + ", ";
            }
            if (!string.IsNullOrEmpty(address.PostCode))
            {
                result += address.PostCode + ", ";
            }

            return result.Trim(',', ' ');
        }
    }

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
            get
            {
                return _currency;
            }
            set
            {
                _currency = value;
                UpdateRowsAndColumns();
            }
        }

        private TLVector<TLLabeledPrice> _prices;
        public TLVector<TLLabeledPrice> Prices
        {
            get
            {
                return _prices;
            }
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
                    amount.Text = BindConvert.Current.FormatAmount(price.Amount, _currency);
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
