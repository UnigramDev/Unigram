//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Payments
{
    public sealed partial class PaymentFormPage : HostedPage
    {
        public PaymentFormViewModel ViewModel => DataContext as PaymentFormViewModel;

        public PaymentFormPage()
        {
            InitializeComponent();

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
            return PlaceholderHelper.GetBitmap(ViewModel.ClientService, small);
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
