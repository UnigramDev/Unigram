using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentReceiptPage : HostedPage
    {
        public PaymentReceiptViewModel ViewModel => DataContext as PaymentReceiptViewModel;

        public BindConvert Convert => BindConvert.Current;

        public PaymentReceiptPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentReceiptViewModel>();
        }

        private string ConvertTitle(bool test)
        {
            return (test ? "Test " : string.Empty) + Strings.Resources.PaymentReceipt;
        }

        private string ConvertAddress(Address address)
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
            if (!string.IsNullOrEmpty(address.CountryCode))
            {
                result += address.CountryCode + ", ";
            }
            if (!string.IsNullOrEmpty(address.PostalCode))
            {
                result += address.PostalCode + ", ";
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

        private IList<LabeledPricePart> _prices;
        public IList<LabeledPricePart> Prices
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

    public class ReceiptNavigation
    {
        public long ChatId { get; private set; }
        public long ReceiptMessageId { get; private set; }

        public ReceiptNavigation(long chatId, long receiptMessageId)
        {
            ChatId = chatId;
            ReceiptMessageId = receiptMessageId;
        }
    }
}
