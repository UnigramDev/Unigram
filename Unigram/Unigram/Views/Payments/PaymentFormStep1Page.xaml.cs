using Unigram.Common;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep1Page : HostedPage
    {
        public PaymentFormStep1ViewModel ViewModel => DataContext as PaymentFormStep1ViewModel;

        public PaymentFormStep1Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormStep1ViewModel>();
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
    }
}
