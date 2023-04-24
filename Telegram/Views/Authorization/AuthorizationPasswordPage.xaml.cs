//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
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

        private State _current = State.Idle;
        private State _next = State.Close;

        private enum State
        {
            Idle,
            Close,
            Peek
        }

        public AuthorizationPasswordPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
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
            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            if (_current != State.Close && PrimaryInput.PasswordRevealMode == PasswordRevealMode.Hidden)
            {
                if (_current == State.Idle)
                {
                    _current = State.Close;
                    Header.AutoPlay = false;
                    Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyPeek.tgs");
                }
                else if (_current == State.Peek)
                {
                    _next = State.Close;
                    Header.Seek(16d / 32d);
                    Header.Play();
                }
            }
            else if (_current != State.Peek && PrimaryInput.PasswordRevealMode == PasswordRevealMode.Visible)
            {
                _next = State.Peek;
                Header.Seek(0);
                Header.Play();
            }
        }

        private void OnPositionChanged(object sender, double e)
        {
            if (_current == State.Idle && _next == State.Close && e == 50d / 97d)
            {
                Header.Pause();
                this.BeginOnUIThread(UpdateAnimation);
            }
            else if (_current == State.Close && _next == State.Peek && e == 16d / 32d)
            {
                _current = State.Peek;
                Header.Pause();
            }
            else if (_current == State.Peek && _next == State.Close && e == 1)
            {
                _current = State.Close;
                Header.Pause();
            }
        }
    }
}
