using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Unigram.Common;
using Unigram.ViewModels.SignIn;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignUpPage : Page
    {
        public SignUpViewModel ViewModel => DataContext as SignUpViewModel;

        public SignUpPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignUpViewModel>();
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
