//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;

namespace Telegram.Services
{
    class AlertsService
    {
        //public static void ProcessError(IDispatcherWrapper fragment, Error error, Type request, params object[] args)
        //{
        //    if (error.Code == 406 || error.Message == null)
        //    {
        //        return null;
        //    }

        //    if (request is TL_account_saveSecureValue || request is TL_account_getAuthorizationForm)
        //    {
        //        if (error.Message.Contains("PHONE_NUMBER_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.ErrorOccurred + "\n" + error.Message);
        //        }
        //    }
        //    else if (request is TL_channels_joinChannel ||
        //     request is TL_channels_editAdmin ||
        //     request is TL_channels_inviteToChannel ||
        //     request is TL_messages_addChatUser ||
        //     request is TL_messages_startBot ||
        //     request is TL_channels_editBanned)
        //    {
        //        if (fragment != null)
        //        {
        //            ShowAddUserAlert(error.Message, fragment, (Boolean)args[0]);
        //        }
        //        else
        //        {
        //            if (error.Message.equals("PEER_FLOOD"))
        //            {
        //                NotificationCenter.getInstance(currentAccount).postNotificationName(NotificationCenter.needShowAlert, 1);
        //            }
        //        }
        //    }
        //    else if (request is TL_messages_createChat)
        //    {
        //        if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowFloodWaitAlert(fragment, error.Message);
        //        }
        //        else
        //        {
        //            ShowAddUserAlert(fragment, error.Message, false);
        //        }
        //    }
        //    else if (request is TL_channels_createChannel)
        //    {
        //        if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowFloodWaitAlert(fragment, error.Message);
        //        }
        //        else
        //        {
        //            ShowAddUserAlert(fragment, error.Message, false);
        //        }
        //    }
        //    else if (request is TL_messages_editMessage)
        //    {
        //        if (!error.Message.Equals("MESSAGE_NOT_MODIFIED"))
        //        {
        //            if (fragment != null)
        //            {
        //                ShowSimpleAlert(fragment, Strings.EditMessageError);
        //            }
        //            else
        //            {
        //                ShowSimpleToast(fragment, Strings.EditMessageError);
        //            }
        //        }
        //    }
        //    else if (request is TL_messages_sendMessage ||
        //              request is TL_messages_sendMedia ||

        //              request is TL_messages_sendBroadcast ||
        //              request is TL_messages_sendInlineBotResult ||

        //              request is TL_messages_forwardMessages)
        //    {
        //        if (error.Message.Equals("PEER_FLOOD"))
        //        {
        //            NotificationCenter.getInstance(currentAccount).postNotificationName(NotificationCenter.needShowAlert, 0);
        //        }
        //    }
        //    else if (request is TL_messages_importChatInvite)
        //    {
        //        if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else if (error.Message.Equals("USERS_TOO_MUCH"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.JoinToGroupErrorFull);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.JoinToGroupErrorNotExist);
        //        }
        //    }
        //    else if (request is TL_messages_getAttachedStickers)
        //    {
        //        if (fragment != null && fragment.getParentActivity() != null)
        //        {
        //            Toast.makeText(fragment.getParentActivity(), Strings.ErrorOccurred + "\n" + error.text, Toast.LENGTH_SHORT).show();
        //        }
        //    }
        //    else if (request is TL_account_confirmPhone || request is TL_account_verifyPhone || request is TL_account_verifyEmail)
        //    {
        //        if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID") || error.Message.Contains("CODE_INVALID") || error.Message.Contains("CODE_EMPTY"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED") || error.Message.Contains("EMAIL_VERIFY_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, error.Message);
        //        }
        //    }
        //    else if (request is TL_auth_resendCode)
        //    {
        //        if (error.Message.Contains("PHONE_NUMBER_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else if (error.Code != -1000)
        //        {
        //            ShowSimpleAlert(fragment, Strings.ErrorOccurred + "\n" + error.Message);
        //        }
        //    }
        //    else if (request is TL_account_sendConfirmPhoneCode)
        //    {
        //        if (error.Code == 400)
        //        {
        //            return ShowSimpleAlert(fragment, Strings.CancelLinkExpired);
        //        }
        //        else if (error.Message != null)
        //        {
        //            if (error.Message.StartsWith("FLOOD_WAIT"))
        //            {
        //                return ShowSimpleAlert(fragment, Strings.FloodWait);
        //            }
        //            else
        //            {
        //                return ShowSimpleAlert(fragment, Strings.ErrorOccurred);
        //            }
        //        }
        //    }
        //    else if (request is TL_account_changePhone)
        //    {
        //        if (error.Message.Contains("PHONE_NUMBER_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, error.Message);
        //        }
        //    }
        //    else if (request is TL_account_sendChangePhoneCode)
        //    {
        //        if (error.Message.Contains("PHONE_NUMBER_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else if (error.Message.StartsWith("PHONE_NUMBER_OCCUPIED"))
        //        {
        //            ShowSimpleAlert(fragment, LocaleController.formatString("ChangePhoneNumberOccupied", R.string.ChangePhoneNumberOccupied, (String)args[0]));
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.ErrorOccurred);
        //        }
        //    }
        //    else if (request is TL_updateUserName)
        //    {
        //        switch (error.Message)
        //        {
        //            case "USERNAME_INVALID":
        //                ShowSimpleAlert(fragment, Strings.UsernameInvalid);
        //                break;
        //            case "USERNAME_OCCUPIED":
        //                ShowSimpleAlert(fragment, Strings.UsernameInUse);
        //                break;
        //            default:
        //                ShowSimpleAlert(fragment, Strings.ErrorOccurred);
        //                break;
        //        }
        //    }
        //    else if (request is TL_contacts_importContacts)
        //    {
        //        if (error == null || error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.ErrorOccurred + "\n" + error.Message);
        //        }
        //    }
        //    else if (request is TL_account_getPassword || request is TL_account_getTmpPassword)
        //    {
        //        if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleToast(fragment, getFloodWaitString(error.Message));
        //        }
        //        else
        //        {
        //            ShowSimpleToast(fragment, error.Message);
        //        }
        //    }
        //    else if (request is TL_payments_sendPaymentForm)
        //    {
        //        switch (error.Message)
        //        {
        //            case "BOT_PRECHECKOUT_FAILED":
        //                ShowSimpleToast(fragment, Strings.PaymentPrecheckoutFailed);
        //                break;
        //            case "PAYMENT_FAILED":
        //                ShowSimpleToast(fragment, Strings.PaymentFailed);
        //                break;
        //            default:
        //                ShowSimpleToast(fragment, error.Message);
        //                break;
        //        }
        //    }
        //    else if (request is TL_payments_validateRequestedInfo)
        //    {
        //        switch (error.Message)
        //        {
        //            case "SHIPPING_NOT_AVAILABLE":
        //                ShowSimpleToast(fragment, Strings.PaymentNoShippingMethod);
        //                break;
        //            default:
        //                ShowSimpleToast(fragment, error.Message);
        //                break;
        //        }
        //    }
        //}

