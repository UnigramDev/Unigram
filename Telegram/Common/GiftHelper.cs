//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public static class GiftHelper
    {
        /// <summary>
        /// Takes an upgraded gift to the TON blockchain: into the current user's wallet when there
        /// can be one, through Fragment otherwise.
        /// </summary>
        public static async void Export(IClientService clientService, INavigationService navigation, ReceivedGift gift)
        {
            if (gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToUnixTimeSeconds();
            if (now < gift.ExportDate)
            {
                var days = (int)Math.Ceiling((gift.ExportDate - now) / 86400d);
                await navigation.ShowPopupAsync(Locale.Declension(Strings.R.Gift2ExportTONUnlocksAlertText, days), Strings.Gift2ExportTONUnlocksAlertTitle, Strings.OK);
                return;
            }

            var name = upgraded.Gift.ToName();
            var wallet = clientService.Options.CanUseTonWallet;
            var keepOnProfile = false;

            if (wallet)
            {
                if (clientService.TonWalletState == null)
                {
                    await clientService.SendAsync(new LoadTonWalletState());
                }

                var popup = new MoveGiftToWalletPopup(clientService.TonWalletState?.Address);

                var confirm = await navigation.ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                keepOnProfile = popup.KeepOnProfile;
            }
            else
            {
                var confirm = await navigation.ShowPopupAsync(string.Format(Strings.Gift2ExportTONFragmentText, name), Strings.Gift2ExportTONFragmentTitle, Strings.OpenFragment, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var message = wallet
                ? string.Format(Strings.PleaseEnterCurrentPasswordGiftWallet, name)
                : string.Format(Strings.PleaseEnterCurrentPasswordGiftTransfer, name);

            if (!await GetTemporaryPasswordAsync(clientService, navigation, message))
            {
                return;
            }

            if (wallet)
            {
                var response = await clientService.SendAsync(new WithdrawUpgradedGift(gift.ReceivedGiftId, keepOnProfile));
                if (response is Ok)
                {
                    // Kept on the profile, it is still among the received gifts.
                    if (!keepOnProfile)
                    {
                        clientService.Session.Resolve<IEventAggregator>().Publish(new UpdateGiftIsSold(gift.ReceivedGiftId));
                    }

                    navigation.ShowToast(Strings.Gift2MovedToWallet);
                }
                else if (response is Error error)
                {
                    ShowExportError(navigation, error);
                }
            }
            else
            {
                var response = await clientService.SendAsync(new GetUpgradedGiftWithdrawalUrl(gift.ReceivedGiftId));
                if (response is HttpUrl httpUrl)
                {
                    MessageHelper.OpenUrl(null, null, httpUrl.Url);
                }
                else if (response is Error error)
                {
                    ShowExportError(navigation, error);
                }
            }
        }

        // Both withdrawals take a temporary password rather than the password itself.
        private static async Task<bool> GetTemporaryPasswordAsync(IClientService clientService, INavigationService navigation, string message)
        {
            if (await clientService.SendAsync(new GetTemporaryPasswordState()) is TemporaryPasswordState { HasPassword: true, ValidFor: > 0 })
            {
                return true;
            }

            if (await clientService.SendAsync(new GetPasswordState()) is not PasswordState passwordState)
            {
                return false;
            }
            else if (!passwordState.HasPassword)
            {
                ShowSecurityCheck(navigation, true);
                return false;
            }

            var placeholder = string.IsNullOrEmpty(passwordState.PasswordHint)
                ? Strings.LoginPassword
                : passwordState.PasswordHint;

            var result = await navigation.ShowInputAsync(InputPopupType.Password, message, Strings.TwoStepVerification, placeholder, Strings.OK, Strings.Cancel);
            if (result.Result != ContentDialogResult.Primary)
            {
                return false;
            }

            var response = await clientService.SendAsync(new CreateTemporaryPassword(result.Text, 60));
            if (response is TemporaryPasswordState)
            {
                return true;
            }
            else if (response is Error error)
            {
                ShowExportError(navigation, error);
            }

            return false;
        }

        private static void ShowExportError(INavigationService navigation, Error error)
        {
            if (error.Message.Equals("PASSWORD_MISSING") || error.Message.StartsWith("PASSWORD_TOO_FRESH_") || error.Message.StartsWith("SESSION_TOO_FRESH_"))
            {
                ShowSecurityCheck(navigation, error.Message.Equals("PASSWORD_MISSING"));
            }
            else
            {
                navigation.ShowToast(error);
            }
        }

        private static async void ShowSecurityCheck(INavigationService navigation, bool passwordMissing)
        {
            var builder = new StringBuilder();
            builder.AppendLine(Strings.Gift2TransferToTONAlertText);
            builder.AppendLine($"\u2022 {Strings.Gift2TransferToTONAlertText1}");
            builder.AppendLine($"\u2022 {Strings.Gift2TransferToTONAlertText2}");

            if (passwordMissing)
            {
                var confirm = await navigation.ShowPopupAsync(builder.ToString(), Strings.Gift2TransferToTONAlertTitle, Strings.EditAdminTransferSetPassword, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    navigation.NavigateToPasswordSetup();
                }
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine(Strings.Gift2TransferToTONAlertText3);

                await navigation.ShowPopupAsync(builder.ToString(), Strings.Gift2TransferToTONAlertTitle, Strings.OK);
            }
        }
    }
}
