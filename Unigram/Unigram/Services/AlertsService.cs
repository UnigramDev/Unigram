using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Strings;

namespace Unigram.Services
{
    //class AlertsService
    //{
    //    public static Dialog ProcessError(TLRPCError error, TLType request, params Object[] args)
    //    {
    //        if (error.ErrorCode == 406 || error.ErrorMessage == null)
    //        {
    //            return null;
    //        }
    //        if (request == TLType.ChannelsJoinChannel ||
    //            request == TLType.ChannelsEditAdmin ||
    //            request == TLType.ChannelsInviteToChannel ||
    //            request == TLType.MessagesAddChatUser ||
    //            request == TLType.MessagesStartBot ||
    //            request == TLType.ChannelsEditBanned)
    //        {
    //            if (fragment != null)
    //            {
    //                ShowAddUserAlert(error.ErrorMessage, (Boolean)args[0]);
    //            }
    //            else
    //            {
    //                if (error.ErrorMessage.Equals("PEER_FLOOD"))
    //                {
    //                    NotificationCenter.getInstance().postNotificationName(NotificationCenter.needShowAlert, 1);
    //                }
    //            }
    //        }
    //        else if (request == TLType.MessagesCreateChat)
    //        {
    //            if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowFloodWaitAlert(error.ErrorMessage);
    //            }
    //            else
    //            {
    //                ShowAddUserAlert(error.ErrorMessage, false);
    //            }
    //        }
    //        else if (request == TLType.ChannelsCreateChannel)
    //        {
    //            if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowFloodWaitAlert(error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.MessagesEditMessage)
    //        {
    //            if (!error.ErrorMessage.Equals("MESSAGE_NOT_MODIFIED"))
    //            {
    //                ShowSimpleAlert(AppResources.EditMessageError);
    //            }
    //        }
    //        else if (request == TLType.MessagesSendMessage ||
    //                 request == TLType.MessagesSendMedia ||
    //                 request == TLType.MessagesSendInlineBotResult ||
    //                 request == TLType.MessagesForwardMessages)
    //        {
    //            if (error.ErrorMessage.Equals("PEER_FLOOD"))
    //            {
    //                NotificationCenter.getInstance().postNotificationName(NotificationCenter.needShowAlert, 0);
    //            }
    //        }
    //        else if (request == TLType.MessagesImportChatInvite)
    //        {
    //            if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else if (error.ErrorMessage.Equals("USERS_TOO_MUCH"))
    //            {
    //                ShowSimpleAlert(AppResources.JoinToGroupErrorFull);
    //            }
    //            else
    //            {
    //                ShowSimpleAlert(AppResources.JoinToGroupErrorNotExist);
    //            }
    //        }
    //        else if (request == TLType.MessagesGetAttachedStickers)
    //        {
    //            Toast.makeText(fragment.getParentActivity(), AppResources.ErrorOccurred + "\n" + error.ErrorMessage, Toast.LENGTH_SHORT).show();
    //        }
    //        else if (request == TLType.AccountConfirmPhone)
    //        {
    //            if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidCode);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
    //            {
    //                ShowSimpleAlert(AppResources.CodeExpired);
    //            }
    //            else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else
    //            {
    //                ShowSimpleAlert(error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.AuthResendCode)
    //        {
    //            if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidPhoneNumber);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidCode);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
    //            {
    //                ShowSimpleAlert(AppResources.CodeExpired);
    //            }
    //            else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else if (error.ErrorCode != -1000)
    //            {
    //                ShowSimpleAlert(AppResources.ErrorOccurred + "\n" + error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.AccountSendConfirmPhoneCode)
    //        {
    //            if (error.ErrorCode == 400)
    //            {
    //                return ShowSimpleAlert(AppResources.CancelLinkExpired);
    //            }
    //            else if (error.ErrorMessage != null)
    //            {
    //                if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //                {
    //                    return ShowSimpleAlert(AppResources.FloodWait);
    //                }
    //                else
    //                {
    //                    return ShowSimpleAlert(AppResources.ErrorOccurred);
    //                }
    //            }
    //        }
    //        else if (request == TLType.AccountChangePhone)
    //        {
    //            if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidPhoneNumber);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidCode);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
    //            {
    //                ShowSimpleAlert(AppResources.CodeExpired);
    //            }
    //            else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else
    //            {
    //                ShowSimpleAlert(error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.AccountSendChangePhoneCode)
    //        {
    //            if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidPhoneNumber);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
    //            {
    //                ShowSimpleAlert(AppResources.InvalidCode);
    //            }
    //            else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
    //            {
    //                ShowSimpleAlert(AppResources.CodeExpired);
    //            }
    //            else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else if (error.ErrorMessage.StartsWith("PHONE_NUMBER_OCCUPIED"))
    //            {
    //                ShowSimpleAlert(string.Format(AppResources.ChangePhoneNumberOccupied, (String)args[0]));
    //            }
    //            else
    //            {
    //                ShowSimpleAlert(AppResources.ErrorOccurred);
    //            }
    //        }
    //        else if (request == TLType.AccountUpdateUsername)
    //        {
    //            switch (error.ErrorMessage)
    //            {
    //                case "USERNAME_INVALID":
    //                    ShowSimpleAlert(AppResources.UsernameInvalid);
    //                    break;
    //                case "USERNAME_OCCUPIED":
    //                    ShowSimpleAlert(AppResources.UsernameInUse);
    //                    break;
    //                //case "USERNAMES_UNAVAILABLE":
    //                //    ShowSimpleAlert(AppResources.FeatureUnavailable);
    //                //    break;
    //                default:
    //                    ShowSimpleAlert(AppResources.ErrorOccurred);
    //                    break;
    //            }
    //        }
    //        else if (request == TLType.ContactsImportContacts)
    //        {
    //            if (error == null || error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleAlert(AppResources.FloodWait);
    //            }
    //            else
    //            {
    //                ShowSimpleAlert(AppResources.ErrorOccurred + "\n" + error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.AccountGetPassword || request == TLType.AccountGetTmpPassword)
    //        {
    //            if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
    //            {
    //                ShowSimpleToast(GetFloodWaitString(error.ErrorMessage));
    //            }
    //            else
    //            {
    //                ShowSimpleToast(error.ErrorMessage);
    //            }
    //        }
    //        else if (request == TLType.PaymentsSendPaymentForm)
    //        {
    //            switch (error.ErrorMessage)
    //            {
    //                case "BOT_PRECHECKOUT_FAILED":
    //                    ShowSimpleToast(AppResources.PaymentPrecheckoutFailed);
    //                    break;
    //                case "PAYMENT_FAILED":
    //                    ShowSimpleToast(AppResources.PaymentFailed);
    //                    break;
    //                default:
    //                    ShowSimpleToast(error.ErrorMessage);
    //                    break;
    //            }
    //        }
    //        else if (request == TLType.PaymentsValidateRequestedInfo)
    //        {
    //            switch (error.ErrorMessage)
    //            {
    //                case "SHIPPING_NOT_AVAILABLE":
    //                    ShowSimpleToast(AppResources.PaymentNoShippingMethod);
    //                    break;
    //                default:
    //                    ShowSimpleToast(error.ErrorMessage);
    //                    break;
    //            }
    //        }

