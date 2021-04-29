using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentCredentialsPopup : ContentPopup
    {
        public PaymentCredentialsViewModel ViewModel => DataContext as PaymentCredentialsViewModel;

        public SavedCredentials Credentials { get; private set; }

        public PaymentCredentialsPopup(PaymentForm paymentForm)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.Resources.PaymentCardInfo;
            SecondaryButtonText = Strings.Resources.Cancel;

            if (ViewModel.Initialize(paymentForm))
            {
                FindName(nameof(WebPanel));
                View.Navigate(new Uri(paymentForm.Url));
            }
            else
            {
                PrimaryButtonText = Strings.Resources.OK;
            }
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
                case "CARD_NUMBER_INVALID":
                    VisualUtilities.ShakeView(FieldCard);
                    break;
                case "CARD_EXPIRE_DATE_INVALID":
                    VisualUtilities.ShakeView(FieldDate);
                    break;
                case "CARD_HOLDER_NAME_INVALID":
                    VisualUtilities.ShakeView(FieldCardName);
                    break;
                case "CARD_CVC_INVALID":
                    VisualUtilities.ShakeView(FieldCVC);
                    break;
                case "CARD_COUNTRY_INVALID":
                    VisualUtilities.ShakeView(FieldCountry);
                    break;
                case "CARD_ZIP_INVALID":
                    VisualUtilities.ShakeView(FieldPostcode);
                    break;
            }
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramPaymentProxy((title, credentials) =>
            {
                Credentials = new SavedCredentials(credentials, title);
                Hide(ContentDialogResult.Primary);
            }));
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();
            var validated = await ViewModel.ValidateAsync();

            Credentials = validated;

            args.Cancel = validated == null;
            deferral.Complete();
        }
    }
}
