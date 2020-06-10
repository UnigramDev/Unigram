using Unigram.Common;
using Unigram.ViewModels.Payments;

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

            ViewModel.PropertyChanged += OnPropertyChanged;
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
