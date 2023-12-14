//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings.LoginEmail
{
    public sealed partial class SettingsLoginEmailCodePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly EmailAddressAuthenticationCodeInfo _codeInfo;

        public SettingsLoginEmailCodePopup(IClientService clientService, EmailAddressAuthenticationCodeInfo codeInfo)
        {
            InitializeComponent();

            _clientService = clientService;
            _codeInfo = codeInfo;

            Subtitle.Text = string.Format(Strings.CheckYourNewEmailSubtitle, codeInfo.EmailAddressPattern);

            PrimaryButtonText = Strings.Continue;
            SecondaryButtonText = Strings.Cancel;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var code = PrimaryInput.Text;
            if (string.IsNullOrEmpty(code) || code.Length < _codeInfo.Length)
            {
                VisualUtilities.ShakeView(PrimaryInput);
                args.Cancel = true;

                return;
            }

            var deferral = args.GetDeferral();

            var response = await _clientService.SendAsync(new CheckLoginEmailAddressCode(new EmailAddressAuthenticationCode(code)));
            if (response is Error error)
            {
                VisualUtilities.ShakeView(PrimaryInput);
                args.Cancel = true;

                if (error.MessageEquals(ErrorType.EMAIL_VERIFY_EXPIRED))
                {
                    await MessagePopup.ShowAsync(target: null, Strings.CodeExpired, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.CODE_INVALID))
                {
                    await MessagePopup.ShowAsync(target: null, Strings.InvalidCode, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
            }

            deferral.Complete();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void PrimaryInput_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Hide(ContentDialogResult.Primary);
                e.Handled = true;
            }
        }

        private void PrimaryInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PrimaryInput.Text.Length == _codeInfo.Length)
            {
                Hide(ContentDialogResult.Primary);
            }
        }
    }
}
