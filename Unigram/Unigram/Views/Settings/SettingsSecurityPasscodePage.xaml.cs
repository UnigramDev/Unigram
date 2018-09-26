using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsSecurityPasscodePage : Page
    {
        public SettingsSecurityPasscodeViewModel ViewModel => DataContext as SettingsSecurityPasscodeViewModel;

        public SettingsSecurityPasscodePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsSecurityPasscodeViewModel>();
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
