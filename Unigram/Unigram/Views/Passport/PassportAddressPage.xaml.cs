using Unigram.Common;
using Unigram.ViewModels.Passport;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Passport
{
    public sealed partial class PassportAddressPage : Page
    {
        public PassportAddressViewModel ViewModel => DataContext as PassportAddressViewModel;

        public PassportAddressPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PassportAddressViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
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
