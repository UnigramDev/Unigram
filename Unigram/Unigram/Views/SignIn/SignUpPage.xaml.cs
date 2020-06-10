using Unigram.Common;
using Unigram.ViewModels.SignIn;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignUpPage : Page
    {
        public SignUpViewModel ViewModel => DataContext as SignUpViewModel;

        public SignUpPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignUpViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "FIRSTNAME_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }
    }
}
