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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordConfirmPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly PasswordState _passwordState;

        public SettingsPasswordConfirmPopup(IClientService clientService, PasswordState passwordState)
        {
            InitializeComponent();

            _clientService = clientService;
            _passwordState = passwordState;

            PrimaryButtonText = Strings.Next;
            SecondaryButtonText = Strings.Cancel;

            Field.PlaceholderText = passwordState.PasswordHint.Length > 0
                ? passwordState.PasswordHint
                : Strings.EnterPassword;
        }

        public string Password { get; private set; }

        public EmailAddressAuthenticationCodeInfo RecoveryEmailAddressCodeInfo { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                var password = Field.Password;

                if (string.IsNullOrEmpty(password))
                {
                    VisualUtilities.ShakeView(Field);
                    return;
                }

                IsPrimaryButtonEnabled = false;

                var deferral = args.GetDeferral();

                var confirm = await _clientService.SendAsync(new GetRecoveryEmailAddress(password));
                if (confirm is RecoveryEmailAddress)
                {
                    Password = password;
                }
                else
                {
                    VisualUtilities.ShakeView(Field);
                    Field.Password = string.Empty;

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

        private void Password_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Hide(ContentDialogResult.Primary);
                e.Handled = true;
            }
        }

        private async void Forgot_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_passwordState.HasRecoveryEmailAddress)
            {
                var response = await _clientService.SendAsync(new RequestPasswordRecovery());
                if (response is EmailAddressAuthenticationCodeInfo info)
                {
                    RecoveryEmailAddressCodeInfo = info;
                    Hide();
                }
                else if (response is Error error)
                {
                    await MessagePopup.ShowAsync(target: null, error.Message ?? "Error message", error.Code.ToString());
                }
            }
            else
            {
                await MessagePopup.ShowAsync(target: null, Strings.RestorePasswordNoEmailText, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                //IsResettable = true;
            }
        }
    }
}
