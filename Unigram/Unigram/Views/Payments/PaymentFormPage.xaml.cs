using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormPage : HostedPage
    {
        public PaymentFormViewModel ViewModel => DataContext as PaymentFormViewModel;

        public PaymentFormPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        private string ConvertTitle(bool test)
        {
            return (test ? "Test " : string.Empty) + Strings.Resources.PaymentCheckout;
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

        private string ConvertPay(long amount, string currency)
        {
            return string.Format(Strings.Resources.PaymentCheckoutPay, Locale.FormatCurrency(amount, currency));
        }

        private void SuggestedTipAmounts_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as TextBlock;
            if (args.Item is long value)
            {
                content.Text = Converter.FormatAmount(value, ViewModel.PaymentForm.Invoice.Currency);
            }
        }
    }
}
