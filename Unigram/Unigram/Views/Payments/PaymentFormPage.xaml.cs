using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Payments;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormPage : HostedPage
    {
        public PaymentFormViewModel ViewModel => DataContext as PaymentFormViewModel;

        public PaymentFormPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormViewModel>();

            DropShadowEx.Attach(BuyShadow);
        }

        private string ConvertTitle(bool receipt, bool test)
        {
            if (receipt)
            {
                return Strings.Resources.PaymentReceipt + (test ? " (Test)" : string.Empty);
            }

            return Strings.Resources.PaymentCheckout + (test ? " (Test)" : string.Empty);
        }

        private ImageSource ConvertPhoto(Photo photo)
        {
            var small = photo?.GetSmall();
            if (small == null)
            {
                Photo.Visibility = Visibility.Collapsed;
                return null;
            }

            Photo.Visibility = Visibility.Visible;
            return PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small);
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

        private string ConvertPay(bool receipt, long amount, string currency)
        {
            if (receipt)
            {
                return Strings.Resources.Close.ToUpper();
            }

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
