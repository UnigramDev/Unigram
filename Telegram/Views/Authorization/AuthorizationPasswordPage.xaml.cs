//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Streams;
using Telegram.ViewModels.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Authorization
{
    public sealed partial class AuthorizationPasswordPage : Page
    {
        public AuthorizationPasswordViewModel ViewModel => DataContext as AuthorizationPasswordViewModel;

        public AuthorizationPasswordPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);

            Header.Source = new LocalFileSource("ms-appx:///Assets/Animations/AuthorizationStateWaitPassword.tgs")
            {
                Markers = new Dictionary<string, int>
                {
                    { "Close", 40 },
                    { "CloseToPeek", 40 + 16 },
                }
            };
            Header.Play();
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
                case "PASSWORD_INVALID":
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

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            PrimaryInput.PasswordRevealMode = RevealButton.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Header.Seek(RevealButton.IsChecked == true ? "Close" : "CloseToPeek");
        }
    }
}
