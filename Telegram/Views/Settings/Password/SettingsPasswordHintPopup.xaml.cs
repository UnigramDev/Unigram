//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordHintPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly string _oldPassword;
        private readonly string _password;

        public SettingsPasswordHintPopup(IClientService clientService, string oldPassword, string password)
        {
            InitializeComponent();

            _clientService = clientService;
            _oldPassword = oldPassword;
            _password = password;

            PrimaryButtonText = string.Empty;
            SecondaryButtonText = Strings.Cancel;
        }

        public string Hint { get; private set; }

        public PasswordState PasswordState { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                var password = _password;
                var hint = Field.Text;

                if (string.Equals(password, hint))
                {
                    ToastPopup.Show(Strings.PasswordAsHintError);
                    args.Cancel = true;
                    return;
                }

                if (_clientService == null)
                {
                    Hint = hint;
                    return;
                }

                IsPrimaryButtonEnabled = false;

                var deferral = args.GetDeferral();

                var response = await _clientService.SendAsync(new SetPassword(_oldPassword, _password, hint, false, string.Empty));
                if (response is PasswordState passwordState)
                {
                    PasswordState = passwordState;
                }
                else if (response is Error error)
                {
                    Logger.Error(error.Message);

                    if (error.CodeEquals(ErrorCode.FLOOD))
                    {
                        AlertsService.ShowFloodWaitAlert(error.Message);
                    }
                    else
                    {
                        await MessagePopup.ShowAsync(target: null, error.Message, Strings.RestorePasswordNoEmailTitle, Strings.OK);
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
            PrimaryButtonText = string.IsNullOrEmpty(Field.Text)
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

        private void Skip_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
