using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Unigram.Common;
using Unigram.ViewModels.SignIn;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInRecoveryPage : Page
    {
        public SignInRecoveryViewModel ViewModel => DataContext as SignInRecoveryViewModel;

        public SignInRecoveryPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInRecoveryViewModel>();
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
                case "RECOVERY_CODE_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(sender);
                e.Handled = true;
            }
        }

        #region Binding

        private string ConvertForgot(string pattern)
        {
            return string.Format(Strings.Resources.RestoreEmailTrouble, pattern);
        }

        private string ConvertTrouble(string pattern)
        {
            return string.Format("");
        }

        #endregion
    }
}
