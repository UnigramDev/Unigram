//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsPasscodeConfirmPopup : ContentPopup
    {
        private readonly IPasscodeService _passcodeService;
        private readonly DispatcherTimer _retryTimer;

        public SettingsPasscodeConfirmPopup()
        {
            InitializeComponent();

            _passcodeService = TypeResolver.Current.Passcode;

            _retryTimer = new DispatcherTimer();
            _retryTimer.Interval = TimeSpan.FromMilliseconds(100);
            _retryTimer.Tick += Retry_Tick;

            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            Title = Strings.Passcode;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(_passcodeService.IsSimple ? InputScopeNameValue.NumericPin : InputScopeNameValue.Password));
            Field.InputScope = confirmScope;
            Field.MaxLength = _passcodeService.IsSimple ? 4 : int.MaxValue;
        }

        private void Retry_Tick(object sender, object e)
        {
            if (_passcodeService.RetryIn > 0)
            {
                RetryIn.Visibility = Visibility.Visible;
                RetryIn.Text = string.Format(Strings.TooManyTries, Locale.Declension(Strings.R.Seconds, _passcodeService.RetryIn));
            }
            else
            {
                _retryTimer.Stop();
                RetryIn.Visibility = Visibility.Collapsed;
            }
        }

        public string Passcode { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_passcodeService.TryUnlock(Field.Password))
            {
                Unlock();
            }
            else
            {
                Lock();
                args.Cancel = true;
            }
        }

        private void Lock()
        {
            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            VisualUtilities.ShakeView(Field);
            Field.Password = string.Empty;
        }

        private void Unlock()
        {
            _retryTimer.Stop();
            Passcode = Field.Password;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Confirm_Changed(object sender, RoutedEventArgs e)
        {
            if (_passcodeService.IsSimple && Field.Password.Length == 4)
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9) { }
            else if (e.Key is >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9) { }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Hide(ContentDialogResult.Primary);
                e.Handled = true;
            }
            else
            {
                e.Handled = _passcodeService.IsSimple;
            }
        }
    }
}
