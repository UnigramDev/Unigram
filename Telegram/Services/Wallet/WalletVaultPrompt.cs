//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Wallet.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Asks the user what <see cref="WalletVault"/> needs, in the window they are acting in.
    /// </summary>
    /// <remarks>
    /// Holds no window of its own. Every call names the one to ask in, handed down from whoever
    /// started the operation, because the vault is reached from the engine's threads and a prompt
    /// about the user's own spending belongs in the window they opened it from - not in whichever
    /// one happens to be focused by the time the engine gets round to asking.
    ///
    /// The marshalling is that window's, for the same reason.
    /// </remarks>
    public sealed class WalletVaultPrompt : IWalletVaultPrompt
    {
        private readonly IPasscodeService _passcodeService;

        public WalletVaultPrompt(IPasscodeService passcodeService)
        {
            _passcodeService = passcodeService;
        }

        public Task<WalletVaultChoice> RequestEnrollmentAsync(INavigationService navigation, WalletVaultMethod? current, WalletEnrollReason reason)
        {
            return navigation.Dispatcher.DispatchAsync(() => EnrollAsync(navigation, current, reason));
        }

        public bool CanAskPasscode => _passcodeService.IsEnabled;

        public Task<string> RequestPasscodeAsync(INavigationService navigation, string reason)
        {
            return navigation.Dispatcher.DispatchAsync(() => ConfirmPasscodeAsync(navigation));
        }

        public Task RequestHelloAsync(INavigationService navigation, string reason, Func<Task> unlock)
        {
            return navigation.Dispatcher.DispatchAsync(() => WalletHelloPopup.ShowAsync(navigation, reason, unlock));
        }

        private async Task<WalletVaultChoice> EnrollAsync(INavigationService navigation, WalletVaultMethod? current, WalletEnrollReason reason)
        {
            var method = await WalletEnrollPopup.ShowAsync(navigation, current, reason);
            if (method == null)
            {
                return null;
            }

            if (method == current)
            {
                // Nothing changes, so nothing is asked for. Covers *keep asking for this passcode*
                // on the way to disabling it, which is a refusal to disable rather than a change,
                // and a passcode already in use, which the caller would otherwise collect only to
                // rewrap the same key under the same secret.
                return new WalletVaultChoice(method.Value);
            }

            if (method != WalletVaultMethod.Passcode)
            {
                return new WalletVaultChoice(method.Value);
            }

            // Only now, with the enrollment popup gone: both screens that can produce a passcode
            // are popups themselves, and the vault needs the code the user typed rather than the
            // fact that one is set.
            var passcode = _passcodeService.IsEnabled
                ? await ConfirmPasscodeAsync(navigation)
                : await SetPasscodeAsync(navigation);

            return passcode != null
                ? new WalletVaultChoice(WalletVaultMethod.Passcode, passcode)
                : null;
        }

        private static async Task<string> ConfirmPasscodeAsync(INavigationService navigation)
        {
            // The settings popup, rather than one of our own: it already counts the attempts and
            // honours the lockout, and a second entry screen would be a second way to guess.
            var popup = new SettingsPasscodeConfirmPopup();

            var confirm = await navigation.ShowPopupAsync(popup);
            return confirm == ContentDialogResult.Primary ? popup.Passcode : null;
        }

        private async Task<string> SetPasscodeAsync(INavigationService navigation)
        {
            var popup = new SettingsPasscodeInputPopup
            {
                IsSimple = _passcodeService.IsSimple
            };

            var confirm = await navigation.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            var timeout = _passcodeService.AutolockTimeout;

            _passcodeService.Set(popup.Passcode, popup.IsSimple, timeout);
            InactivityHelper.Initialize(timeout);

            return popup.Passcode;
        }
    }
}
