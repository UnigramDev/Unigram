//
// Copyright Fela Ameghino 2015-2024
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
    public sealed partial class PaymentCredentialsPopup : ContentPopup
    {
        public PaymentCredentialsViewModel ViewModel => DataContext as PaymentCredentialsViewModel;

        public SavedCredentials Credentials { get; private set; }

        public PaymentCredentialsPopup(PaymentFormTypeRegular paymentForm)
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.PaymentCardInfo;

            var url = ViewModel.Initialize(paymentForm);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                View.Navigate(url);
            }
            else
            {
                PrimaryButtonText = Strings.OK;
                SecondaryButtonText = Strings.Cancel;
            }
        }

        public PaymentCredentialsPopup(PaymentFormTypeRegular paymentForm, PaymentOption paymentOption)
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<PaymentCredentialsViewModel>();

            Title = Strings.PaymentCardInfo;

            var url = ViewModel.Initialize(paymentForm, paymentOption);
            if (url != null)
            {
                FindName(nameof(WebPanel));
                View.Navigate(url);
            }
            else
            {
                SecondaryButtonText = Strings.Cancel;
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

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPrimaryButtonEnabled = false;

            if (Credentials == null)
            {
                var deferral = args.GetDeferral();
                var validated = await ViewModel.ValidateAsync();

                Credentials = validated;

                args.Cancel = validated == null;
                deferral.Complete();
            }

            IsPrimaryButtonEnabled = true;
        }

        private void View_EventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            if (e.EventName == "payment_form_submit")
            {
                var response = e.EventData.GetNamedValue("credentials");
                var title = e.EventData.GetNamedString("title", string.Empty);

                var credentials = response.Stringify();

                Credentials = new SavedCredentials(credentials, title);
                Hide(ContentDialogResult.Primary);
            }
        }
    }
}
