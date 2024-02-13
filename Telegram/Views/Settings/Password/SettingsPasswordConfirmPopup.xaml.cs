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
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordConfirmPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly PasswordState _passwordState;

        private readonly DispatcherTimer _pendingTimer;

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

            _pendingTimer = new DispatcherTimer();
            _pendingTimer.Interval = TimeSpan.FromSeconds(1);
            _pendingTimer.Tick += Pending_Tick;

            if (passwordState.PendingResetDate > 0)
            {
                _pendingTimer.Start();
                Pending_Tick(_pendingTimer, null);
            }
            else
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(Strings.ForgotPassword);
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Click += Forgot_Click;

                Footer.Inlines.Add(hyperlink);
            }
        }

        private void Pending_Tick(object sender, object e)
        {
            Footer.Inlines.Clear();

            var diff = _passwordState.PendingResetDate - DateTime.Now.ToTimestamp();
            if (diff > 0)
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(Strings.CancelReset);
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Click += CancelReset_Click;

                Footer.Inlines.Add(string.Format(Strings.RestorePasswordResetIn, Locale.FormatTtl(diff)));
                Footer.Inlines.Add(new LineBreak());
                Footer.Inlines.Add(hyperlink);
            }
            else
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(Strings.ResetPassword);
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Click += ResetPassword_Click;

                Footer.Inlines.Add(hyperlink);
            }
        }

        private async void CancelReset_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var confirm = await MessagePopup.ShowAsync(target: null, Strings.CancelPasswordReset, Strings.AppName, Strings.CancelPasswordResetYes, Strings.CancelPasswordResetNo);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await _clientService.SendAsync(new CancelPasswordReset());
                if (response is Ok)
                {
                    _passwordState.PendingResetDate = 0;
                    _pendingTimer.Stop();

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(Strings.ForgotPassword);
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Click += Forgot_Click;

                    Footer.Inlines.Clear();
                    Footer.Inlines.Add(hyperlink);
                }
            }
        }

        private void ResetPassword_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            ResetPassword();
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
            else if (_passwordState.PendingResetDate == 0)
            {
                var confirm = await MessagePopup.ShowAsync(target: null, Strings.RestorePasswordNoEmailText2, Strings.RestorePasswordNoEmailTitle, Strings.Reset, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ResetPassword();
                }
            }
            else
            {
                await MessagePopup.ShowAsync(target: null, Strings.RestorePasswordNoEmailText, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                //IsResettable = true;
            }
        }

        private async void ResetPassword()
        {
            var response = await _clientService.SendAsync(new ResetPassword());
            if (response is ResetPasswordResultOk)
            {
                // TODO: reload password state
                Hide();
            }
            else if (response is ResetPasswordResultPending pending)
            {
                _passwordState.PendingResetDate = pending.PendingResetDate;

                _pendingTimer.Stop();
                _pendingTimer.Start();

                Pending_Tick(_pendingTimer, null);
            }
            else if (response is ResetPasswordResultDeclined declined)
            {
                var diff = _passwordState.PendingResetDate - DateTime.Now.ToTimestamp();
                if (diff > 0)
                {
                    await MessagePopup.ShowAsync(target: null, string.Format(Strings.ResetPasswordWait, Locale.FormatTtl(diff)), Strings.AppName, Strings.OK);
                }
            }
        }
    }
}
