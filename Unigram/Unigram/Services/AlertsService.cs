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
                    ShowSimpleAlert(AppResources.EditMessageError);
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
                    ShowSimpleAlert(AppResources.FloodWait);
                }
                else if (error.ErrorMessage.Equals("USERS_TOO_MUCH"))
                {
                    ShowSimpleAlert(AppResources.JoinToGroupErrorFull);
                }
                else
                {
                    ShowSimpleAlert(AppResources.JoinToGroupErrorNotExist);
                }
            }
            else if (request == TLType.MessagesGetAttachedStickers)
            {
                //Toast.makeText(fragment.getParentActivity(), AppResources.ErrorOccurred + "\n" + error.ErrorMessage, Toast.LENGTH_SHORT).show();
            }
            else if (request == TLType.AccountConfirmPhone)
            {
                if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(AppResources.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(AppResources.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(AppResources.FloodWait);
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
                    ShowSimpleAlert(AppResources.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(AppResources.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(AppResources.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(AppResources.FloodWait);
                }
                else if (error.ErrorCode != -1000)
                {
                    ShowSimpleAlert(AppResources.ErrorOccurred + "\n" + error.ErrorMessage);
                }
            }
            else if (request == TLType.AccountSendConfirmPhoneCode)
            {
                if (error.ErrorCode == 400)
                {
                    ShowSimpleAlert(AppResources.CancelLinkExpired);
                }
                else if (error.ErrorMessage != null)
                {
                    if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                    {
                        ShowSimpleAlert(AppResources.FloodWait);
                    }
                    else
                    {
                        ShowSimpleAlert(AppResources.ErrorOccurred);
                    }
                }
            }
            else if (request == TLType.AccountChangePhone)
            {
                if (error.ErrorMessage.Contains("PHONE_NUMBER_INVALID"))
                {
                    ShowSimpleAlert(AppResources.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(AppResources.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(AppResources.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(AppResources.FloodWait);
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
                    ShowSimpleAlert(AppResources.InvalidPhoneNumber);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
                {
                    ShowSimpleAlert(AppResources.InvalidCode);
                }
                else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
                {
                    ShowSimpleAlert(AppResources.CodeExpired);
                }
                else if (error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(AppResources.FloodWait);
                }
                else if (error.ErrorMessage.StartsWith("PHONE_NUMBER_OCCUPIED"))
                {
                    ShowSimpleAlert(string.Format(AppResources.ChangePhoneNumberOccupied, (String)args[0]));
                }
                else
                {
                    ShowSimpleAlert(AppResources.ErrorOccurred);
                }
            }
            else if (request == TLType.AccountUpdateUsername)
            {
                switch (error.ErrorMessage)
                {
                    case "USERNAME_INVALID":
                        ShowSimpleAlert(AppResources.UsernameInvalid);
                        break;
                    case "USERNAME_OCCUPIED":
                        ShowSimpleAlert(AppResources.UsernameInUse);
                        break;
                    //case "USERNAMES_UNAVAILABLE":
                    //    ShowSimpleAlert(AppResources.FeatureUnavailable);
                    //    break;
                    default:
                        ShowSimpleAlert(AppResources.ErrorOccurred);
                        break;
                }
            }
            else if (request == TLType.ContactsImportContacts)
            {
                if (error == null || error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    ShowSimpleAlert(AppResources.FloodWait);
                }
                else
                {
                    ShowSimpleAlert(AppResources.ErrorOccurred + "\n" + error.ErrorMessage);
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
                        ShowSimpleToast(AppResources.PaymentPrecheckoutFailed);
                        break;
                    case "PAYMENT_FAILED":
                        ShowSimpleToast(AppResources.PaymentFailed);
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
                        ShowSimpleToast(AppResources.PaymentNoShippingMethod);
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
            //        ShowSimpleAlert(AppResources.InvalidPhoneNumber);
            //    }
            //    else if (error.ErrorMessage.Contains("PHONE_CODE_EMPTY") || error.ErrorMessage.Contains("PHONE_CODE_INVALID"))
            //    {
            //        ShowSimpleAlert(AppResources.InvalidCode);
            //    }
            //    else if (error.ErrorMessage.Contains("PHONE_CODE_EXPIRED"))
            //    {
            //        ShowSimpleAlert(AppResources.CodeExpired);
            //    }
            //    else if (error.ErrorMessage.Contains("FIRSTNAME_INVALID"))
            //    {
            //        ShowSimpleAlert(AppResources.InvalidFirstName);
            //    }
            //    else if (error.ErrorMessage.Contains("LASTNAME_INVALID"))
            //    {
            //        ShowSimpleAlert(AppResources.InvalidLastName);
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
            dialog.Title = AppResources.AppName;
            dialog.PrimaryButtonText = AppResources.OK;

            if (reason != 2)
            {
                dialog.SecondaryButtonText = AppResources.MoreInfo;
                dialog.SecondaryButtonClick += (s, args) =>
                {
                    MessageHelper.NavigateToUsername("spambot", null, null, null);
                };
            }

            if (reason == 0)
            {
                dialog.Message = AppResources.NobodyLikesSpam1;
            }
            else if (reason == 1)
            {
                dialog.Message = AppResources.NobodyLikesSpam2;
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
            dialog.Title = AppResources.AppName;
            dialog.Message = text;
            dialog.PrimaryButtonText = AppResources.OK;

            await dialog.ShowQueuedAsync();
        }

        private static String GetFloodWaitString(String error)
        {
            var time = error.ToInt32();
            return string.Format(AppResources.FloodWaitTime, BindConvert.Current.CallDuration(time));
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
            dialog.Title = AppResources.AppName;
            dialog.PrimaryButtonText = AppResources.OK;

            switch (error)
            {
                case "PEER_FLOOD":
                    dialog.Message = AppResources.NobodyLikesSpam2;
                    dialog.SecondaryButtonText = AppResources.MoreInfo;
                    dialog.SecondaryButtonClick += (s, args) =>
                    {
                        MessageHelper.NavigateToUsername("spambot", null, null, null);
                    };
                    break;
                case "USER_BLOCKED":
                case "USER_BOT":
                case "USER_ID_INVALID":
                    dialog.Message = channel ? AppResources.ChannelUserCantAdd : AppResources.GroupUserCantAdd;
                    break;
                case "USERS_TOO_MUCH":
                    dialog.Message = channel ? AppResources.ChannelUserAddLimit : AppResources.GroupUserAddLimit;
                    break;
                case "USER_NOT_MUTUAL_CONTACT":
                    dialog.Message = channel ? AppResources.ChannelUserLeftError : AppResources.GroupUserLeftError;
                    break;
                case "ADMINS_TOO_MUCH":
                    dialog.Message = channel ? AppResources.ChannelUserCantAdmin : AppResources.GroupUserCantAdmin;
                    break;
                case "BOTS_TOO_MUCH":
                    dialog.Message = channel ? AppResources.ChannelUserCantBot : AppResources.GroupUserCantBot;
                    break;
                case "USER_PRIVACY_RESTRICTED":
                    dialog.Message = channel ? AppResources.InviteToChannelError : AppResources.InviteToGroupError;
                    break;
                case "USERS_TOO_FEW":
                    dialog.Message = AppResources.CreateGroupError;
                    break;
                case "USER_RESTRICTED":
                    dialog.Message = AppResources.UserRestricted;
                    break;
                case "YOU_BLOCKED_USER":
                    dialog.Message = AppResources.YouBlockedUser;
                    break;
                case "CHAT_ADMIN_BAN_REQUIRED":
                case "USER_KICKED":
                    dialog.Message = AppResources.AddAdminErrorBlacklisted;
                    break;
                case "CHAT_ADMIN_INVITE_REQUIRED":
                    dialog.Message = AppResources.AddAdminErrorNotAMember;
                    break;
                case "USER_ADMIN_INVALID":
                    dialog.Message = AppResources.AddBannedErrorAdmin;
                    break;
                default:
                    dialog.Message = AppResources.ErrorOccurred + "\n" + error;
                    break;
            }

            await dialog.ShowQueuedAsync();
        }
    }
}
