//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services.Wallet;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// The app's own frame around the Windows Hello window.
    /// </summary>
    /// <remarks>
    /// Windows raises its PIN window over whatever is on screen and says nothing about who asked or
    /// what for, so this sits behind it and does. It also gives a failure somewhere to be read and
    /// retried: on its own, a Hello window that goes away has told the user nothing.
    /// </remarks>
    public sealed partial class WalletHelloPopup : ModalPopup
    {
        private readonly Func<Task> _unlock;

        // What to rethrow once the popup is gone. Null after a success, and after the user
        // dismissing the Windows window - which is an answer rather than a fault.
        private Exception _failure;

        // Loaded can run again on the same popup, and a second attempt while the first is still
        // waiting on Windows would raise a second Hello window over it.
        private bool _running;

        private WalletHelloPopup(string reason, Func<Task> unlock)
        {
            InitializeComponent();

            _unlock = unlock;

            Subtitle.Text = string.IsNullOrEmpty(reason)
                ? Strings.WalletHelloConfirmText
                : reason;

            SecondaryButtonText = Strings.Cancel;
        }

        /// <summary>
        /// Shows the popup, runs <paramref name="unlock"/> in it, and answers the way it did:
        /// returning once it has succeeded, throwing what stopped it otherwise.
        /// </summary>
        public static async Task ShowAsync(INavigationService navigation, string reason, Func<Task> unlock)
        {
            var popup = new WalletHelloPopup(reason, unlock);
            await navigation.ShowPopupAsync(popup);

            if (popup._failure != null)
            {
                ExceptionDispatchInfo.Capture(popup._failure).Throw();
            }
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            // The Windows window comes up on its own: the user asked to spend, and a button here
            // first would only be a step between them and the one prompt that means something.
            Attempt();
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            args.Cancel = true;
            Attempt();
        }

        private async void Attempt()
        {
            if (_running)
            {
                return;
            }

            _running = true;

            ErrorLabel.Visibility = Visibility.Collapsed;
            PrimaryButtonText = null;

            try
            {
                await _unlock();

                _failure = null;
                Hide(ContentDialogResult.Primary);
            }
            catch (WalletVaultException ex)
            {
                _failure = ex;

                switch (ex.Failure)
                {
                    case WalletVaultFailure.Cancelled:
                        // They dismissed the Windows window, which is them saying no. Repeating it
                        // back as an error would be telling them what they just decided.
                        _failure = null;
                        Hide();
                        break;
                    case WalletVaultFailure.Unenrolled:
                        // Retrying cannot help - the key the TPM held is gone - and the way out is
                        // to bind the wallet again, which the caller does once this reaches it.
                        Hide();
                        break;
                    default:
                        ShowError(Strings.WalletHelloErrorGeneric);
                        break;
                }
            }
            catch (Exception ex)
            {
                _failure = ex;
                ShowError(Strings.WalletHelloNotAvailable);
            }
            finally
            {
                _running = false;
            }
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.Visibility = Visibility.Visible;

            PrimaryButtonText = Strings.WalletHelloTryAgain;
        }
    }
}
