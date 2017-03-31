using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Core.Dependency;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Payments
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PaymentReceiptPage : Page
    {
        public PaymentReceiptViewModel ViewModel => DataContext as PaymentReceiptViewModel;

        public PaymentReceiptPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentReceiptViewModel>();
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
                ColumnDefinitions.Clear();

                for (int i = 0; i < _prices.Count; i++)
                {
                    var price = _prices[0];

                    var label = new TextBlock();
                    label.Style = App.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    label.Text = price.Label;

                    var amount = new TextBlock();
                    amount.Style = App.Current.Resources["DisabledBodyTextBlockStyle"] as Style;
                    amount.Text = BindConvert.Current.FormatAmount(price.Amount, _currency);
                    amount.Margin = new Thickness(8, 8, 0, 8);

                    Grid.SetRow(label, i);
                    Grid.SetRow(amount, i);
                    Grid.SetColumn(amount, 1);

                    Children.Add(label);
                    Children.Add(amount);
                }
            }
        }
    }
}