    //        return null;
    //    }

    //    public static Toast ShowSimpleToast(String text)
    //    {
    //        if (text == null)
    //        {
    //            return null;
    //        }

    //        Toast toast = Toast.makeText(baseFragment.getParentActivity(), text, Toast.LENGTH_LONG);
    //        toast.show();
    //        return toast;
    //    }

    //    public static Dialog ShowSimpleAlert(String text)
    //    {
    //        if (text == null)
    //        {
    //            return null;
    //        }

    //        AlertDialog.Builder builder = new AlertDialog.Builder(baseFragment.getParentActivity());
    //        builder.setTitle(AppResources.AppName);
    //        builder.setMessage(text);
    //        builder.setPositiveButton(AppResources.OK, null);
    //        Dialog dialog = builder.create();
    //        baseFragment.showDialog(dialog);
    //        return dialog;
    //    }

    //    private static String GetFloodWaitString(String error)
    //    {
    //        int.TryParse(error, out int time);
    //        String timeString;
    //        if (time < 60)
    //        {
    //            timeString = LocaleController.formatPluralString("Seconds", time);
    //        }
    //        else
    //        {
    //            timeString = LocaleController.formatPluralString("Minutes", time / 60);
    //        }
    //        return LocaleController.formatString("FloodWaitTime", R.string.FloodWaitTime, timeString);
    //    }

