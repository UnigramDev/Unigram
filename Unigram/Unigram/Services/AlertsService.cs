using System;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;

namespace Unigram.Services
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
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred + "\n" + error.Message);
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
        //                ShowSimpleAlert(fragment, Strings.Resources.EditMessageError);
        //            }
        //            else
        //            {
        //                ShowSimpleToast(fragment, Strings.Resources.EditMessageError);
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
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //        }
        //        else if (error.Message.Equals("USERS_TOO_MUCH"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.JoinToGroupErrorFull);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.JoinToGroupErrorNotExist);
        //        }
        //    }
        //    else if (request is TL_messages_getAttachedStickers)
        //    {
        //        if (fragment != null && fragment.getParentActivity() != null)
        //        {
        //            Toast.makeText(fragment.getParentActivity(), Strings.Resources.ErrorOccurred + "\n" + error.text, Toast.LENGTH_SHORT).show();
        //        }
        //    }
        //    else if (request is TL_account_confirmPhone || request is TL_account_verifyPhone || request is TL_account_verifyEmail)
        //    {
        //        if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID") || error.Message.Contains("CODE_INVALID") || error.Message.Contains("CODE_EMPTY"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED") || error.Message.Contains("EMAIL_VERIFY_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
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
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //        }
        //        else if (error.Code != -1000)
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred + "\n" + error.Message);
        //        }
        //    }
        //    else if (request is TL_account_sendConfirmPhoneCode)
        //    {
        //        if (error.Code == 400)
        //        {
        //            return ShowSimpleAlert(fragment, Strings.Resources.CancelLinkExpired);
        //        }
        //        else if (error.Message != null)
        //        {
        //            if (error.Message.StartsWith("FLOOD_WAIT"))
        //            {
        //                return ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //            }
        //            else
        //            {
        //                return ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred);
        //            }
        //        }
        //    }
        //    else if (request is TL_account_changePhone)
        //    {
        //        if (error.Message.Contains("PHONE_NUMBER_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
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
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidPhoneNumber);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EMPTY") || error.Message.Contains("PHONE_CODE_INVALID"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.InvalidCode);
        //        }
        //        else if (error.Message.Contains("PHONE_CODE_EXPIRED"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.CodeExpired);
        //        }
        //        else if (error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //        }
        //        else if (error.Message.StartsWith("PHONE_NUMBER_OCCUPIED"))
        //        {
        //            ShowSimpleAlert(fragment, LocaleController.formatString("ChangePhoneNumberOccupied", R.string.ChangePhoneNumberOccupied, (String)args[0]));
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred);
        //        }
        //    }
        //    else if (request is TL_updateUserName)
        //    {
        //        switch (error.Message)
        //        {
        //            case "USERNAME_INVALID":
        //                ShowSimpleAlert(fragment, Strings.Resources.UsernameInvalid);
        //                break;
        //            case "USERNAME_OCCUPIED":
        //                ShowSimpleAlert(fragment, Strings.Resources.UsernameInUse);
        //                break;
        //            default:
        //                ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred);
        //                break;
        //        }
        //    }
        //    else if (request is TL_contacts_importContacts)
        //    {
        //        if (error == null || error.Message.StartsWith("FLOOD_WAIT"))
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.FloodWait);
        //        }
        //        else
        //        {
        //            ShowSimpleAlert(fragment, Strings.Resources.ErrorOccurred + "\n" + error.Message);
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
        //                ShowSimpleToast(fragment, Strings.Resources.PaymentPrecheckoutFailed);
        //                break;
        //            case "PAYMENT_FAILED":
        //                ShowSimpleToast(fragment, Strings.Resources.PaymentFailed);
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
        //                ShowSimpleToast(fragment, Strings.Resources.PaymentNoShippingMethod);
        //                break;
        //            default:
        //                ShowSimpleToast(fragment, error.Message);
        //                break;
        //        }
        //    }
        //}

        private static async void ShowPeerFloodAlert(IDispatcherWrapper fragment, int reason)
        {
            var dialog = new MessagePopup();
            dialog.Title = Strings.Resources.AppName;
            dialog.PrimaryButtonText = Strings.Resources.OK;

            if (reason != 2)
            {
                dialog.SecondaryButtonText = Strings.Resources.MoreInfo;
                dialog.SecondaryButtonClick += (s, args) =>
                {
                    MessageHelper.NavigateToUsername(null, null, "spambot", null, null, null, null);
                };
            }

            if (reason == 0)
            {
                dialog.Message = Strings.Resources.NobodyLikesSpam1;
            }
            else if (reason == 1)
            {
                dialog.Message = Strings.Resources.NobodyLikesSpam2;
            }
            else if (reason == 2)
            {
                //builder.setMessage((String)args[1]);
            }

            await dialog.ShowQueuedAsync();
        }

        public static void ShowSimpleToast(IDispatcherWrapper fragment, String text)
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

        public static async void ShowSimpleAlert(String text)
        {
            if (text == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.Resources.AppName;
            dialog.Message = text;
            dialog.PrimaryButtonText = Strings.Resources.OK;

            await dialog.ShowQueuedAsync();
        }

        private static String GetFloodWaitString(String error)
        {
            var time = error.ToInt32();
            return string.Format(Strings.Resources.FloodWaitTime, Locale.FormatCallDuration(time));
        }

        public static void ShowFloodWaitAlert(String error)
        {
            if (error == null || !error.StartsWith("FLOOD_WAIT"))
            {
                return;
            }

            ShowSimpleAlert(GetFloodWaitString(error));
        }

        public static async void ShowAddUserAlert(IDispatcherWrapper fragment, string error, bool channel)
        {
            if (error == null)
            {
                return;
            }

            var dialog = new MessagePopup();
            dialog.Title = Strings.Resources.AppName;
            dialog.PrimaryButtonText = Strings.Resources.OK;

            switch (error)
            {
                case "PEER_FLOOD":
                    dialog.Message = Strings.Resources.NobodyLikesSpam2;
                    dialog.SecondaryButtonText = Strings.Resources.MoreInfo;
                    dialog.SecondaryButtonClick += (s, args) =>
                    {
                        MessageHelper.NavigateToUsername(null, null, "spambot", null, null, null, null);
                    };
                    break;
                case "USER_BLOCKED":
                case "USER_BOT":
                case "USER_ID_INVALID":
                    dialog.Message = channel ? Strings.Resources.ChannelUserCantAdd : Strings.Resources.GroupUserCantAdd;
                    break;
                case "USERS_TOO_MUCH":
                    dialog.Message = channel ? Strings.Resources.ChannelUserAddLimit : Strings.Resources.GroupUserAddLimit;
                    break;
                case "USER_NOT_MUTUAL_CONTACT":
                    dialog.Message = channel ? Strings.Resources.ChannelUserLeftError : Strings.Resources.GroupUserLeftError;
                    break;
                case "ADMINS_TOO_MUCH":
                    dialog.Message = channel ? Strings.Resources.ChannelUserCantAdmin : Strings.Resources.GroupUserCantAdmin;
                    break;
                case "BOTS_TOO_MUCH":
                    dialog.Message = channel ? Strings.Resources.ChannelUserCantBot : Strings.Resources.GroupUserCantBot;
                    break;
                case "USER_PRIVACY_RESTRICTED":
                    dialog.Message = channel ? Strings.Resources.InviteToChannelError : Strings.Resources.InviteToGroupError;
                    break;
                case "USERS_TOO_FEW":
                    dialog.Message = Strings.Resources.CreateGroupError;
                    break;
                case "USER_RESTRICTED":
                    dialog.Message = Strings.Resources.UserRestricted;
                    break;
                case "YOU_BLOCKED_USER":
                    dialog.Message = Strings.Resources.YouBlockedUser;
                    break;
                case "CHAT_ADMIN_BAN_REQUIRED":
                case "USER_KICKED":
                    dialog.Message = Strings.Resources.AddAdminErrorBlacklisted;
                    break;
                case "CHAT_ADMIN_INVITE_REQUIRED":
                    dialog.Message = Strings.Resources.AddAdminErrorNotAMember;
                    break;
                case "USER_ADMIN_INVALID":
                    dialog.Message = Strings.Resources.AddBannedErrorAdmin;
                    break;
                default:
                    dialog.Message = Strings.Resources.ErrorOccurred + "\n" + error;
                    break;
            }

            await dialog.ShowQueuedAsync();
        }
    }
}
