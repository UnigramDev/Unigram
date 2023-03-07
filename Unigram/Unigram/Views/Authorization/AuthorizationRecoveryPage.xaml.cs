//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels.Authorization;

namespace Unigram.Views.Authorization
{
    public sealed partial class AuthorizationRecoveryPage : Page
    {
        public AuthorizationRecoveryViewModel ViewModel => DataContext as AuthorizationRecoveryViewModel;

        public AuthorizationRecoveryPage()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var window = WindowContext.ForXamlRoot(XamlRoot);
            if (window != null)
            {
                window.Window.SetTitleBar(TitleBar);
            }
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
