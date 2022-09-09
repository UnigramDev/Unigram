using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Authorization
{
    public sealed partial class AuthorizationCodePage : Page
    {
        public AuthorizationCodeViewModel ViewModel => DataContext as AuthorizationCodeViewModel;

        public AuthorizationCodePage()
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
                case "SENT_CODE_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        #region Binding

        private string ConvertType(AuthenticationCodeInfo codeInfo)
        {
            if (codeInfo == null)
            {
                return null;
            }

            switch (codeInfo.Type)
            {
                case AuthenticationCodeTypeTelegramMessage:
                    return Strings.Resources.SentAppCode;
                case AuthenticationCodeTypeSms:
                    return string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(codeInfo.PhoneNumber));
            }

            return null;
        }

        #endregion

    }
}
