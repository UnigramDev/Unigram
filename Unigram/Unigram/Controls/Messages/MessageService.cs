using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class MessageService : SimpleButton
    {
        public void UpdateMessage(MessageViewModel message)
        {
            Tag = message;

            var content = Content as TextBlock;
            if (content == null)
            {
                return;
            }

            var entities = GetEntities(message, true);
            if (entities.Text != null)
            {
                var span = new Span();
                content.Inlines.Clear();
                content.Inlines.Add(span);

                ReplaceEntities(message, span, entities.Text, entities.Entities);
            }
        }

        public static string GetText(MessageViewModel message)
        {
            return GetEntities(message, false).Text;
        }

        private static (string Text, IList<TextEntity> Entities) GetEntities(MessageViewModel message, bool active)
        {
            switch (message.Content)
            {
                case MessageBasicGroupChatCreate basicGroupChatCreate:
                    return UpdateBasicGroupChatCreate(message, basicGroupChatCreate, active);
                case MessageChatAddMembers chatAddMembers:
                    return UpdateChatAddMembers(message, chatAddMembers, active);
                case MessageChatChangePhoto chatChangePhoto:
                    return UpdateChatChangePhoto(message, chatChangePhoto, active);
                case MessageChatChangeTitle chatChangeTitle:
                    return UpdateChatChangeTitle(message, chatChangeTitle, active);
                case MessageChatDeleteMember chatDeleteMember:
                    return UpdateChatDeleteMember(message, chatDeleteMember, active);
                case MessageChatDeletePhoto chatDeletePhoto:
                    return UpdateChatDeletePhoto(message, chatDeletePhoto, active);
                case MessageChatJoinByLink chatJoinByLink:
                    return UpdateChatJoinByLink(message, chatJoinByLink, active);
                case MessageChatSetTtl chatSetTtl:
                    return UpdateChatSetTtl(message, chatSetTtl, active);
                case MessageChatUpgradeFrom chatUpgradeFrom:
                    return UpdateChatUpgradeFrom(message, chatUpgradeFrom, active);
                case MessageChatUpgradeTo chatUpgradeTo:
                    return UpdateChatUpgradeTo(message, chatUpgradeTo, active);
                case MessageContactRegistered contactRegistered:
                    return UpdateContactRegistered(message, contactRegistered, active);
                case MessageCustomServiceAction customServiceAction:
                    return UpdateCustomServiceAction(message, customServiceAction, active);
                case MessageGameScore gameScore:
                    return UpdateGameScore(message, gameScore, active);
                case MessagePaymentSuccessful paymentSuccessful:
                    return UpdatePaymentSuccessful(message, paymentSuccessful, active);
                case MessagePinMessage pinMessage:
                    return UpdatePinMessage(message, pinMessage, active);
                case MessageScreenshotTaken screenshotTaken:
                    return UpdateScreenshotTaken(message, screenshotTaken, active);
                case MessageSupergroupChatCreate supergroupChatCreate:
                    return UpdateSupergroupChatCreate(message, supergroupChatCreate, active);
                case MessageExpiredPhoto expiredPhoto:
                    return UpdateExpiredPhoto(message, expiredPhoto, active);
                case MessageExpiredVideo expiredVideo:
                    return UpdateExpiredVideo(message, expiredVideo, active);
                // Local types:
                case MessageChatEvent chatEvent:
                    switch (chatEvent.Event.Action)
                    {
                        case ChatEventSignMessagesToggled signMessagesToggled:
                            return UpdateSignMessagesToggled(message, signMessagesToggled, active);
                        case ChatEventStickerSetChanged stickerSetChanged:
                            return UpdateStickerSetChanged(message, stickerSetChanged, active);
                        case ChatEventInvitesToggled invitesToggled:
                            return UpdateInvitesToggled(message, invitesToggled, active);
                        case ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled:
                            return UpdateIsAllHistoryAvailableToggled(message, isAllHistoryAvailableToggled, active);
                        case ChatEventMessageUnpinned messageUnpinned:
                            return UpdateMessageUnpinned(message, messageUnpinned, active);
                        case ChatEventMessageDeleted messageDeleted:
                            return chatEvent.IsFull ? GetEntities(new MessageViewModel(message.ProtoService, message.Delegate, messageDeleted.Message), active) : UpdateMessageDeleted(message, messageDeleted, active);
                        case ChatEventMessageEdited messageEdited:
                            return chatEvent.IsFull ? GetEntities(new MessageViewModel(message.ProtoService, message.Delegate, messageEdited.NewMessage), active) : UpdateMessageEdited(message, messageEdited, active);
                        case ChatEventDescriptionChanged descriptionChanged:
                            return UpdateDescriptionChanged(message, descriptionChanged, active);
                        case ChatEventMessagePinned messagePinned:
                            return chatEvent.IsFull ? GetEntities(new MessageViewModel(message.ProtoService, message.Delegate, messagePinned.Message), active) : UpdateMessagePinned(message, messagePinned, active);
                        case ChatEventUsernameChanged usernameChanged:
                            return UpdateUsernameChanged(message, usernameChanged, active);
                        default:
                            return (null, null);
                    }
                case MessageHeaderDate headerDate:
                    return UpdateHeaderDate(message, headerDate, active);
                default:
                    return (null, null);
            }
        }

        #region Local

        private static (string Text, IList<TextEntity> Entities) UpdateHeaderDate(MessageViewModel message, MessageHeaderDate headerDate, bool active)
        {
            return (DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(message.Date)), null);
        }

        #endregion

        #region Event log

        private static (string Text, IList<TextEntity> Entities) UpdateSignMessagesToggled(MessageViewModel message, ChatEventSignMessagesToggled signMessagesToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (signMessagesToggled.SignMessages)
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledSignaturesOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledSignaturesOff, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateStickerSetChanged(MessageViewModel message, ChatEventStickerSetChanged stickerSetChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (stickerSetChanged.NewStickerSetId == 0)
            {
                content = ReplaceWithLink(Strings.Android.EventLogRemovedStickersSet, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogChangedStickersSet, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateInvitesToggled(MessageViewModel message, ChatEventInvitesToggled invitesToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (invitesToggled.AnyoneCanInvite)
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesOff, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateIsAllHistoryAvailableToggled(MessageViewModel message, ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (isAllHistoryAvailableToggled.IsAllHistoryAvailable)
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesHistoryOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesHistoryOff, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageUnpinned(MessageViewModel message, ChatEventMessageUnpinned messageUnpinned, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Android.EventLogUnpinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageDeleted(MessageViewModel message, ChatEventMessageDeleted messageDeleted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Android.EventLogDeletedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageEdited(MessageViewModel message, ChatEventMessageEdited messageEdited, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (messageEdited.NewMessage.Content is MessageText)
            {
                content = ReplaceWithLink(Strings.Android.EventLogEditedMessages, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogEditedCaption, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateDescriptionChanged(MessageViewModel message, ChatEventDescriptionChanged descriptionChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (message.IsChannelPost)
            {
                content = ReplaceWithLink(Strings.Android.EventLogEditedChannelDescription, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogEditedGroupDescription, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessagePinned(MessageViewModel message, ChatEventMessagePinned messagePinned, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Android.EventLogPinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateUsernameChanged(MessageViewModel message, ChatEventUsernameChanged usernameChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (string.IsNullOrEmpty(usernameChanged.NewUsername))
            {
                content = ReplaceWithLink(Strings.Android.EventLogRemovedGroupLink, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.EventLogChangedGroupLink, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        #endregion

        private static (string, IList<TextEntity>) UpdateBasicGroupChatCreate(MessageViewModel message, MessageBasicGroupChatCreate basicGroupChatCreate, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                content = Strings.Android.ActionYouCreateGroup;
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.ActionCreateGroup, "un1", message.GetSenderUser(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatAddMembers(MessageViewModel message, MessageChatAddMembers chatAddMembers, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            int singleUserId = 0;
            if (singleUserId == 0 && chatAddMembers.MemberUserIds.Count == 1)
            {
                singleUserId = chatAddMembers.MemberUserIds[0];
            }

            var fromUser = message.GetSenderUser();

            if (singleUserId != 0)
            {
                var whoUser = message.ProtoService.GetUser(singleUserId);
                if (singleUserId == message.SenderUserId)
                {
                    var chat = message.GetChat();
                    var supergroup = chat.Type as ChatTypeSupergroup;
                    if (supergroup != null && supergroup.IsChannel)
                    {
                        if (singleUserId == message.ProtoService.GetMyId())
                        {
                            content = Strings.Android.ChannelJoined;
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Android.EventLogChannelJoined, "un1", fromUser, ref entities);
                        }
                    }
                    else
                    {
                        if (supergroup != null && !supergroup.IsChannel)
                        {
                            if (singleUserId == message.ProtoService.GetMyId())
                            {
                                content = Strings.Android.ChannelMegaJoined;
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Android.ActionAddUserSelfMega, "un1", fromUser, ref entities);
                            }
                        }
                        else if (message.IsOutgoing)
                        {
                            content = Strings.Android.ActionAddUserSelfYou;
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Android.ActionAddUserSelf, "un1", fromUser, ref entities);
                        }
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionYouAddUser, "un2", whoUser, ref entities);
                    }
                    else if (singleUserId == message.ProtoService.GetMyId())
                    {
                        var chat = message.GetChat();
                        var supergroup = chat.Type as ChatTypeSupergroup;
                        if (supergroup != null)
                        {
                            if (supergroup.IsChannel)
                            {
                                content = ReplaceWithLink(Strings.Android.ChannelAddedBy, "un1", fromUser, ref entities);
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Android.MegaAddedBy, "un1", fromUser, ref entities);
                            }
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Android.ActionAddUserYou, "un1", fromUser, ref entities);
                        }
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionAddUser, "un1", fromUser, ref entities);
                        content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                    }
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Android.ActionYouAddUser, "un2", chatAddMembers.MemberUserIds, message.ProtoService, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionAddUser, "un1", message.GetSenderUser(), ref entities);
                    content = ReplaceWithLink(content, "un2", chatAddMembers.MemberUserIds, message.ProtoService, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatChangePhoto(MessageViewModel message, MessageChatChangePhoto chatChangePhoto, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                content = Strings.Android.ActionChannelChangedPhoto;
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Android.ActionYouChangedPhoto;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionChangedPhoto, "un1", message.GetSenderUser(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatChangeTitle(MessageViewModel message, MessageChatChangeTitle chatChangeTitle, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                content = Strings.Android.ActionChannelChangedTitle.Replace("un2", chatChangeTitle.Title);
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Android.ActionYouChangedTitle.Replace("un2", chatChangeTitle.Title);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionChangedTitle.Replace("un2", chatChangeTitle.Title), "un1", message.GetSenderUser(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatDeleteMember(MessageViewModel message, MessageChatDeleteMember chatDeleteMember, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (chatDeleteMember.UserId == message.SenderUserId)
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Android.ActionYouLeftUser;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionLeftUser, "un1", fromUser, ref entities);
                }
            }
            else
            {
                var whoUser = message.ProtoService.GetUser(chatDeleteMember.UserId);
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Android.ActionYouKickUser, "un2", whoUser, ref entities);
                }
                else if (chatDeleteMember.UserId == message.ProtoService.GetMyId())
                {
                    content = ReplaceWithLink(Strings.Android.ActionKickUserYou, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionKickUser, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatDeletePhoto(MessageViewModel message, MessageChatDeletePhoto chatDeletePhoto, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                content = Strings.Android.ActionChannelRemovedPhoto;
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Android.ActionYouRemovedPhoto;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionRemovedPhoto, "un1", message.GetSenderUser(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatJoinByLink(MessageViewModel message, MessageChatJoinByLink chatJoinByLink, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                content = Strings.Android.ActionInviteYou;
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.ActionInviteUser, "un1", message.GetSenderUser(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatSetTtl(MessageViewModel message, MessageChatSetTtl chatSetTtl, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (chatSetTtl.Ttl != 0)
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(Strings.Android.MessageLifetimeChangedOutgoing, Locale.FormatTTLString(chatSetTtl.Ttl));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.MessageLifetimeChanged, "un1", Locale.FormatTTLString(chatSetTtl.Ttl)), "un1", message.GetSenderUser(), ref entities);
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Android.MessageLifetimeYouRemoved;
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.MessageLifetimeRemoved, "un1"), "un1", message.GetSenderUser(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeFrom(MessageViewModel message, MessageChatUpgradeFrom chatUpgradeFrom, bool active)
        {
            return (Strings.Android.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeTo(MessageViewModel message, MessageChatUpgradeTo chatUpgradeTo, bool active)
        {
            return (Strings.Android.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateContactRegistered(MessageViewModel message, MessageContactRegistered contactRegistered, bool active)
        {
            return (string.Format(Strings.Android.NotificationContactJoined, message.GetSenderUser().GetFullName()), null);
        }

        private static (string, IList<TextEntity>) UpdateCustomServiceAction(MessageViewModel message, MessageCustomServiceAction customServiceAction, bool active)
        {
            return (customServiceAction.Text, null);
        }

        private static (string, IList<TextEntity>) UpdateGameScore(MessageViewModel message, MessageGameScore gameScore, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var game = GetGame(message);
            if (game == null)
            {
                if (message.SenderUserId == message.ProtoService.GetMyId())
                {
                    content = string.Format(Strings.Android.ActionYouScored, Locale.Declension("Points", gameScore.Score));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.ActionUserScored, Locale.Declension("Points", gameScore.Score)), "un1", message.GetSenderUser(), ref entities);
                }
            }
            else
            {
                if (message.SenderUserId == message.ProtoService.GetMyId())
                {
                    content = string.Format(Strings.Android.ActionYouScoredInGame, Locale.Declension("Points", gameScore.Score));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.ActionUserScoredInGame, Locale.Declension("Points", gameScore.Score)), "un1", message.GetSenderUser(), ref entities);
                }

                content = ReplaceWithLink(content, "un2", game, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdatePaymentSuccessful(MessageViewModel message, MessagePaymentSuccessful paymentSuccessful, bool active)
        {
            var content = string.Empty;

            var invoice = GetInvoice(message);
            var chat = message.GetChat();

            if (invoice != null)
            {
                content = string.Format(Strings.Android.PaymentSuccessfullyPaid, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ProtoService.GetTitle(chat), invoice.Title);
            }
            else
            {
                content = string.Format(Strings.Android.PaymentSuccessfullyPaidNoItem, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ProtoService.GetTitle(chat));
            }

            return (content, null);
        }

        private static (string, IList<TextEntity>) UpdatePinMessage(MessageViewModel message, MessagePinMessage pinMessage, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var sender = message.GetSenderUser();
            var chat = message.GetChat();

            var reply = message.ReplyToMessage;

            if (reply == null)
            {
                content = ReplaceWithLink(Strings.Android.ActionPinnedNoText, "un1", sender ?? (BaseObject)chat, ref entities);
            }
            else
            {
                if (reply.Content is MessageAudio)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedMusic, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVideo)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedVideo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageAnimation)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedGif, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVoiceNote)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedVoice, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVideoNote)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedRound, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageSticker)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedSticker, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageDocument)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedFile, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageLocation location)
                {
                    content = ReplaceWithLink(location.LivePeriod > 0 ? Strings.Android.ActionPinnedGeoLive : Strings.Android.ActionPinnedGeo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVenue)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedGeo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageContact)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedContact, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessagePhoto)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedPhoto, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageGame game)
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.ActionPinnedGame, "\uD83C\uDFAE " + game.Game.Title), "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageText text)
                {
                    var mess = text.Text.Text.Replace('\n', ' ');
                    if (mess.Length > 20)
                    {
                        mess = mess.Substring(0, Math.Min(mess.Length, 20)) + "...";
                    }

                    content = ReplaceWithLink(string.Format(Strings.Android.ActionPinnedText, mess), "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedNoText, "un1", sender ?? (BaseObject)chat, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateScreenshotTaken(MessageViewModel message, MessageScreenshotTaken screenshotTaken, bool active)
        {
            string content;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                content = Strings.Android.ActionTakeScreenshootYou;
            }
            else
            {
                content = ReplaceWithLink(Strings.Android.ActionTakeScreenshoot, "un1", message.GetSenderUser(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateSupergroupChatCreate(MessageViewModel message, MessageSupergroupChatCreate supergroupChatCreate, bool active)
        {
            var content = string.Empty;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                content = Strings.Android.ActionCreateChannel;
            }
            else
            {
                content = Strings.Android.ActionCreateMega;
            }

            return (content, null);
        }

        private static (string, IList<TextEntity>) UpdateExpiredPhoto(MessageViewModel message, MessageExpiredPhoto expiredPhoto, bool active)
        {
            return (Strings.Android.AttachPhotoExpired, null);
        }

        private static (string, IList<TextEntity>) UpdateExpiredVideo(MessageViewModel message, MessageExpiredVideo expiredVideo, bool active)
        {
            return (Strings.Android.AttachVideoExpired, null);
        }



        public static string ReplaceWithLink(string source, string param, BaseObject obj, ref List<TextEntity> entities)
        {
            var start = source.IndexOf(param);
            if (start >= 0)
            {
                String name;
                String id;
                if (obj is User user)
                {
                    name = user.GetFullName();
                    id = "tg-user://" + user.Id;
                }
                else if (obj is Chat chat)
                {
                    name = chat.Title;
                    id = "tg-chat://" + chat.Id;
                }
                else if (obj is Game game)
                {
                    name = game.Title;
                    id = "tg-game://";
                }
                else
                {
                    name = "";
                    id = "0";
                }

                name = name.Replace('\n', ' ');
                source = source.Replace(param, name);

                if (entities != null)
                {
                    if (obj is User mention)
                    {
                        entities.Add(new TextEntity(start, name.Length, new TextEntityTypeMentionName(mention.Id)));
                    }
                    else
                    {
                        entities.Add(new TextEntity(start, name.Length, new TextEntityTypeTextUrl(id)));
                    }
                }
            }

            return source;
        }

        private static string ReplaceWithLink(string source, string param, IList<int> uids, IProtoService cacheService, ref List<TextEntity> entities)
        {
            var index = 0;
            int start = index = source.IndexOf(param);
            if (start >= 0)
            {
                var names = new StringBuilder();
                for (int a = 0; a < uids.Count; a++)
                {
                    User user = null;
                    if (user == null)
                    {
                        user = cacheService.GetUser(uids[a]);
                    }
                    if (user != null)
                    {
                        var name = user.GetFullName();
                        if (names.Length != 0)
                        {
                            names.Append(", ");
                        }

                        start = index + names.Length;
                        names.Append(name);

                        if (entities != null)
                        {
                            entities.Add(new TextEntity(start, name.Length, new TextEntityTypeMentionName(user.Id)));
                        }
                    }
                }

                source = source.Replace(param, names.ToString());
            }

            return source;
        }

        private void ReplaceEntities(MessageViewModel message, Span span, string text, IList<TextEntity> entities)
        {
            if (entities == null)
            {
                span.Inlines.Add(new Run { Text = text });
                return;
            }

            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.Type is TextEntityTypeBold)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeCode)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypeMention || entity.Type is TextEntityTypeHashtag || entity.Type is TextEntityTypeBotCommand)
                {
                    var data = text.Substring(entity.Offset, entity.Length);

                    var hyperlink = new Hyperlink();
                    //hyperlink.Click += (s, args) => Hyperlink_Navigate(type, data, message);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);

                    //if (entity is TLMessageEntityUrl)
                    //{
                    //    SetEntity(hyperlink, (string)data);
                    //}
                }
                else if (entity.Type is TextEntityTypeTextUrl || entity.Type is TextEntityTypeMentionName)
                {
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                    }
                    else if (entity.Type is TextEntityTypeMentionName mentionName)
                    {
                        data = mentionName.UserId;
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, null);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    //hyperlink.Foreground = foreground;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = new SolidColorBrush(Colors.White);
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    span.Inlines.Add(hyperlink);

                    //if (entity is TLMessageEntityTextUrl textUrl)
                    //{
                    //    SetEntity(hyperlink, textUrl.Url);
                    //    ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    //}
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
        }

        private void Entity_Click(MessageViewModel message, TextEntityType type, string data)
        {
            if (type is TextEntityTypeBotCommand)
            {
                message.Delegate.SendBotCommand(data);
            }
            else if (type is TextEntityTypeEmailAddress)
            {

            }
            else if (type is TextEntityTypeHashtag)
            {

            }
            else if (type is TextEntityTypeMention)
            {
                message.Delegate.OpenUsername(data);
            }
            else if (type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true);
            }
            else if (type is TextEntityTypeUrl)
            {
                message.Delegate.OpenUrl(data, false);
            }
        }



        private static Game GetGame(MessageViewModel message)
        {
            var reply = message.ReplyToMessage;
            if (reply == null)
            {
                return null;
            }

            var game = reply.Content as MessageGame;
            if (game == null)
            {
                return null;
            }

            return game.Game;
        }

        private static MessageInvoice GetInvoice(MessageViewModel message)
        {
            var reply = message.ReplyToMessage;
            if (reply == null)
            {
                return null;
            }

            var invoice = reply.Content as MessageInvoice;
            if (invoice == null)
            {
                return null;
            }

            return invoice;
        }
    }
}
