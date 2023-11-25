//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Payments
{
    public sealed partial class PaymentAddressPopup : ContentPopup
    {
        public PaymentAddressViewModel ViewModel => DataContext as PaymentAddressViewModel;

        public ValidatedOrderInfo ValidatedInfo { get; private set; }

        public PaymentAddressPopup(InputInvoice inputInvoice, Invoice invoice, OrderInfo info)
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<PaymentAddressViewModel>();

            Title = Strings.PaymentShippingInfo;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            ViewModel.Initialize(inputInvoice, invoice, info);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "REQ_INFO_NAME_INVALID":
                    VisualUtilities.ShakeView(FieldName);
                    break;
                case "REQ_INFO_PHONE_INVALID":
                    VisualUtilities.ShakeView(FieldPhone);
                    break;
                case "REQ_INFO_EMAIL_INVALID":
                    VisualUtilities.ShakeView(FieldEmail);
                    break;
                case "ADDRESS_COUNTRY_INVALID":
                    VisualUtilities.ShakeView(FieldCountry);
                    break;
                case "ADDRESS_CITY_INVALID":
                    VisualUtilities.ShakeView(FieldCity);
                    break;
                case "ADDRESS_POSTCODE_INVALID":
                    VisualUtilities.ShakeView(FieldPostcode);
                    break;
                case "ADDRESS_STATE_INVALID":
                    VisualUtilities.ShakeView(FieldState);
                    break;
                case "ADDRESS_STREET_LINE1_INVALID":
                    VisualUtilities.ShakeView(FieldStreet1);
                    break;
                case "ADDRESS_STREET_LINE2_INVALID":
                    VisualUtilities.ShakeView(FieldStreet2);
                    break;
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPrimaryButtonEnabled = false;

            var deferral = args.GetDeferral();
            var validated = await ViewModel.ValidateAsync();

            ValidatedInfo = validated;

            args.Cancel = validated == null;
            deferral.Complete();

            IsPrimaryButtonEnabled = true;
        }
    }
}
