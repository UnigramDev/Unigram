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
    public sealed partial class SettingsLoginEmailAddressPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public SettingsLoginEmailAddressPopup(IClientService clientService)
        {
            InitializeComponent();

            _clientService = clientService;

            PrimaryButtonText = Strings.Continue;
            SecondaryButtonText = Strings.Cancel;
        }

        public EmailAddressAuthenticationCodeInfo CodeInfo { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var address = PrimaryInput.Text;
            if (string.IsNullOrEmpty(address) || !address.IsValidEmailAddress())
            {
                VisualUtilities.ShakeView(PrimaryInput);
                args.Cancel = true;

                return;
            }

            var deferral = args.GetDeferral();

            var response = await _clientService.SendAsync(new SetLoginEmailAddress(address));
            if (response is EmailAddressAuthenticationCodeInfo codeInfo)
            {
                CodeInfo = codeInfo;
            }
            else if (response is Error error)
            {
                VisualUtilities.ShakeView(PrimaryInput);
                args.Cancel = true;

                if (error.MessageEquals(ErrorType.EMAIL_INVALID))
                {
                    await MessagePopup.ShowAsync(target: null, Strings.EmailAddressInvalid, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.EMAIL_NOT_ALLOWED))
                {
                    await MessagePopup.ShowAsync(target: null, Strings.EmailNotAllowed, Strings.RestorePasswordNoEmailTitle, Strings.OK);
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
    }
}