        private static async void ShowPeerFloodAlert(IDispatcherContext fragment, int reason)
        {
            var dialog = new MessagePopup();
            dialog.Title = Strings.AppName;
            dialog.PrimaryButtonText = Strings.OK;

            if (reason != 2)
            {
                dialog.SecondaryButtonText = Strings.MoreInfo;
                dialog.SecondaryButtonClick += (s, args) =>
                {
                    MessageHelper.NavigateToUsername(null, null, "spambot", null, null);
                };
            }

            if (reason == 0)
            {
                dialog.Message = Strings.NobodyLikesSpam1;
            }
            else if (reason == 1)
            {
                dialog.Message = Strings.NobodyLikesSpam2;
            }
            else if (reason == 2)
            {
                //builder.setMessage((String)args[1]);
            }

            await dialog.ShowQueuedAsync();
        }

        public static void ShowSimpleToast(IDispatcherContext fragment, string text)
        {
            if (text == null)
            {
                return;
            }

            // TODO:
            //Toast toast = Toast.makeText(baseFragment.getParentActivity(), text, Toast.LENGTH_LONG);
            //toast.show();
            //return toast;

            ShowSimpleAlert(text);
        }

        public static async void ShowSimpleAlert(string text)
        {
            if (text == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.AppName;
            dialog.Message = text;
            dialog.PrimaryButtonText = Strings.OK;

            await dialog.ShowQueuedAsync();
        }

        private static string GetFloodWaitString(string error)
        {
            var time = error.ToInt32();
            return string.Format(Strings.FloodWaitTime, Locale.FormatCallDuration(time));
        }

        public static void ShowFloodWaitAlert(string error)
        {
            if (error == null || !error.StartsWith("FLOOD_WAIT"))
            {
                return;
            }

            ShowSimpleAlert(GetFloodWaitString(error));
        }

        public static async void ShowAddUserAlert(IDispatcherContext fragment, string error, bool channel)
        {
            if (error == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.AppName;
            dialog.PrimaryButtonText = Strings.OK;

            switch (error)
            {
                case "PEER_FLOOD":
                    dialog.Message = Strings.NobodyLikesSpam2;
                    dialog.SecondaryButtonText = Strings.MoreInfo;
                    dialog.SecondaryButtonClick += (s, args) =>
                    {
                        MessageHelper.NavigateToUsername(null, null, "spambot", null, null);
                    };
                    break;
                case "USER_BLOCKED":
                case "USER_BOT":
                case "USER_ID_INVALID":
                    dialog.Message = channel ? Strings.ChannelUserCantAdd : Strings.GroupUserCantAdd;
                    break;
                case "USERS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ChannelUserAddLimit : Strings.GroupUserAddLimit;
                    break;
                case "USER_NOT_MUTUAL_CONTACT":
                    dialog.Message = channel ? Strings.ChannelUserLeftError : Strings.GroupUserLeftError;
                    break;
                case "ADMINS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ChannelUserCantAdmin : Strings.GroupUserCantAdmin;
                    break;
                case "BOTS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ChannelUserCantBot : Strings.GroupUserCantBot;
                    break;
                case "USER_PRIVACY_RESTRICTED":
                    dialog.Message = channel ? Strings.InviteToChannelError : Strings.InviteToGroupError;
                    break;
                case "USERS_TOO_FEW":
                    dialog.Message = Strings.CreateGroupError;
                    break;
                case "USER_RESTRICTED":
                    dialog.Message = Strings.UserRestricted;
                    break;
                case "YOU_BLOCKED_USER":
                    dialog.Message = Strings.YouBlockedUser;
                    break;
                case "CHAT_ADMIN_BAN_REQUIRED":
                case "USER_KICKED":
                    dialog.Message = Strings.AddAdminErrorBlacklisted;
                    break;
                case "CHAT_ADMIN_INVITE_REQUIRED":
                    dialog.Message = Strings.AddAdminErrorNotAMember;
                    break;
                case "USER_ADMIN_INVALID":
                    dialog.Message = Strings.AddBannedErrorAdmin;
                    break;
                default:
                    dialog.Message = Strings.ErrorOccurred + "\n" + error;
                    break;
            }

            await dialog.ShowQueuedAsync();
        }
    }
}
