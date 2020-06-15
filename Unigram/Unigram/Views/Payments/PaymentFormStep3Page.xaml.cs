using System;
using Unigram.Common;
using Unigram.Native;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep3Page : HostedPage
    {
        public PaymentFormStep3ViewModel ViewModel => DataContext as PaymentFormStep3ViewModel;

        public PaymentFormStep3Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormStep3ViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
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

            if (e.PropertyName.Equals("Navigate"))
            {
                View.Navigate(new Uri(ViewModel.PaymentForm.Url));
            }
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramPaymentProxy((title, credentials) =>
            {
                ViewModel.NavigateToNextStep(title, credentials, false);
            }));
        }
    }
}
