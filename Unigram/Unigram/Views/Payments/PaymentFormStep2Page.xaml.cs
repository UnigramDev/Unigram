using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep2Page : HostedPage
    {
        public PaymentFormStep2ViewModel ViewModel => DataContext as PaymentFormStep2ViewModel;

        public PaymentFormStep2Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormStep2ViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
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

        private IList<ShippingOption> _options;
        public IList<ShippingOption> Options
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



        public ShippingOption Shipping
        {
            get { return (ShippingOption)GetValue(ShippingProperty); }
            set { SetValue(ShippingProperty, value); }
        }

        public static readonly DependencyProperty ShippingProperty =
            DependencyProperty.Register("Shipping", typeof(ShippingOption), typeof(ShippingOptionsPanel), new PropertyMetadata(null));



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
                        Shipping = radio.DataContext as ShippingOption;
                    };

                    Grid.SetRow(radio, i);

                    Children.Add(radio);
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
                }
            }
        }
    }
}
