using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPhoneSentCodePage : Page
    {
        public SettingsPhoneSentCodeViewModel ViewModel => DataContext as SettingsPhoneSentCodeViewModel;

        public SettingsPhoneSentCodePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPhoneSentCodeViewModel>();
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

        private string ConvertType(AuthenticationCodeInfo codeInfo, string number)
        {
            if (codeInfo == null)
            {
                return null;
            }

            switch (codeInfo.Type)
            {
                case AuthenticationCodeTypeTelegramMessage appType:
                    return Strings.Resources.SentAppCode;
                case AuthenticationCodeTypeSms smsType:
                    return string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(number));
            }

            return null;
        }

        #endregion
    }
}
