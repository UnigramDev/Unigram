//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
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
            Title = Strings.Resources.Passcode;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Biometrics.Visibility = await KeyCredentialManager.IsSupportedAsync() ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
