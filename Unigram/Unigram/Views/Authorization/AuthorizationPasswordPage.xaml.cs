using System;
using Unigram.Common;
using Unigram.ViewModels.SignIn;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInPasswordPage : Page
    {
        public SignInPasswordViewModel ViewModel => DataContext as SignInPasswordViewModel;

        private State _current = State.Idle;
        private State _next = State.Close;

        private enum State
        {
            Idle,
            Close,
            Peek
        }

        public SignInPasswordPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInPasswordViewModel>();
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
