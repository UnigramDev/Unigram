using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentAddressPopup : ContentPopup
    {
        public PaymentAddressViewModel ViewModel => DataContext as PaymentAddressViewModel;

        public ValidatedOrderInfo ValidatedInfo { get; private set; }

        public PaymentAddressPopup(long chatId, long messageId, Invoice invoice, OrderInfo info)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentAddressViewModel>();

            Title = Strings.Resources.PaymentShippingInfo;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            ViewModel.Initialize(chatId, messageId, invoice, info);
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
            var deferral = args.GetDeferral();
            var validated = await ViewModel.ValidateAsync();

            ValidatedInfo = validated;

            args.Cancel = validated == null;
            deferral.Complete();
        }
    }
}
