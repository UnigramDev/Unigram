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
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings.Password
{
    public enum SettingsPasswordEmailCodeType
    {
        New,
        Continue,
        Recovery
    }

    public sealed partial class SettingsPasswordEmailCodePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private EmailAddressAuthenticationCodeInfo _codeInfo;

        private readonly bool _recovery;
        private bool _aborted;

        public SettingsPasswordEmailCodePopup(IClientService clientService, EmailAddressAuthenticationCodeInfo codeInfo, SettingsPasswordEmailCodeType type)
        {
            InitializeComponent();

            _clientService = clientService;
            _codeInfo = codeInfo;

            _recovery = type == SettingsPasswordEmailCodeType.Recovery;

            if (type == SettingsPasswordEmailCodeType.Recovery)
            {
                Title.Text = Strings.PasswordRecovery;
                Subtitle.Text = string.Format(Strings.RestoreEmailSent, codeInfo.EmailAddressPattern);

                Help.Text = Strings.RestoreEmailTroubleNoEmail;
                IsPrimaryButtonSplit = false;
            }
            else
            {
                Title.Text = Strings.VerificationCode;
                Subtitle.Text = type == SettingsPasswordEmailCodeType.New ? Strings.EmailPasswordConfirmText2 : string.Format(Strings.EmailPasswordConfirmText3, codeInfo.EmailAddressPattern);
                IsPrimaryButtonSplit = true;

                Help.Text = Strings.ResendCode;
            }

            PrimaryButtonText = Strings.Next;
            SecondaryButtonText = Strings.Cancel;
        }

        protected override void OnApplyTemplate()
        {
            var button = GetTemplateChild("PrimarySplitButton") as Button;
            if (button != null && IsPrimaryButtonSplit)
            {
                button.Click += PrimaryButton_ContextRequested;
            }

            base.OnApplyTemplate();
        }

        private void PrimaryButton_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(Abort, Strings.AbortPasswordMenu);

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void Abort()
        {
            var confirm = await MessagePopup.ShowAsync(target: null, Strings.CancelEmailQuestion, Strings.CancelEmailQuestionTitle, Strings.Abort, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await _clientService.SendAsync(new CancelRecoveryEmailAddressVerification());
                if (response is PasswordState passwordState)
                {
                    PasswordState = passwordState;

                    _aborted = true;
                    Hide();
                }
                else if (response is Error error)
                {
                    await MessagePopup.ShowAsync(target: null, error.Message, Strings.AppName, Strings.OK);
                }
            }
        }

        public PasswordState PasswordState { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                if (_aborted)
                {
                    return;
                }

                var code = Field.Text;

                if (string.IsNullOrEmpty(code))
                {
                    VisualUtilities.ShakeView(Field);
                    return;
                }

                IsPrimaryButtonEnabled = false;

                var deferral = args.GetDeferral();

                var response = await _clientService.SendAsync(new CheckRecoveryEmailAddressCode(code));
                if (response is PasswordState passwordState)
                {
                    PasswordState = passwordState;

                    if (passwordState.RecoveryEmailAddressCodeInfo != null)
                    {
                        Field.Text = string.Empty;

                        VisualUtilities.ShakeView(Field);
                        args.Cancel = true;

                        await MessagePopup.ShowAsync(target: null, Strings.InvalidCode, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                    }
                    else if (passwordState.HasPassword is false)
                    {
                        await MessagePopup.ShowAsync(target: null, Strings.CodeExpired, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                    }
                }
                else if (response is Error error)
                {
                    await MessagePopup.ShowAsync(target: null, error.Message, Strings.AppName, Strings.OK);
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
            if (Field.Text.Length == _codeInfo.Length && _codeInfo.Length > 0)
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
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
                e.Handled = true;
            }
        }

        private async void Help_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_recovery)
            {
                var confirm = await MessagePopup.ShowAsync(target: null, Strings.RestoreEmailTroubleText2, Strings.ResendCode, Strings.Reset, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new ResetPassword());
                    if (response is ResetPasswordResult)
                    {
                        // TODO:
                    }
                }
            }
            else
            {
                var response = await _clientService.SendAsync(new ResendRecoveryEmailAddressCode());
                if (response is PasswordState passwordState)
                {
                    if (passwordState.RecoveryEmailAddressCodeInfo != null)
                    {
                        _codeInfo = passwordState.RecoveryEmailAddressCodeInfo;
                        await MessagePopup.ShowAsync(target: null, Strings.ResendCodeInfo, Strings.TwoStepVerification, Strings.OK);
                    }
                    else
                    {
                        if (passwordState.HasPassword is false)
                        {
                            await MessagePopup.ShowAsync(target: null, Strings.CodeExpired, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                        }

                        Hide();
                    }
                }
                else if (response is Error error)
                {
                    await MessagePopup.ShowAsync(target: null, error.Message, Strings.AppName, Strings.OK);
                }
            }
        }
    }
}
