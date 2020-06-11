using Unigram.Common;
using Unigram.ViewModels.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep4Page : HostedPage
    {
        public PaymentFormStep4ViewModel ViewModel => DataContext as PaymentFormStep4ViewModel;

        public PaymentFormStep4Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<PaymentFormStep4ViewModel>();

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
                case "PASSWORD_HASH_INVALID":
                    VisualUtilities.ShakeView(FieldPassword);
                    break;
            }
        }
    }
}
