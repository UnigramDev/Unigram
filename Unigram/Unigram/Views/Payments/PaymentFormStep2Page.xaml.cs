using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Converters;
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
    public sealed partial class PaymentFormStep2Page : Page
    {
        public PaymentFormStep2ViewModel ViewModel => DataContext as PaymentFormStep2ViewModel;

        public PaymentFormStep2Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentFormStep2ViewModel>();
        }
    }

    public class ShippingOptionsPanel : Grid
    {
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

        private TLVector<TLShippingOption> _options;
        public TLVector<TLShippingOption> Options
        {
            get
            {
                return _options;
            }
            set
            {
                _options = value;
                UpdateRowsAndColumns();
            }
        }



        public TLShippingOption Shipping
        {
            get { return (TLShippingOption)GetValue(ShippingProperty); }
            set { SetValue(ShippingProperty, value); }
        }

        public static readonly DependencyProperty ShippingProperty =
            DependencyProperty.Register("Shipping", typeof(TLShippingOption), typeof(ShippingOptionsPanel), new PropertyMetadata(null));



        private void UpdateRowsAndColumns()
        {
            if (_options != null && _currency != null)
            {
                Children.Clear();
                RowDefinitions.Clear();

                Shipping = _options[0];

                for (int i = 0; i < _options.Count; i++)
                {
                    var option = _options[i];

                    var radio = new RadioButton();
                    radio.IsChecked = i == 0;
                    radio.Content = BindConvert.Current.ShippingOption(option, _currency);
                    radio.Margin = new Thickness(12, 4, 12, 4);
                    radio.DataContext = option;
                    radio.Checked += (s, args) =>
                    {
                        Shipping = radio.DataContext as TLShippingOption;
                    };

                    Grid.SetRow(radio, i);

                    Children.Add(radio);
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
                }
            }
        }
    }
}
