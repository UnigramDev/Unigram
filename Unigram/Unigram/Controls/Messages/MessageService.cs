using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Td.Api;
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
                case MessagePassportDataSent passportDataSent:
                    return UpdatePassportDataSent(message, passportDataSent, active);
                case MessagePaymentSuccessful paymentSuccessful:
                    return UpdatePaymentSuccessful(message, paymentSuccessful, active);
                case MessagePinMessage pinMessage:
                    return UpdatePinMessage(message, pinMessage, active);
                case MessageScreenshotTaken screenshotTaken:
                    return UpdateScreenshotTaken(message, screenshotTaken, active);
                case MessageSupergroupChatCreate supergroupChatCreate:
                    return UpdateSupergroupChatCreate(message, supergroupChatCreate, active);
                case MessageWebsiteConnected websiteConnected:
                    return UpdateWebsiteConnected(message, websiteConnected, active);
                case MessageExpiredPhoto expiredPhoto:
                    return UpdateExpiredPhoto(message, expiredPhoto, active);
                case MessageExpiredVideo expiredVideo:
                    return UpdateExpiredVideo(message, expiredVideo, active);
                // Local types:
                case MessageChatEvent chatEvent:
                    switch (chatEvent.Action)
                    {
                        case ChatEventSignMessagesToggled signMessagesToggled:
                            return UpdateSignMessagesToggled(message, signMessagesToggled, active);
                        case ChatEventStickerSetChanged stickerSetChanged:
                            return UpdateStickerSetChanged(message, stickerSetChanged, active);
                        case ChatEventInvitesToggled invitesToggled:
                            return UpdateInvitesToggled(message, invitesToggled, active);
                        case ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled:
                            return UpdateIsAllHistoryAvailableToggled(message, isAllHistoryAvailableToggled, active);
                        case ChatEventLinkedChatChanged linkedChatChanged:
                            return UpdateLinkedChatChanged(message, linkedChatChanged, active);
                        case ChatEventLocationChanged locationChanged:
                            return UpdateLocationChanged(message, locationChanged, active);
                        case ChatEventMessageUnpinned messageUnpinned:
                            return UpdateMessageUnpinned(message, messageUnpinned, active);
                        case ChatEventMessageDeleted messageDeleted:
                            return UpdateMessageDeleted(message, messageDeleted, active);
                        case ChatEventMessageEdited messageEdited:
                            return UpdateMessageEdited(message, messageEdited, active);
                        case ChatEventDescriptionChanged descriptionChanged:
                            return UpdateDescriptionChanged(message, descriptionChanged, active);
                        case ChatEventMessagePinned messagePinned:
                            return UpdateMessagePinned(message, messagePinned, active);
                        case ChatEventUsernameChanged usernameChanged:
                            return UpdateUsernameChanged(message, usernameChanged, active);
                        case ChatEventPollStopped pollStopped:
                            return UpdatePollStopped(message, pollStopped, active);
                        case ChatEventSlowModeDelayChanged slowModeDelayChanged:
                            return UpdateSlowModeDelayChanged(message, slowModeDelayChanged, active);
                        default:
                            return (null, null);
                    }
                case MessageHeaderDate headerDate:
                    return UpdateHeaderDate(message, headerDate, active);
                default:
                    return (string.Empty, null);
            }
        }

        #region Local

        private static (string Text, IList<TextEntity> Entities) UpdateHeaderDate(MessageViewModel message, MessageHeaderDate headerDate, bool active)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                return (string.Format(Strings.Resources.MessageScheduledOn, BindConvert.DayGrouping(Utils.UnixTimestampToDateTime(sendAtDate.SendDate))), null);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                return (Strings.Resources.MessageScheduledUntilOnline, null);
            }

            return (BindConvert.DayGrouping(Utils.UnixTimestampToDateTime(message.Date)), null);
        }

        #endregion

        #region Event log

        private static (string Text, IList<TextEntity> Entities) UpdateSlowModeDelayChanged(MessageViewModel message, ChatEventSlowModeDelayChanged slowModeDelayChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (slowModeDelayChanged.NewSlowModeDelay > 0)
            {
                if (slowModeDelayChanged.NewSlowModeDelay < 60)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.EventLogToggledSlowmodeOn, string.Format(Strings.Resources.SlowmodeSeconds, slowModeDelayChanged.NewSlowModeDelay)), "un1", fromUser, ref entities);
                }
                else if (slowModeDelayChanged.NewSlowModeDelay < 60 * 60)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.EventLogToggledSlowmodeOn, string.Format(Strings.Resources.SlowmodeMinutes, slowModeDelayChanged.NewSlowModeDelay / 60)), "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.EventLogToggledSlowmodeOn, string.Format(Strings.Resources.SlowmodeHours, slowModeDelayChanged.NewSlowModeDelay / 60 / 60)), "un1", fromUser, ref entities);
                }
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledSlowmodeOff, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateSignMessagesToggled(MessageViewModel message, ChatEventSignMessagesToggled signMessagesToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (signMessagesToggled.SignMessages)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledSignaturesOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledSignaturesOff, "un1", fromUser, ref entities);
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
                content = ReplaceWithLink(Strings.Resources.EventLogRemovedStickersSet, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogChangedStickersSet, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateInvitesToggled(MessageViewModel message, ChatEventInvitesToggled invitesToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (invitesToggled.CanInviteUsers)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledInvitesOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledInvitesOff, "un1", fromUser, ref entities);
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
                content = ReplaceWithLink(Strings.Resources.EventLogToggledInvitesHistoryOn, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogToggledInvitesHistoryOff, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateLinkedChatChanged(MessageViewModel message, ChatEventLinkedChatChanged linkedChatChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (message.IsChannelPost)
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogChangedLinkedGroup, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ProtoService.GetChat(linkedChatChanged.NewLinkedChatId), ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogRemovedLinkedGroup, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ProtoService.GetChat(linkedChatChanged.OldLinkedChatId), ref entities);
                }
            }
            else
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogChangedLinkedChannel, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ProtoService.GetChat(linkedChatChanged.NewLinkedChatId), ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogRemovedLinkedChannel, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ProtoService.GetChat(linkedChatChanged.OldLinkedChatId), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateLocationChanged(MessageViewModel message, ChatEventLocationChanged locationChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (locationChanged.NewLocation != null)
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.EventLogChangedLocation, locationChanged.NewLocation.Address), "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogRemovedLocation, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageUnpinned(MessageViewModel message, ChatEventMessageUnpinned messageUnpinned, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Resources.EventLogUnpinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageDeleted(MessageViewModel message, ChatEventMessageDeleted messageDeleted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Resources.EventLogDeletedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageEdited(MessageViewModel message, ChatEventMessageEdited messageEdited, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (messageEdited.NewMessage.Content is MessageText)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogEditedMessages, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogEditedCaption, "un1", fromUser, ref entities);
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
                content = ReplaceWithLink(Strings.Resources.EventLogEditedChannelDescription, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogEditedGroupDescription, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessagePinned(MessageViewModel message, ChatEventMessagePinned messagePinned, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            content = ReplaceWithLink(Strings.Resources.EventLogPinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateUsernameChanged(MessageViewModel message, ChatEventUsernameChanged usernameChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            if (string.IsNullOrEmpty(usernameChanged.NewUsername))
            {
                content = ReplaceWithLink(Strings.Resources.EventLogRemovedGroupLink, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.EventLogChangedGroupLink, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdatePollStopped(MessageViewModel message, ChatEventPollStopped pollStopped, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSenderUser();

            var poll = pollStopped.Message.Content as MessagePoll;
            if (poll.Poll.Type is PollTypeRegular)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogStopPoll, "un1", fromUser, ref entities);
            }
            else if (poll.Poll.Type is PollTypeQuiz)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogStopQuiz, "un1", fromUser, ref entities);
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
                content = Strings.Resources.ActionYouCreateGroup;
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.ActionCreateGroup, "un1", message.GetSenderUser(), ref entities);
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
                    if (chat == null)
                    {
                        return (content, entities);
                    }

                    var supergroup = chat.Type as ChatTypeSupergroup;
                    if (supergroup != null && supergroup.IsChannel)
                    {
                        if (singleUserId == message.ProtoService.Options.MyId)
                        {
                            content = Strings.Resources.ChannelJoined;
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Resources.EventLogChannelJoined, "un1", fromUser, ref entities);
                        }
                    }
                    else
                    {
                        if (supergroup != null && !supergroup.IsChannel)
                        {
                            if (singleUserId == message.ProtoService.Options.MyId)
                            {
                                content = Strings.Resources.ChannelMegaJoined;
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Resources.ActionAddUserSelfMega, "un1", fromUser, ref entities);
                            }
                        }
                        else if (message.IsOutgoing)
                        {
                            content = Strings.Resources.ActionAddUserSelfYou;
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Resources.ActionAddUserSelf, "un1", fromUser, ref entities);
                        }
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionYouAddUser, "un2", whoUser, ref entities);
                    }
                    else if (singleUserId == message.ProtoService.Options.MyId)
                    {
                        var chat = message.GetChat();
                        var supergroup = chat.Type as ChatTypeSupergroup;
                        if (supergroup != null)
                        {
                            if (supergroup.IsChannel)
                            {
                                content = ReplaceWithLink(Strings.Resources.ChannelAddedBy, "un1", fromUser, ref entities);
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Resources.MegaAddedBy, "un1", fromUser, ref entities);
                            }
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Resources.ActionAddUserYou, "un1", fromUser, ref entities);
                        }
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionAddUser, "un1", fromUser, ref entities);
                        content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                    }
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionYouAddUser, "un2", chatAddMembers.MemberUserIds, message.ProtoService, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionAddUser, "un1", message.GetSenderUser(), ref entities);
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
                content = chatChangePhoto.Photo.Animation != null
                    ? Strings.Resources.ActionChannelChangedVideo
                    : Strings.Resources.ActionChannelChangedPhoto;
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = chatChangePhoto.Photo.Animation != null
                        ? Strings.Resources.ActionYouChangedVideo
                        : Strings.Resources.ActionYouChangedPhoto;
                }
                else
                {
                    content = chatChangePhoto.Photo.Animation != null
                        ? ReplaceWithLink(Strings.Resources.ActionChangedVideo, "un1", message.GetSenderUser(), ref entities)
                        : ReplaceWithLink(Strings.Resources.ActionChangedPhoto, "un1", message.GetSenderUser(), ref entities);
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
                content = Strings.Resources.ActionChannelChangedTitle.Replace("un2", chatChangeTitle.Title);
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Resources.ActionYouChangedTitle.Replace("un2", chatChangeTitle.Title);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionChangedTitle.Replace("un2", chatChangeTitle.Title), "un1", message.GetSenderUser(), ref entities);
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
                    content = Strings.Resources.ActionYouLeftUser;
                }
                else
                {
                    if (message.IsChannelPost)
                    {
                        content = ReplaceWithLink(Strings.Resources.EventLogLeftChannel, "un1", fromUser, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionLeftUser, "un1", fromUser, ref entities);
                    }
                }
            }
            else
            {
                var whoUser = message.ProtoService.GetUser(chatDeleteMember.UserId);
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionYouKickUser, "un2", whoUser, ref entities);
                }
                else if (chatDeleteMember.UserId == message.ProtoService.Options.MyId)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionKickUserYou, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionKickUser, "un1", fromUser, ref entities);
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
                content = Strings.Resources.ActionChannelRemovedPhoto;
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Resources.ActionYouRemovedPhoto;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionRemovedPhoto, "un1", message.GetSenderUser(), ref entities);
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
                content = Strings.Resources.ActionInviteYou;
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.ActionInviteUser, "un1", message.GetSenderUser(), ref entities);
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
                    content = string.Format(Strings.Resources.MessageLifetimeChangedOutgoing, Locale.FormatTtl(chatSetTtl.Ttl));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.MessageLifetimeChanged, "un1", Locale.FormatTtl(chatSetTtl.Ttl)), "un1", message.GetSenderUser(), ref entities);
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Resources.MessageLifetimeYouRemoved;
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.MessageLifetimeRemoved, "un1"), "un1", message.GetSenderUser(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeFrom(MessageViewModel message, MessageChatUpgradeFrom chatUpgradeFrom, bool active)
        {
            return (Strings.Resources.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeTo(MessageViewModel message, MessageChatUpgradeTo chatUpgradeTo, bool active)
        {
            return (Strings.Resources.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateContactRegistered(MessageViewModel message, MessageContactRegistered contactRegistered, bool active)
        {
            return (string.Format(Strings.Resources.NotificationContactJoined, message.GetSenderUser().GetFullName()), null);
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
                if (message.SenderUserId == message.ProtoService.Options.MyId)
                {
                    content = string.Format(Strings.Resources.ActionYouScored, Locale.Declension("Points", gameScore.Score));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserScored, Locale.Declension("Points", gameScore.Score)), "un1", message.GetSenderUser(), ref entities);
                }
            }
            else
            {
                if (message.SenderUserId == message.ProtoService.Options.MyId)
                {
                    content = string.Format(Strings.Resources.ActionYouScoredInGame, Locale.Declension("Points", gameScore.Score));
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserScoredInGame, Locale.Declension("Points", gameScore.Score)), "un1", message.GetSenderUser(), ref entities);
                }

                content = ReplaceWithLink(content, "un2", game, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdatePassportDataSent(MessageViewModel message, MessagePassportDataSent passportDataSent, bool active)
        {
            var content = string.Empty;

            StringBuilder str = new StringBuilder();
            for (int a = 0, size = passportDataSent.Types.Count; a < size; a++)
            {
                var type = passportDataSent.Types[a];
                if (str.Length > 0)
                {
                    str.Append(", ");
                }
                if (type is PassportElementTypePhoneNumber)
                {
                    str.Append(Strings.Resources.ActionBotDocumentPhone);
                }
                else if (type is PassportElementTypeEmailAddress)
                {
                    str.Append(Strings.Resources.ActionBotDocumentEmail);
                }
                else if (type is PassportElementTypeAddress)
                {
                    str.Append(Strings.Resources.ActionBotDocumentAddress);
                }
                else if (type is PassportElementTypePersonalDetails)
                {
                    str.Append(Strings.Resources.ActionBotDocumentIdentity);
                }
                else if (type is PassportElementTypePassport)
                {
                    str.Append(Strings.Resources.ActionBotDocumentPassport);
                }
                else if (type is PassportElementTypeDriverLicense)
                {
                    str.Append(Strings.Resources.ActionBotDocumentDriverLicence);
                }
                else if (type is PassportElementTypeIdentityCard)
                {
                    str.Append(Strings.Resources.ActionBotDocumentIdentityCard);
                }
                else if (type is PassportElementTypeUtilityBill)
                {
                    str.Append(Strings.Resources.ActionBotDocumentUtilityBill);
                }
                else if (type is PassportElementTypeBankStatement)
                {
                    str.Append(Strings.Resources.ActionBotDocumentBankStatement);
                }
                else if (type is PassportElementTypeRentalAgreement)
                {
                    str.Append(Strings.Resources.ActionBotDocumentRentalAgreement);
                }
                else if (type is PassportElementTypeInternalPassport)
                {
                    str.Append(Strings.Resources.ActionBotDocumentInternalPassport);
                }
                else if (type is PassportElementTypePassportRegistration)
                {
                    str.Append(Strings.Resources.ActionBotDocumentPassportRegistration);
                }
                else if (type is PassportElementTypeTemporaryRegistration)
                {
                    str.Append(Strings.Resources.ActionBotDocumentTemporaryRegistration);
                }
            }

            var chat = message.ProtoService.GetChat(message.ChatId);
            content = string.Format(Strings.Resources.ActionBotDocuments, chat?.Title ?? string.Empty, str.ToString());

            return (content, null);
        }

        private static (string, IList<TextEntity>) UpdatePaymentSuccessful(MessageViewModel message, MessagePaymentSuccessful paymentSuccessful, bool active)
        {
            var content = string.Empty;

            var invoice = GetInvoice(message);
            var chat = message.GetChat();

            if (invoice != null)
            {
                content = string.Format(Strings.Resources.PaymentSuccessfullyPaid, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ProtoService.GetTitle(chat), invoice.Title);
            }
            else
            {
                content = string.Format(Strings.Resources.PaymentSuccessfullyPaidNoItem, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ProtoService.GetTitle(chat));
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
                content = ReplaceWithLink(Strings.Resources.ActionPinnedNoText, "un1", sender ?? (BaseObject)chat, ref entities);
            }
            else
            {
                if (reply.Content is MessageAudio)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedMusic, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVideo)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedVideo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageAnimation)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedGif, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVoiceNote)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedVoice, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVideoNote)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedRound, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageSticker)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedSticker, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageDocument)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedFile, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageLocation location)
                {
                    content = ReplaceWithLink(location.LivePeriod > 0 ? Strings.Resources.ActionPinnedGeoLive : Strings.Resources.ActionPinnedGeo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageVenue)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedGeo, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageContact)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedContact, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessagePhoto)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedPhoto, "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessagePoll poll)
                {
                    if (poll.Poll.Type is PollTypeRegular)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedPoll, "un1", sender ?? (BaseObject)chat, ref entities);
                    }
                    else if (poll.Poll.Type is PollTypeQuiz)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedQuiz, "un1", sender ?? (BaseObject)chat, ref entities);
                    }
                }
                else if (reply.Content is MessageGame game)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionPinnedGame, "\uD83C\uDFAE " + game.Game.Title), "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else if (reply.Content is MessageText text)
                {
                    var mess = text.Text.Text.Replace('\n', ' ');
                    if (mess.Length > 20)
                    {
                        mess = mess.Substring(0, Math.Min(mess.Length, 20)) + "...";
                    }

                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionPinnedText, mess), "un1", sender ?? (BaseObject)chat, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedNoText, "un1", sender ?? (BaseObject)chat, ref entities);
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
                content = Strings.Resources.ActionTakeScreenshootYou;
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.ActionTakeScreenshoot, "un1", message.GetSenderUser(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateSupergroupChatCreate(MessageViewModel message, MessageSupergroupChatCreate supergroupChatCreate, bool active)
        {
            var content = string.Empty;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                content = Strings.Resources.ActionCreateChannel;
            }
            else
            {
                content = Strings.Resources.ActionCreateMega;
            }

            return (content, null);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateWebsiteConnected(MessageViewModel message, MessageWebsiteConnected websiteConnected, bool active)
        {
            var content = Strings.Resources.ActionBotAllowed;
            var entities = active ? new List<TextEntity>() : null;

            var start = content.IndexOf("{0}");
            content = string.Format(content, websiteConnected.DomainName);

            if (start >= 0 && active)
            {
                entities.Add(new TextEntity(start, websiteConnected.DomainName.Length, new TextEntityTypeUrl()));
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateExpiredPhoto(MessageViewModel message, MessageExpiredPhoto expiredPhoto, bool active)
        {
            return (Strings.Resources.AttachPhotoExpired, null);
        }

        private static (string, IList<TextEntity>) UpdateExpiredVideo(MessageViewModel message, MessageExpiredVideo expiredVideo, bool active)
        {
            return (Strings.Resources.AttachVideoExpired, null);
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
                else if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypePhoneNumber || entity.Type is TextEntityTypeMention || entity.Type is TextEntityTypeHashtag || entity.Type is TextEntityTypeCashtag || entity.Type is TextEntityTypeBotCommand)
                {
                    var data = text.Substring(entity.Offset, entity.Length);

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = new SolidColorBrush(Colors.White);
                    hyperlink.FontWeight = FontWeights.SemiBold;
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
                message.Delegate.OpenUrl("mailto:" + data, false);
            }
            else if (type is TextEntityTypePhoneNumber)
            {
                message.Delegate.OpenUrl("tel:" + data, false);
            }
            else if (type is TextEntityTypeHashtag || type is TextEntityTypeCashtag)
            {
                message.Delegate.OpenHashtag(data);
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
