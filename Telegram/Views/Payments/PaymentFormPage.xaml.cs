//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Views.Payments
{
    public sealed partial class PaymentFormPage : HostedPage
    {
        public PaymentFormViewModel ViewModel => DataContext as PaymentFormViewModel;

        public PaymentFormPage()
        {
            InitializeComponent();

            VisualUtilities.DropShadow(BuyShadow);
        }

        private string ConvertTitle(bool receipt, bool test)
        {
            if (receipt)
            {
                return Strings.PaymentReceipt + (test ? " (Test)" : string.Empty);
            }

            return Strings.PaymentCheckout + (test ? " (Test)" : string.Empty);
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
                return Strings.Close.ToUpper();
            }

            return string.Format(Strings.PaymentCheckoutPay, Locale.FormatCurrency(amount, currency));
        }

        private void SuggestedTipAmounts_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is TextBlock content && args.Item is long value && ViewModel.PaymentForm.Type is PaymentFormTypeRegular regular)
            {
                content.Text = Formatter.FormatAmount(value, regular.Invoice.Currency);
                args.Handled = true;
            }
        }
    }
}
