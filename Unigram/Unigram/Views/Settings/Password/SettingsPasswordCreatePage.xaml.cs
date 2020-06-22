using System;
using Unigram.Common;
using Unigram.ViewModels.Settings.Password;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordCreatePage : HostedPage
    {
        public SettingsPasswordCreateViewModel ViewModel => DataContext as SettingsPasswordCreateViewModel;

        public SettingsPasswordCreatePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordCreateViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        private void Walkthrough_Loaded(object sender, RoutedEventArgs e)
        {
            Walkthrough.Header.PositionChanged += Lottie_PositionChanged;
        }

        private void Field_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateAnimation();
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            Field1.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Field2.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;

            UpdateAnimation();
        }

        private State _current = State.Idle;
        private State _next = State.Idle;

        private void UpdateAnimation()
        {
            var empty = string.IsNullOrEmpty(Field1.Password) && string.IsNullOrEmpty(Field2.Password);
            if (_current != State.Idle && empty)
            {
                if (_current == State.Close)
                {
                    _next = State.Idle;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyClose.tgs");
                    Walkthrough.Header.SetPosition(0.5);
                }
                else if (_current == State.Peek)
                {
                    _next = State.Close;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyPeek.tgs");
                }

                Walkthrough.Header.Play();
            }
            else if (_current != State.Close && Field1.PasswordRevealMode == PasswordRevealMode.Hidden && !empty)
            {
                if (_current == State.Idle)
                {
                    _next = State.Close;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyClose.tgs");
                }
                else if (_current == State.Peek)
                {
                    _next = State.Close;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyPeek.tgs");
                }

                Walkthrough.Header.Play();
            }
            else if (_current != State.Peek && Field1.PasswordRevealMode == PasswordRevealMode.Visible && !empty)
            {
                if (_current == State.Close)
                {
                    _next = State.Peek;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyPeek.tgs");
                }
                else if (_current == State.Idle)
                {
                    _next = State.Close;
                    Walkthrough.Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyClose.tgs");
                }

                Walkthrough.Header.Play();
            }
        }

        private void Lottie_PositionChanged(object sender, double e)
        {
            this.BeginOnUIThread(() =>
            {
                // Idle to Close
                if (e >= 0.5 && _current == State.Idle && _next == State.Close)
                {
                    _current = State.Close;

                    Walkthrough.Header.Pause();
                    UpdateAnimation();
                }
                else if (e >= 1 && _current == State.Close && _next == State.Idle)
                {
                    _current = State.Idle;

                    Walkthrough.Header.Pause();
                    UpdateAnimation();
                }
                // Close to Peek
                else if (e >= 0.5 && _current == State.Close && _next == State.Peek)
                {
                    _current = State.Peek;

                    Walkthrough.Header.Pause();
                    UpdateAnimation();
                }
                else if (e >= 1 && _current == State.Peek && _next == State.Close)
                {
                    _current = State.Close;

                    Walkthrough.Header.Pause();
                    UpdateAnimation();
                }
                // Default
                else if (e >= 0 && e <= 0.5 && _current == State.Idle && _next == State.Idle)
                {
                    Walkthrough.Header.Pause();
                }
            });
        }

        enum State
        {
            Idle,
            Close,
            Peek
        }
    }
}
