//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPasswordPage : HostedPage
    {
        public SettingsPasswordViewModel ViewModel => DataContext as SettingsPasswordViewModel;

        public SettingsPasswordPage()
        {
            InitializeComponent();
            Title = Strings.TwoStepVerification;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //PrimaryInput.Focus(FocusState.Keyboard);
        }

        //private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        //{
        //    if (e.Key == Windows.System.VirtualKey.Enter)
        //    {
        //        ViewModel.SendCommand.Execute(sender);
        //        e.Handled = true;
        //    }
        //}
    }
}
