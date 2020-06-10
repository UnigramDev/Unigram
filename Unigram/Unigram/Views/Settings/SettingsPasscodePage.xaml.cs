using System;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Windows.Security.Credentials;
using Windows.UI.Xaml;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPasscodePage : HostedPage
    {
        public SettingsPasscodeViewModel ViewModel => DataContext as SettingsPasscodeViewModel;

        public SettingsPasscodePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasscodeViewModel>();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Biometrics.Visibility = await KeyCredentialManager.IsSupportedAsync() ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Binding

        private string ConvertAutolock(int seconds)
        {
            return Locale.FormatAutoLock(seconds);
        }

        #endregion

    }
}
