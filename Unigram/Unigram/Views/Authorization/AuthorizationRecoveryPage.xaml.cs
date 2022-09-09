using Unigram.Common;
using Unigram.ViewModels.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Authorization
{
    public sealed partial class AuthorizationRecoveryPage : Page
    {
        public AuthorizationRecoveryViewModel ViewModel => DataContext as AuthorizationRecoveryViewModel;

        public AuthorizationRecoveryPage()
        {
            InitializeComponent();
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
