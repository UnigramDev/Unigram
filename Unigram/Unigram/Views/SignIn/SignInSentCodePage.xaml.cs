using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.SignIn;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInSentCodePage : Page
    {
        public SignInSentCodeViewModel ViewModel => DataContext as SignInSentCodeViewModel;

        public SignInSentCodePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInSentCodeViewModel>();
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

        private string ConvertType(AuthenticationCodeInfo codeInfo)
        {
            if (codeInfo == null)
            {
                return null;
            }

            switch (codeInfo.Type)
            {
                case AuthenticationCodeTypeTelegramMessage:
                    return Strings.Resources.SentAppCode;
                case AuthenticationCodeTypeSms:
                    return string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(codeInfo.PhoneNumber));
            }

            return null;
        }

        #endregion

        private readonly State _current;
        private State _next;
        private double _stop;

        private void PrimaryInput_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var index = PrimaryInput.SelectionStart + PrimaryInput.SelectionLength - 1;
            if (index >= 0)
            {
                var rect = PrimaryInput.GetRectFromCharacterIndex(PrimaryInput.SelectionStart + PrimaryInput.SelectionLength - 1, false);
                var position = rect.X / PrimaryInput.ActualWidth;

                var frame = (int)(Math.Min(1, Math.Max(0, position)) * 140);

                _stop = (20d + frame) / 179d;
                _next = State.Tracking;
                Header.Play(/*_current == State.Tracking &&*/ Header.Offset > _stop);
            }
            else
            {
                _stop = 0;
                _next = State.Idle;
                Header.Play(true);
            }
        }

        private void OnPositionChanged(object sender, double e)
        {
            System.Diagnostics.Debug.WriteLine("Current: {0}, next: {1}, e: {2}, stop: {3}", _current, _next, e, _stop);

            if (e == _stop /*&& _current == State.Tracking && _next == State.Tracking*/)
            {
                this.BeginOnUIThread(Header.Pause);
            }
            //else if (_current == State.Tracking && _next == State.Idle && (e == 0 || e == 1))
            //{
            //    this.BeginOnUIThread(() =>
            //    {
            //        _current = State.Idle;

            //        Header.Pause();

            //        Header.IsLoopingEnabled = true;
            //        Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyIdle.tgs");
            //        Header.Play();
            //    });
            //}
            //else if (_current == State.Idle && _next == State.Tracking && (e == 0 || e == 1))
            //{
            //    this.BeginOnUIThread(() =>
            //    {
            //        _current = State.Tracking;

            //        Header.Pause();

            //        Header.IsLoopingEnabled = false;
            //        Header.Source = new Uri("ms-appx:///Assets/Animations/TwoFactorSetupMonkeyTracking.tgs");
            //        Header.Play();
            //    });
            //}
        }

        enum State
        {
            Idle,
            Tracking
        }
    }
}