    //    public static void ShowFloodWaitAlert(String error)
    //    {
    //        if (error == null || !error.StartsWith("FLOOD_WAIT"))
    //        {
    //            return;
    //        }
    //        int.TryParse(error, out int time);
    //        String timeString;
    //        if (time < 60)
    //        {
    //            timeString = LocaleController.formatPluralString("Seconds", time);
    //        }
    //        else
    //        {
    //            timeString = LocaleController.formatPluralString("Minutes", time / 60);
    //        }

    //        AlertDialog.Builder builder = new AlertDialog.Builder(fragment.getParentActivity());
    //        builder.setTitle(AppResources.AppName);
    //        builder.setMessage(LocaleController.formatString("FloodWaitTime", R.string.FloodWaitTime, timeString));
    //        builder.setPositiveButton(AppResources.OK, null);
    //        fragment.showDialog(builder.create(), true, null);
    //    }

    //    public static void ShowAddUserAlert(string error, bool channel)
    //    {
    //        if (error == null)
    //        {
    //            return;
    //        }
    //        AlertDialog.Builder builder = new AlertDialog.Builder(fragment.getParentActivity());
    //        builder.setTitle(AppResources.AppName);
    //        switch (error)
    //        {
    //            case "PEER_FLOOD":
    //                //        builder.setMessage(AppResources.NobodyLikesSpam2);
    //                //        builder.setNegativeButton(AppResources.MoreInfo, new DialogInterface.OnClickListener() {
    //                //        @Override
    //                //        public void onClick(DialogInterface dialogInterface, int i)
    //                //        {
    //                //            MessagesController.openByUserName("spambot", 1);
    //                //        }
    //                //});
    //                break;
    //            case "USER_BLOCKED":
    //            case "USER_BOT":
    //            case "USER_ID_INVALID":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.ChannelUserCantAdd);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.GroupUserCantAdd);
    //                }
    //                break;
    //            case "USERS_TOO_MUCH":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.ChannelUserAddLimit);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.GroupUserAddLimit);
    //                }
    //                break;
    //            case "USER_NOT_MUTUAL_CONTACT":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.ChannelUserLeftError);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.GroupUserLeftError);
    //                }
    //                break;
    //            case "ADMINS_TOO_MUCH":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.ChannelUserCantAdmin);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.GroupUserCantAdmin);
    //                }
    //                break;
    //            case "BOTS_TOO_MUCH":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.ChannelUserCantBot);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.GroupUserCantBot);
    //                }
    //                break;
    //            case "USER_PRIVACY_RESTRICTED":
    //                if (channel)
    //                {
    //                    builder.setMessage(AppResources.InviteToChannelError);
    //                }
    //                else
    //                {
    //                    builder.setMessage(AppResources.InviteToGroupError);
    //                }
    //                break;
    //            case "USERS_TOO_FEW":
    //                builder.setMessage(AppResources.CreateGroupError);
    //                break;
    //            case "USER_RESTRICTED":
    //                builder.setMessage(AppResources.UserRestricted);
    //                break;
    //            case "YOU_BLOCKED_USER":
    //                builder.setMessage(AppResources.YouBlockedUser);
    //                break;
    //            case "CHAT_ADMIN_BAN_REQUIRED":
    //            case "USER_KICKED":
    //                builder.setMessage(AppResources.AddAdminErrorBlacklisted);
    //                break;
    //            case "CHAT_ADMIN_INVITE_REQUIRED":
    //                builder.setMessage(AppResources.AddAdminErrorNotAMember);
    //                break;
    //            case "USER_ADMIN_INVALID":
    //                builder.setMessage(AppResources.AddBannedErrorAdmin);
    //                break;
    //            default:
    //                builder.setMessage(AppResources.ErrorOccurred + "\n" + error);
    //                break;
    //        }
    //        builder.setPositiveButton(AppResources.OK, null);
    //        fragment.showDialog(builder.create(), true, null);
    //    }
    //}
}
