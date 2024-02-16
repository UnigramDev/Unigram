//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordEmailAddressPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly Function _function;

        private bool _skip;

        public SettingsPasswordEmailAddressPopup(IClientService clientService, Function function)
        {
            InitializeComponent();

            _clientService = clientService;
            _function = function;

            PrimaryButtonText = function is SetPassword ? string.Empty : Strings.Next;
            SecondaryButtonText = Strings.Cancel;
        }

        public PasswordState PasswordState { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                var address = Field.Text;

                if (string.IsNullOrEmpty(address) || !address.IsValidEmailAddress())
                {
                    if (_function is not SetPassword || !_skip)
                    {
                        VisualUtilities.ShakeView(Field);
                        args.Cancel = true;
                        return;
                    }
                }

                IsPrimaryButtonEnabled = false;

                if (_function is SetRecoveryEmailAddress setRecoveryEmailAddress)
                {
                    setRecoveryEmailAddress.NewRecoveryEmailAddress = address;
                }
                else if (_function is SetPassword setPassword)
                {
                    setPassword.NewRecoveryEmailAddress = address;
                    setPassword.SetRecoveryEmailAddress = true;
                }

                var deferral = args.GetDeferral();

                var response = await _clientService.SendAsync(_function);
                if (response is PasswordState passwordState)
                {
                    PasswordState = passwordState;
                }
                else if (response is Error error)
                {
                    Logger.Error(error.Message);

                    if (error.MessageEquals(ErrorType.EMAIL_INVALID))
                    {
                        await MessagePopup.ShowAsync(target: null, Strings.EmailAddressInvalid, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.EMAIL_NOT_ALLOWED))
                    {
                        await MessagePopup.ShowAsync(target: null, Strings.EmailNotAllowed, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                    }

                    args.Cancel = true;
                }

                deferral.Complete();
            }
            catch
            {
                // Deferral already completed.
            }

            IsPrimaryButtonEnabled = true;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            PrimaryButtonText = string.IsNullOrEmpty(Field.Text) && _function is SetPassword
                ? string.Empty
                : Strings.Next;
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Hide(ContentDialogResult.Primary);
                e.Handled = true;
            }
        }

        private async void Skip_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await MessagePopup.ShowAsync(target: null, Strings.YourEmailSkipWarningText, Strings.YourEmailSkipWarning, Strings.YourEmailSkip, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                _skip = true;
                Hide(ContentDialogResult.Primary);
            }
        }
    }
}
