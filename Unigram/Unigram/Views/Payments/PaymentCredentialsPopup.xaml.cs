//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Payments;

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

            var url = ViewModel.Initialize(paymentForm);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                //View.Navigate(new Uri(url));
            }
            else
            {
                PrimaryButtonText = Strings.Resources.OK;
                SecondaryButtonText = Strings.Resources.Cancel;
            }
        }

        public PaymentCredentialsPopup(PaymentForm paymentForm, PaymentOption paymentOption)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.Resources.PaymentCardInfo;

            var url = ViewModel.Initialize(paymentForm, paymentOption);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                //View.Navigate(new Uri(url));
            }
            else
            {
                SecondaryButtonText = Strings.Resources.Cancel;
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

        //private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        //{
        //    sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramPaymentProxy((title, credentials) =>
        //    {
        //        Credentials = new SavedCredentials(credentials, title);
        //        Hide(ContentDialogResult.Primary);
        //    }));
        //}

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Credentials == null)
            {
                var deferral = args.GetDeferral();
                var validated = await ViewModel.ValidateAsync();

                Credentials = validated;

                args.Cancel = validated == null;
                deferral.Complete();
            }
        }
    }
}
