//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Security.Credentials.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Host
{
    public sealed partial class PasscodeWindow : WindowContent
    {
        private readonly IPasscodeService _passcodeService;
        private readonly bool _biometrics;

        private readonly DispatcherTimer _retryTimer;

        private int _length;

        public PasscodeWindow(WindowContext window, bool biometrics)
            : base(window, false)
        {
            InitializeComponent();

            _passcodeService = LifetimeService.Current.Passcode;
            _biometrics = biometrics;

            // White over the wallpaper; WindowContext.Unlock puts the previous ones back.
            UseDarkCaptionButtons();

            _retryTimer = new DispatcherTimer();
            _retryTimer.Interval = TimeSpan.FromMilliseconds(100);
            _retryTimer.Tick += Retry_Tick;

            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            var clientService = LifetimeService.Current.ActiveItem?.Resolve<IClientService>();
            if (clientService != null)
            {
                Wallpaper.Update(clientService);
            }
            else
            {
                Wallpaper.Update(light: null, dark: null);
            }

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(_passcodeService.IsSimple ? InputScopeNameValue.NumericPin : InputScopeNameValue.Password));

            Field.InputScope = confirmScope;
            Field.MaxLength = _passcodeService.IsSimple ? 4 : int.MaxValue;

            if (_passcodeService.IsSimple)
            {
                Field.Opacity = 0;
                AutomationProperties.SetName(Field, Strings.EnterPIN);

                Dots.Visibility = Visibility.Visible;
                Enter.Visibility = Visibility.Collapsed;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
                Biometrics.Margin = new Thickness(0, 20, 0, 0);
            }
        }

        protected override UIElement TitleBarElement => TitleBar;

        private void Retry_Tick(object sender, object e)
        {
            if (_passcodeService.RetryIn > 0)
            {
                RetryPanel.Visibility = Visibility.Visible;
                RetryIn.Text = string.Format(Strings.TooManyTries, Locale.Declension(Strings.R.Seconds, _passcodeService.RetryIn));
            }
            else
            {
                _retryTimer.Stop();
                RetryPanel.Visibility = Visibility.Collapsed;
            }

            if (_passcodeService.IsSimple)
            {
                Subtitle.Visibility = RetryPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void Field_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            if (_passcodeService.IsLocked)
            {
                Logger.Info("canceled");

                args.TryCancel();
            }
            else
            {
                Logger.Info();
            }
        }

        private void Field_TextChanged(object sender, RoutedEventArgs e)
        {
            // One step per character typed or erased, none for a paste. A wrong code turns it back
            // once, in Lock, which clears the field without it counting as erasing.
            var length = Field.Password.Length;
            if (length == _length + 1)
            {
                Wallpaper.UpdateBackground();
            }
            else if (length == _length - 1)
            {
                Wallpaper.RevertBackground();
            }

            _length = length;

            if (_passcodeService.IsSimple)
            {
                for (int i = 0; i < Dots.Children.Count; i++)
                {
                    if (Dots.Children[i] is Border dot)
                    {
                        dot.Background = i < length ? dot.BorderBrush : null;
                    }
                }

                if (length == 4)
                {
                    TryUnlock();
                }
            }
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                TryUnlock();
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            TryUnlock();
        }

        private void TryUnlock()
        {
            if (_passcodeService.TryUnlock(Field.Password))
            {
                Unlock();
            }
            else
            {
                Lock();
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            Field.LosingFocus += Field_LosingFocus;

            UserConsentVerifierAvailability availability;
            try
            {
                availability = await UserConsentVerifier.CheckAvailabilityAsync();
            }
            catch
            {
                availability = UserConsentVerifierAvailability.DeviceBusy;
            }

            if (_passcodeService.IsBiometricsEnabled && availability == UserConsentVerifierAvailability.Available)
            {
                Biometrics.Visibility = Visibility.Visible;

                if (_biometrics)
                {
                    Biometrics_Click(null, null);
                }
            }
            else
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            Field.LosingFocus -= Field_LosingFocus;

            _retryTimer.Stop();
        }

        protected override void OnWindowActivated(bool active)
        {
            // Unlock() used to unsubscribe from Activated; the base owns that subscription
            // now, so the handler bows out on the state Unlock() already sets.
            if (!_passcodeService.IsLocked)
            {
                return;
            }

            if (active)
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void Lock()
        {
            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            VisualUtilities.ShakeView(FieldPanel);
            Wallpaper.RevertBackground();

            _length = 0;
            Field.Password = string.Empty;
        }

        private void Unlock()
        {
            Field.LosingFocus -= Field_LosingFocus;

            _passcodeService.Unlock();
            _retryTimer.Stop();
        }

        private async void Biometrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await Window.RequestUserConsentAsync(Strings.PasscodeWindowsHello);
                if (result == UserConsentVerificationResult.Verified)
                {
                    Unlock();
                }
                else
                {
                    Logger.Error(result);
                    Field.Focus(FocusState.Keyboard);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Field.Focus(FocusState.Keyboard);
            }
        }

        private async void LogOut_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.AreYouSureLogout, Strings.AppName, Strings.LogOut, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var client in LifetimeService.Current.ResolveAll<IClientService>())
                {
                    client.Send(new LogOut());
                }
            }
        }
    }
}
