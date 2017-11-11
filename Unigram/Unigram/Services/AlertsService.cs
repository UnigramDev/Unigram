using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Strings;

namespace Unigram.Services
{
    class AlertsService
    {
        public static void ProcessError(TLRPCError error, TLType request, params Object[] args)
        {
            if (error.ErrorCode == 406 || error.ErrorMessage == null)
            {
                return;
            }
            if (request == TLType.ChannelsJoinChannel ||
                request == TLType.ChannelsEditAdmin ||
                request == TLType.ChannelsInviteToChannel ||
                request == TLType.MessagesAddChatUser ||
                request == TLType.MessagesStartBot ||
                request == TLType.ChannelsEditBanned)
            {
                //if (fragment != null)
                //{
                ShowAddUserAlert(error.ErrorMessage, (Boolean)args[0]);
                //}
                //else
                //{
                //    if (error.ErrorMessage.Equals("PEER_FLOOD"))
                //    {
                //        ShowPeerFloodAlert(1);
                //    }
                //}
            }
            else if (request == TLType.MessagesCreateChat)
            {
                if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowFloodWaitAlert(error.ErrorMessage);
                }
                else
                {
                    ShowAddUserAlert(error.ErrorMessage, false);
                }
            }
            else if (request == TLType.ChannelsCreateChannel)
            {
                if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowFloodWaitAlert(error.ErrorMessage);
                }
            }
            else if (request == TLType.MessagesEditMessage)
            {
                if (!error.ErrorMessage.Equals("MESSAGE_NOT_MODIFIED"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.EditMessageError);
                }
            }
            else if (request == TLType.MessagesSendMessage ||
                     request == TLType.MessagesSendMedia ||
                     request == TLType.MessagesSendInlineBotResult ||
                     request == TLType.MessagesForwardMessages)
            {
                if (error.ErrorMessage.Equals("PEER_FLOOD"))
                {
                    ShowPeerFloodAlert(0);
                }
            }
            else if (request == TLType.MessagesImportChatInvite)
            {
                if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else if (error.ErrorMessage.Equals("USERS_TOO_MUCH"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.JoinToGroupErrorFull);
                }
                else
                {
                    ShowSimpleAlert(Strings.ErrorMessages.JoinToGroupErrorNotExist);
                }
            }
            else if (request == TLType.MessagesGetAttachedStickers)
            {
                //Toast.makeText(fragment.getParentActivity(), Strings.Resources.ErrorOccurred + "\n" + error.ErrorMessage, Toast.LENGTH_SHORT).show();
            }
            else if (request == TLType.AccountConfirmPhone)
            {
                if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else
                {
                    ShowSimpleAlert(error.ErrorMessage);
                }
            }
            else if (request == TLType.AuthResendCode)
            {
                if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else if (error.ErrorCode != -1000)
                {
                    ShowSimpleAlert(Strings.ErrorMessages.ErrorOccurred + "\n" + error.ErrorMessage);
                }
            }
            else if (request == TLType.AccountSendConfirmPhoneCode)
            {
                if (error.ErrorCode == 400)
                {
                    ShowSimpleAlert(Strings.ErrorMessages.CancelLinkExpired);
                }
                else if (error.ErrorMessage != null)
                {
                    if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                    {
                        ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                    }
                    else
                    {
                        ShowSimpleAlert(Strings.ErrorMessages.ErrorOccurred);
                    }
                }
            }
            else if (request == TLType.AccountChangePhone)
            {
                if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else
                {
                    ShowSimpleAlert(error.ErrorMessage);
                }
            }
            else if (request == TLType.AccountSendChangePhoneCode)
            {
                if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else if (error.ErrorMessage.StartsWith("PHONE_NUMBER_OCCUPIED"))
                {
                    ShowSimpleAlert(string.Format(Strings.ErrorMessages.ChangePhoneNumberOccupied, (String)args[0]));
                }
                else
                {
                    ShowSimpleAlert(Strings.ErrorMessages.ErrorOccurred);
                }
            }
            else if (request == TLType.AccountUpdateUsername)
            {
                switch (error.ErrorMessage)
                {
                    case "USERNAME_INVALID":
                        ShowSimpleAlert(Strings.ErrorMessages.UsernameInvalid);
                        break;
                    case "USERNAME_OCCUPIED":
                        ShowSimpleAlert(Strings.ErrorMessages.UsernameInUse);
                        break;
                    //case "USERNAMES_UNAVAILABLE":
                    //    ShowSimpleAlert(Strings.Resources.FeatureUnavailable);
                    //    break;
                    default:
                        ShowSimpleAlert(Strings.ErrorMessages.ErrorOccurred);
                        break;
                }
            }
            else if (request == TLType.ContactsImportContacts)
            {
                if (error == null || error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(Strings.ErrorMessages.FloodWait);
                }
                else
                {
                    ShowSimpleAlert(Strings.ErrorMessages.ErrorOccurred + "\n" + error.ErrorMessage);
                }
            }
            else if (request == TLType.AccountGetPassword || request == TLType.AccountGetTmpPassword)
            {
                if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleToast(GetFloodWaitString(error.ErrorMessage));
                }
                else
                {
                    ShowSimpleToast(error.ErrorMessage);
                }
            }
            else if (request == TLType.PaymentsSendPaymentForm)
            {
                switch (error.ErrorMessage)
                {
                    case "BOT_PRECHECKOUT_FAILED":
                        ShowSimpleToast(Strings.ErrorMessages.PaymentPrecheckoutFailed);
                        break;
                    case "PAYMENT_FAILED":
                        ShowSimpleToast(Strings.ErrorMessages.PaymentFailed);
                        break;
                    default:
                        ShowSimpleToast(error.ErrorMessage);
                        break;
                }
            }
            else if (request == TLType.PaymentsValidateRequestedInfo)
            {
                switch (error.ErrorMessage)
                {
                    case "SHIPPING_NOT_AVAILABLE":
                        ShowSimpleToast(Strings.ErrorMessages.PaymentNoShippingMethod);
                        break;
                    default:
                        ShowSimpleToast(error.ErrorMessage);
                        break;
                }
            }
            //// Added
            //else if (request == TLType.AuthSignUp)
            //{
            //    if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
            //    {
            //        ShowSimpleAlert(Strings.Resources.InvalidPhoneNumber);
            //    }
            //    else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
            //    {
            //        ShowSimpleAlert(Strings.Resources.InvalidCode);
            //    }
            //    else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
            //    {
            //        ShowSimpleAlert(Strings.Resources.CodeExpired);
            //    }
            //    else if (error.ErrorMessage.Contains("FIRSTNAME_INVALID"))
            //    {
            //        ShowSimpleAlert(Strings.Resources.InvalidFirstName);
            //    }
            //    else if (error.ErrorMessage.Contains("LASTNAME_INVALID"))
            //    {
            //        ShowSimpleAlert(Strings.Resources.InvalidLastName);
            //    }
            //    else
            //    {
            //        ShowSimpleAlert(error.ErrorMessage);
            //    }
            //}

            return;
        }

        private static async void ShowPeerFloodAlert(int reason)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = InAppBranding.ServiceName;
            dialog.PrimaryButtonText = Strings.DialogResources.OK;

            if (reason != 2)
            {
                dialog.SecondaryButtonText = Strings.DialogResources.MoreInfo;
                dialog.SecondaryButtonClick += (s, args) =>
                {
                    MessageHelper.NavigateToUsername("spambot", null, null, null);
                };
            }

            if (reason == 0)
            {
                dialog.Message = Strings.ErrorMessages.NobodyLikesSpam1;
            }
            else if (reason == 1)
            {
                dialog.Message = Strings.ErrorMessages.NobodyLikesSpam2;
            }
            else if (reason == 2)
            {
                //builder.setMessage((String)args[1]);
            }

            await dialog.ShowQueuedAsync();
        }

        public static void ShowSimpleToast(String text)
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

            var dialog = new TLMessageDialog();
            dialog.Title = InAppBranding.ServiceName;
            dialog.Message = text;
            dialog.PrimaryButtonText = Strings.DialogResources.OK;

            await dialog.ShowQueuedAsync();
        }

        private static String GetFloodWaitString(String error)
        {
            var time = error.ToInt32();
            return string.Format(Strings.ErrorMessages.FloodWaitTime, BindConvert.Current.CallDuration(time));
        }

        public static void ShowFloodWaitAlert(String error)
        {
            if (error == null || !error.StartsWith("FLOOD_WAIT"))
            {
                return;
            }

            ShowSimpleAlert(GetFloodWaitString(error));
        }

        public static async void ShowAddUserAlert(string error, bool channel)
        {
            if (error == null)
            {
                return;
            }

            var dialog = new TLMessageDialog();
            dialog.Title = InAppBranding.ServiceName;
            dialog.PrimaryButtonText = Strings.DialogResources.OK;

            switch (error)
            {
                case "PEER_FLOOD":
                    dialog.Message = Strings.ErrorMessages.NobodyLikesSpam2;
                    dialog.SecondaryButtonText = Strings.DialogResources.MoreInfo;
                    dialog.SecondaryButtonClick += (s, args) =>
                    {
                        MessageHelper.NavigateToUsername("spambot", null, null, null);
                    };
                    break;
                case "USER_BLOCKED":
                case "USER_BOT":
                case "USER_ID_INVALID":
                    dialog.Message = channel ? Strings.ErrorMessages.ChannelUserCantAdd : Strings.ErrorMessages.GroupUserCantAdd;
                    break;
                case "USERS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ErrorMessages.ChannelUserAddLimit : Strings.ErrorMessages.GroupUserAddLimit;
                    break;
                case "USER_NOT_MUTUAL_CONTACT":
                    dialog.Message = channel ? Strings.ErrorMessages.ChannelUserLeftError : Strings.ErrorMessages.GroupUserLeftError;
                    break;
                case "ADMINS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ErrorMessages.ChannelUserCantAdmin : Strings.ErrorMessages.GroupUserCantAdmin;
                    break;
                case "BOTS_TOO_MUCH":
                    dialog.Message = channel ? Strings.ErrorMessages.ChannelUserCantBot : Strings.ErrorMessages.GroupUserCantBot;
                    break;
                case "USER_PRIVACY_RESTRICTED":
                    dialog.Message = channel ? Strings.ErrorMessages.InviteToChannelError : Strings.ErrorMessages.InviteToGroupError;
                    break;
                case "USERS_TOO_FEW":
                    dialog.Message = Strings.ErrorMessages.CreateGroupError;
                    break;
                case "USER_RESTRICTED":
                    dialog.Message = Strings.ErrorMessages.UserRestricted;
                    break;
                case "YOU_BLOCKED_USER":
                    dialog.Message = Strings.ErrorMessages.YouBlockedUser;
                    break;
                case "CHAT_ADMIN_BAN_REQUIRED":
                case "USER_KICKED":
                    dialog.Message = Strings.ErrorMessages.AddAdminErrorBlacklisted;
                    break;
                case "CHAT_ADMIN_INVITE_REQUIRED":
                    dialog.Message = Strings.ErrorMessages.AddAdminErrorNotAMember;
                    break;
                case "USER_ADMIN_INVALID":
                    dialog.Message = Strings.ErrorMessages.AddBannedErrorAdmin;
                    break;
                default:
                    dialog.Message = Strings.ErrorMessages.ErrorOccurred + "\n" + error;
                    break;
            }

            await dialog.ShowQueuedAsync();
        }
    }
}
