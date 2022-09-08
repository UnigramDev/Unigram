using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Authorization
{
    public sealed partial class AuthorizationEmailCodePage : Page
    {
        public AuthorizationEmailCodeViewModel ViewModel => DataContext as AuthorizationEmailCodeViewModel;

        public AuthorizationEmailCodePage()
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
                case "CODE_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void PrimaryInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(null);
                e.Handled = true;
            }
        }

        #region Binding

        private string ConvertType(EmailAddressAuthenticationCodeInfo codeInfo)
        {
            if (codeInfo == null)
            {
                return null;
            }

            return string.Format(Strings.Resources.CheckYourEmailSubtitle, codeInfo.EmailAddressPattern);
        }

        #endregion

    }
}
