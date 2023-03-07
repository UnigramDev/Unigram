//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;

namespace Unigram.Controls.Messages
{
    public class MessageService : Button
    {
        private MessageViewModel _message;

        public MessageService()
        {
            DefaultStyleKey = typeof(MessageService);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var content = FindName("Text") as FormattedTextBlock;
            if (content == null)
            {
                return;
            }

            content.TextEntityClick += Message_TextEntityClick;
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (_message is not MessageViewModel message)
            {
                return;
            }

            if (e.Type is TextEntityTypeMention && e.Data is string username)
            {
                message.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true);
            }
            else if (e.Type is TextEntityTypeUrl && e.Data is string url)
            {
                message.Delegate.OpenUrl(url, false);
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;
            Tag = message;

            var content = FindName("Text") as FormattedTextBlock;
            if (content == null)
            {
                return;
            }

            var entities = GetEntities(message, true);
            if (entities.Text != null)
            {
                content.SetText(message.ClientService, entities.Text, entities.Entities);
                AutomationProperties.SetName(this, entities.Text);
            }
        }

        public static string GetText(MessageViewModel message)
        {
            return GetEntities(message, false).Text;
        }

        private static (string Text, IList<TextEntity> Entities) GetEntities(MessageViewModel message, bool active)
        {
            return message.Content switch
            {
                MessageBasicGroupChatCreate basicGroupChatCreate => UpdateBasicGroupChatCreate(message, basicGroupChatCreate, active),
                MessageChatAddMembers chatAddMembers => UpdateChatAddMembers(message, chatAddMembers, active),
                MessageChatChangePhoto chatChangePhoto => UpdateChatChangePhoto(message, chatChangePhoto, active),
                MessageChatChangeTitle chatChangeTitle => UpdateChatChangeTitle(message, chatChangeTitle, active),
                MessageChatSetTheme chatSetTheme => UpdateChatSetTheme(message, chatSetTheme, active),
                MessageChatDeleteMember chatDeleteMember => UpdateChatDeleteMember(message, chatDeleteMember, active),
                MessageChatDeletePhoto chatDeletePhoto => UpdateChatDeletePhoto(message, chatDeletePhoto, active),
                MessageChatJoinByLink chatJoinByLink => UpdateChatJoinByLink(message, chatJoinByLink, active),
                MessageChatJoinByRequest chatJoinByRequest => UpdateChatJoinByRequest(message, chatJoinByRequest, active),
                MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime => UpdateChatSetMessageAutoDeleteTime(message, chatSetMessageAutoDeleteTime, active),
                MessageChatUpgradeFrom chatUpgradeFrom => UpdateChatUpgradeFrom(message, chatUpgradeFrom, active),
                MessageChatUpgradeTo chatUpgradeTo => UpdateChatUpgradeTo(message, chatUpgradeTo, active),
                MessageContactRegistered contactRegistered => UpdateContactRegistered(message, contactRegistered, active),
                MessageCustomServiceAction customServiceAction => UpdateCustomServiceAction(message, customServiceAction, active),
                MessageForumTopicCreated forumTopicCreated => UpdateForumTopicCreated(message, forumTopicCreated, active),
                MessageForumTopicEdited forumTopicEdited => UpdateForumTopicEdited(message, forumTopicEdited, active),
                MessageForumTopicIsClosedToggled forumTopicIsClosedToggled => UpdateForumTopicIsClosedToggled(message, forumTopicIsClosedToggled, active),
                MessageGameScore gameScore => UpdateGameScore(message, gameScore, active),
                MessageGiftedPremium giftedPremium => UpdateGiftedPremium(message, giftedPremium, active),
                MessageInviteVideoChatParticipants inviteVideoChatParticipants => UpdateInviteVideoChatParticipants(message, inviteVideoChatParticipants, active),
                MessageProximityAlertTriggered proximityAlertTriggered => UpdateProximityAlertTriggered(message, proximityAlertTriggered, active),
                MessagePassportDataSent passportDataSent => UpdatePassportDataSent(message, passportDataSent, active),
                MessagePaymentSuccessful paymentSuccessful => UpdatePaymentSuccessful(message, paymentSuccessful, active),
                MessagePinMessage pinMessage => UpdatePinMessage(message, pinMessage, active),
                MessageScreenshotTaken screenshotTaken => UpdateScreenshotTaken(message, screenshotTaken, active),
                MessageSuggestProfilePhoto suggestProfilePhoto => UpdateSuggestProfilePhoto(message, suggestProfilePhoto, active),
                MessageSupergroupChatCreate supergroupChatCreate => UpdateSupergroupChatCreate(message, supergroupChatCreate, active),
                MessageVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, active),
                MessageVideoChatScheduled videoChatScheduled => UpdateVideoChatScheduled(message, videoChatScheduled, active),
                MessageVideoChatStarted videoChatStarted => UpdateVideoChatStarted(message, videoChatStarted, active),
                MessageWebsiteConnected websiteConnected => UpdateWebsiteConnected(message, websiteConnected, active),
                MessageWebAppDataSent webAppDataSent => UpdateWebAppDataSent(message, webAppDataSent, active),
                MessageExpiredPhoto expiredPhoto => UpdateExpiredPhoto(message, expiredPhoto, active),
                MessageExpiredVideo expiredVideo => UpdateExpiredVideo(message, expiredVideo, active),
                // Local types:
                MessageChatEvent chatEvent => chatEvent.Action switch
                {
                    ChatEventAvailableReactionsChanged availableReactionsChanged => UpdateAvailableReactionsChanged(message, availableReactionsChanged, active),
                    ChatEventHasProtectedContentToggled hasProtectedContentToggled => UpdateHasProtectedContentToggled(message, hasProtectedContentToggled, active),
                    ChatEventSignMessagesToggled signMessagesToggled => UpdateSignMessagesToggled(message, signMessagesToggled, active),
                    ChatEventStickerSetChanged stickerSetChanged => UpdateStickerSetChanged(message, stickerSetChanged, active),
                    ChatEventInvitesToggled invitesToggled => UpdateInvitesToggled(message, invitesToggled, active),
                    ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled => UpdateIsAllHistoryAvailableToggled(message, isAllHistoryAvailableToggled, active),
                    ChatEventLinkedChatChanged linkedChatChanged => UpdateLinkedChatChanged(message, linkedChatChanged, active),
                    ChatEventLocationChanged locationChanged => UpdateLocationChanged(message, locationChanged, active),
                    ChatEventMessageUnpinned messageUnpinned => UpdateMessageUnpinned(message, messageUnpinned, active),
                    ChatEventMessageDeleted messageDeleted => UpdateMessageDeleted(message, messageDeleted, active),
                    ChatEventMessageEdited messageEdited => UpdateMessageEdited(message, messageEdited, active),
                    ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged => UpdateMessageAutoDeleteTimeChanged(message, messageAutoDeleteTimeChanged, active),
                    ChatEventDescriptionChanged descriptionChanged => UpdateDescriptionChanged(message, descriptionChanged, active),
                    ChatEventInviteLinkDeleted inviteLinkDeleted => UpdateInviteLinkDeleted(message, inviteLinkDeleted, active),
                    ChatEventInviteLinkEdited inviteLinkEdited => UpdateInviteLinkEdited(message, inviteLinkEdited, active),
                    ChatEventInviteLinkRevoked inviteLinkRevoked => UpdateInviteLinkRevoked(message, inviteLinkRevoked, active),
                    ChatEventMessagePinned messagePinned => UpdateMessagePinned(message, messagePinned, active),
                    ChatEventUsernameChanged usernameChanged => UpdateUsernameChanged(message, usernameChanged, active),
                    ChatEventPollStopped pollStopped => UpdatePollStopped(message, pollStopped, active),
                    ChatEventSlowModeDelayChanged slowModeDelayChanged => UpdateSlowModeDelayChanged(message, slowModeDelayChanged, active),
                    ChatEventVideoChatCreated videoChatCreated => UpdateVideoChatCreated(message, videoChatCreated, active),
                    ChatEventVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, active),
                    ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled => UpdateVideoChatMuteNewParticipantsToggled(message, videoChatMuteNewParticipantsToggled, active),
                    ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled => UpdateVideoChatParticipantIsMutedToggled(message, videoChatParticipantIsMutedToggled, active),
                    ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged => UpdateVideoChatParticipantVolumeLevelChanged(message, videoChatParticipantVolumeLevelChanged, active),
                    ChatEventIsForumToggled isForumToggled => UpdateChatEventIsForumToggled(message, isForumToggled, active),
                    ChatEventForumTopicCreated forumTopicCreated => UpdateChatEventForumTopicCreated(message, forumTopicCreated, active),
                    ChatEventForumTopicDeleted forumTopicDeleted => UpdateChatEventForumTopicDeleted(message, forumTopicDeleted, active),
                    ChatEventForumTopicEdited forumTopicEdited => UpdateChatEventForumTopicEdited(message, forumTopicEdited, active),
                    ChatEventForumTopicPinned forumTopicPinned => UpdateChatEventForumTopicPinned(message, forumTopicPinned, active),
                    ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed => UpdateChatEventForumTopicToggleIsClosed(message, forumTopicToggleIsClosed, active),
                    //ChatEventActiveUsernamesChanged activeUsernamesChanged => UpdateChatEventActiveUsernames(message, activeUsernamesChanged, active),
                    _ => (string.Empty, null)
                },
                MessageHeaderDate headerDate => UpdateHeaderDate(message, headerDate, active),
                _ => (string.Empty, null)
            };
        }

        #region Local

        private static (string Text, IList<TextEntity> Entities) UpdateHeaderDate(MessageViewModel message, MessageHeaderDate headerDate, bool active)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                return (string.Format(Strings.Resources.MessageScheduledOn, Converter.DayGrouping(Utils.UnixTimestampToDateTime(sendAtDate.SendDate))), null);
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                return (Strings.Resources.MessageScheduledUntilOnline, null);
            }

            return (Converter.DayGrouping(Utils.UnixTimestampToDateTime(message.Date)), null);
        }

        #endregion

        #region Event log

        private static (string Text, IList<TextEntity> Entities) UpdateSlowModeDelayChanged(MessageViewModel message, ChatEventSlowModeDelayChanged slowModeDelayChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

        private static (string Text, IList<TextEntity> Entities) UpdateAvailableReactionsChanged(MessageViewModel message, ChatEventAvailableReactionsChanged availableReactionsChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (availableReactionsChanged.NewAvailableReactions is ChatAvailableReactionsAll
                || (availableReactionsChanged.NewAvailableReactions is ChatAvailableReactionsSome some && some.Reactions.Count > 0))
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionReactionsChanged,
                    string.Join(", ", availableReactionsChanged.OldAvailableReactions),
                    string.Join(", ", availableReactionsChanged.NewAvailableReactions)), "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionReactionsChanged,
                    string.Join(", ", availableReactionsChanged.OldAvailableReactions),
                    string.Join(", ", availableReactionsChanged.NewAvailableReactions)), "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateHasProtectedContentToggled(MessageViewModel message, ChatEventHasProtectedContentToggled hasProtectedContentToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (hasProtectedContentToggled.HasProtectedContent)
            {
                content = ReplaceWithLink(message.IsChannelPost
                    ? Strings.Resources.ActionForwardsRestrictedChannel
                    : Strings.Resources.ActionForwardsRestrictedGroup, "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(message.IsChannelPost
                    ? Strings.Resources.ActionForwardsEnabledChannel
                    : Strings.Resources.ActionForwardsEnabledGroup, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateSignMessagesToggled(MessageViewModel message, ChatEventSignMessagesToggled signMessagesToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

            if (message.IsChannelPost)
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogChangedLinkedGroup, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId), ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogRemovedLinkedGroup, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId), ref entities);
                }
            }
            else
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogChangedLinkedChannel, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId), ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogRemovedLinkedChannel, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateLocationChanged(MessageViewModel message, ChatEventLocationChanged locationChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogUnpinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageDeleted(MessageViewModel message, ChatEventMessageDeleted messageDeleted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogDeletedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessageEdited(MessageViewModel message, ChatEventMessageEdited messageEdited, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

        private static (string Text, IList<TextEntity> Entities) UpdateMessageAutoDeleteTimeChanged(MessageViewModel message, ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime > 0)
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionTTLChanged, Locale.FormatTtl(messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime)), "un1", fromUser, ref entities);
            }
            else
            {
                content = ReplaceWithLink(Strings.Resources.ActionTTLDisabled, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateDescriptionChanged(MessageViewModel message, ChatEventDescriptionChanged descriptionChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

        private static (string Text, IList<TextEntity> Entities) UpdateInviteLinkDeleted(MessageViewModel message, ChatEventInviteLinkDeleted inviteLinkDeleted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();
            content = ReplaceWithLink(string.Format(Strings.Resources.ActionDeletedInviteLink, inviteLinkDeleted.InviteLink.InviteLink), "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateInviteLinkEdited(MessageViewModel message, ChatEventInviteLinkEdited inviteLinkEdited, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            //if (inviteLinkEdited.)
            //{
            //}
            //else
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionEditedInviteLinkToSame, inviteLinkEdited.NewInviteLink.InviteLink), "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateInviteLinkRevoked(MessageViewModel message, ChatEventInviteLinkRevoked inviteLinkRevoked, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();
            content = ReplaceWithLink(string.Format(Strings.Resources.ActionRevokedInviteLink, inviteLinkRevoked.InviteLink.InviteLink), "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateMessagePinned(MessageViewModel message, ChatEventMessagePinned messagePinned, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogPinnedMessages, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string Text, IList<TextEntity> Entities) UpdateUsernameChanged(MessageViewModel message, ChatEventUsernameChanged usernameChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

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

            var fromUser = message.GetSender();

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

        private static (string, IList<TextEntity>) UpdateVideoChatCreated(MessageViewModel message, ChatEventVideoChatCreated videoChatCreated, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.ClientService.TryGetUser(message.SenderId, out User fromUser))
            {
                if (message.IsChannelPost)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogStartedLiveStream, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogStartedVoiceChat, "un1", fromUser, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatEnded(MessageViewModel message, ChatEventVideoChatEnded videoChatEnded, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.ClientService.TryGetUser(message.SenderId, out User fromUser))
            {
                if (message.IsChannelPost)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogEndedLiveStream, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogEndedVoiceChat, "un1", fromUser, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatMuteNewParticipantsToggled(MessageViewModel message, ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.ClientService.TryGetUser(message.SenderId, out User fromUser))
            {
                if (videoChatMuteNewParticipantsToggled.MuteNewParticipants)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogVoiceChatNotAllowedToSpeak, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogVoiceChatAllowedToSpeak, "un1", fromUser, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatParticipantIsMutedToggled(MessageViewModel message, ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantIsMutedToggled.ParticipantId);

            if (message.ClientService.TryGetUser(message.SenderId, out User fromUser))
            {
                if (videoChatParticipantIsMutedToggled.IsMuted)
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogVoiceChatMuted, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.EventLogVoiceChatUnmuted, "un1", fromUser, ref entities);
                }

                content = ReplaceWithLink(content, "un2", whoUser, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatParticipantVolumeLevelChanged(MessageViewModel message, ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantVolumeLevelChanged.ParticipantId);

            if (message.ClientService.TryGetUser(message.SenderId, out User fromUser))
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionVolumeChanged, videoChatParticipantVolumeLevelChanged.VolumeLevel), "un1", fromUser, ref entities);
                content = ReplaceWithLink(content, "un2", whoUser, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventIsForumToggled(MessageViewModel message, ChatEventIsForumToggled isForumToggled, bool active)
        {
            // EventLogSwitchToForum, EventLogSwitchToGroup

            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(isForumToggled.IsForum
                ? Strings.Resources.EventLogSwitchToForum
                : Strings.Resources.EventLogSwitchToGroup, "un1", fromUser, ref entities);

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventForumTopicCreated(MessageViewModel message, ChatEventForumTopicCreated forumTopicCreated, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogCreateTopic, "un1", fromUser, ref entities);
            content = ReplaceWithLink(content, "un2", forumTopicCreated.TopicInfo, ref entities);

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventForumTopicDeleted(MessageViewModel message, ChatEventForumTopicDeleted forumTopicDeleted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogDeleteTopic, "un1", fromUser, ref entities);
            content = ReplaceWithLink(content, "un2", forumTopicDeleted.TopicInfo, ref entities);

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventForumTopicEdited(MessageViewModel message, ChatEventForumTopicEdited forumTopicEdited, bool active)
        {
            // EventLogEditTopic
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            content = ReplaceWithLink(Strings.Resources.EventLogEditTopic, "un1", fromUser, ref entities);
            content = ReplaceWithLink(content, "un2", forumTopicEdited.OldTopicInfo, ref entities);
            content = ReplaceWithLink(content, "un3", forumTopicEdited.NewTopicInfo, ref entities);

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventForumTopicPinned(MessageViewModel message, ChatEventForumTopicPinned forumTopicPinned, bool active)
        {
            // EventLogPinTopic, EventLogUnpinTopic
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (forumTopicPinned.NewTopicInfo != null)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogPinTopic, "un1", fromUser, ref entities);
                content = ReplaceWithLink(content, "un2", forumTopicPinned.NewTopicInfo, ref entities);
            }
            else if (forumTopicPinned.OldTopicInfo != null)
            {
                content = ReplaceWithLink(Strings.Resources.EventLogUnpinTopic, "un1", fromUser, ref entities);
                content = ReplaceWithLink(content, "un2", forumTopicPinned.OldTopicInfo, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatEventForumTopicToggleIsClosed(MessageViewModel message, ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            return (content, entities);
        }

        //private static (string, IList<TextEntity>) UpdateChatEventActiveUsernames(MessageViewModel message, ChatEventActiveUsernamesChanged activeUsernamesChanged, bool active)
        //{
        //    //var content = string.Empty;
        //    //var entities = active ? new List<TextEntity>() : null;

        //    //var fromUser = message.GetSender();

        //    //content = ReplaceWithLink(isForumToggled.IsForum
        //    //    ? Strings.Resources.EventLogSwitchToForum
        //    //    : Strings.Resources.EventLogSwitchToGroup, "un1", fromUser, ref entities);

        //    //return (content, entities);
        //}

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
                content = ReplaceWithLink(Strings.Resources.ActionCreateGroup, "un1", message.GetSender(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatAddMembers(MessageViewModel message, MessageChatAddMembers chatAddMembers, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            long singleUserId = 0;
            if (singleUserId == 0 && chatAddMembers.MemberUserIds.Count == 1)
            {
                singleUserId = chatAddMembers.MemberUserIds[0];
            }

            var fromUser = message.GetSender();

            if (singleUserId != 0)
            {
                var whoUser = message.ClientService.GetUser(singleUserId);
                if (message.SenderId is MessageSenderUser senderUser && singleUserId == senderUser.UserId)
                {
                    var chat = message.GetChat();
                    if (chat == null)
                    {
                        return (content, entities);
                    }

                    var supergroup = chat.Type as ChatTypeSupergroup;
                    if (supergroup != null && supergroup.IsChannel)
                    {
                        if (singleUserId == message.ClientService.Options.MyId)
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
                            if (singleUserId == message.ClientService.Options.MyId)
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
                    else if (singleUserId == message.ClientService.Options.MyId)
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
                    content = ReplaceWithLink(Strings.Resources.ActionYouAddUser, "un2", chatAddMembers.MemberUserIds, message.ClientService, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionAddUser, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", chatAddMembers.MemberUserIds, message.ClientService, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatChangePhoto(MessageViewModel message, MessageChatChangePhoto chatChangePhoto, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsChannelPost)
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
                        ? ReplaceWithLink(Strings.Resources.ActionChangedVideo, "un1", message.GetSender(), ref entities)
                        : ReplaceWithLink(Strings.Resources.ActionChangedPhoto, "un1", message.GetSender(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatChangeTitle(MessageViewModel message, MessageChatChangeTitle chatChangeTitle, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsChannelPost)
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
                    content = ReplaceWithLink(Strings.Resources.ActionChangedTitle.Replace("un2", chatChangeTitle.Title), "un1", message.GetSender(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatSetTheme(MessageViewModel message, MessageChatSetTheme chatSetTheme, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                if (string.IsNullOrEmpty(chatSetTheme.ThemeName))
                {
                    content = Strings.Resources.ChatThemeDisabledYou;
                }
                else
                {
                    content = string.Format(Strings.Resources.ChatThemeChangedYou, chatSetTheme.ThemeName);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(chatSetTheme.ThemeName))
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ChatThemeDisabled, "un1"), "un1", message.GetSender(), ref entities);
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ChatThemeChangedTo, "un1", chatSetTheme.ThemeName), "un1", message.GetSender(), ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatDeleteMember(MessageViewModel message, MessageChatDeleteMember chatDeleteMember, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (message.SenderId is MessageSenderUser senderUser && chatDeleteMember.UserId == senderUser.UserId)
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
                var whoUser = message.ClientService.GetUser(chatDeleteMember.UserId);
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionYouKickUser, "un2", whoUser, ref entities);
                }
                else if (chatDeleteMember.UserId == message.ClientService.Options.MyId)
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

            if (message.IsChannelPost)
            {
                content = Strings.Resources.ActionChannelRemovedPhoto;
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = Strings.Resources.ActionYouRemovedPhoto;
                }
                else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    content = ReplaceWithLink(Strings.Resources.ActionRemovedPhoto, "un1", senderUser, ref entities);
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
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                content = ReplaceWithLink(Strings.Resources.ActionInviteUser, "un1", senderUser, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatJoinByRequest(MessageViewModel message, MessageChatJoinByRequest chatJoinByRequest, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            //if (message.IsOutgoing)
            //{
            //    content = Strings.Resources.ActionInviteYou;
            //}
            //else
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                content = ReplaceWithLink(Strings.Resources.UserAcceptedToGroupAction, "un1", senderUser, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatSetMessageAutoDeleteTime(MessageViewModel message, MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var chat = message.GetChat();
            if (chat.Type is ChatTypeSecret)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (message.IsOutgoing)
                    {
                        content = string.Format(Strings.Resources.MessageLifetimeChangedOutgoing, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Resources.MessageLifetimeChanged, "un1", Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), "un1", message.GetSender(), ref entities);
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
                        content = ReplaceWithLink(string.Format(Strings.Resources.MessageLifetimeRemoved, "un1"), "un1", message.GetSender(), ref entities);
                    }
                }
            }
            else if (message.IsChannelPost)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    content = string.Format(Strings.Resources.ActionTTLChannelChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime));
                }
                else
                {
                    content = Strings.Resources.ActionTTLChannelDisabled;
                }
            }
            else
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (chatSetMessageAutoDeleteTime.FromUserId == message.ClientService.Options.MyId)
                    {
                        content = string.Format(Strings.Resources.AutoDeleteGlobalActionFromYou, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime));
                    }
                    else if (chatSetMessageAutoDeleteTime.FromUserId != 0)
                    {
                        content = ReplaceWithLink(string.Format(Strings.Resources.AutoDeleteGlobalAction, "un1", Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), "un1", message.GetSender(), ref entities);
                    }
                    else if (message.IsOutgoing)
                    {
                        content = string.Format(Strings.Resources.ActionTTLYouChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Resources.ActionTTLChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), "un1", message.GetSender(), ref entities);
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        content = Strings.Resources.ActionTTLYouDisabled;
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionTTLDisabled, "un1", message.GetSender(), ref entities);
                    }
                }
            }


            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeFrom(MessageViewModel message, MessageChatUpgradeFrom chatUpgradeFrom, bool active)
        {
            return (string.Empty, null);
            return (Strings.Resources.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateChatUpgradeTo(MessageViewModel message, MessageChatUpgradeTo chatUpgradeTo, bool active)
        {
            return (string.Empty, null);
            return (Strings.Resources.ActionMigrateFromGroup, null);
        }

        private static (string, IList<TextEntity>) UpdateContactRegistered(MessageViewModel message, MessageContactRegistered contactRegistered, bool active)
        {
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return (string.Format(Strings.Resources.NotificationContactJoined, senderUser.FullName()), null);
            }

            return (string.Empty, null);
        }

        private static (string, IList<TextEntity>) UpdateCustomServiceAction(MessageViewModel message, MessageCustomServiceAction customServiceAction, bool active)
        {
            return (customServiceAction.Text, null);
        }

        private static (string, IList<TextEntity>) UpdateForumTopicCreated(MessageViewModel message, MessageForumTopicCreated forumTopicCreated, bool active)
        {
            // TopicWasCreatedAction
            // TopicCreated
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (true)
            {
                content = string.Format(Strings.Resources.TopicWasCreatedAction, $"\U0001F4C3 {forumTopicCreated.Name}");
                entities?.Add(new TextEntity(0, 2, new TextEntityTypeCustomEmoji(forumTopicCreated.Icon.CustomEmojiId)));
            }
            else
            {
                content = Strings.Resources.TopicCreated;
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateForumTopicEdited(MessageViewModel message, MessageForumTopicEdited forumTopicEdited, bool active)
        {
            // TopicWasIconChangedToAction, TopicWasRenamedToAction TopicWasRenamedToAction2
            // TopicIconChangedToAction, TopicRenamedToAction
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (true)
            {
                if (forumTopicEdited.EditIconCustomEmojiId && forumTopicEdited.Name.Length > 0)
                {
                    content = string.Format(Strings.Resources.TopicWasRenamedToAction2, "un1", $"\U0001F4C3 {forumTopicEdited.Name}");
                    content = ReplaceWithLink(content, "un1", fromUser, ref entities);
                }
                else if (forumTopicEdited.EditIconCustomEmojiId)
                {
                    content = string.Format(Strings.Resources.TopicWasIconChangedToAction, "un1", "\U0001F4C3");
                    content = ReplaceWithLink(content, "un1", fromUser, ref entities);
                }
                else
                {
                    content = string.Format(Strings.Resources.TopicWasRenamedToAction, "un1", forumTopicEdited.Name);
                    content = ReplaceWithLink(content, "un1", fromUser, ref entities);
                }
            }

            var index = content.IndexOf("\U0001F4C3");
            if (index != -1 && entities != null)
            {
                entities.Add(new TextEntity(index, 2, new TextEntityTypeCustomEmoji(forumTopicEdited.IconCustomEmojiId)));
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateForumTopicIsClosedToggled(MessageViewModel message, MessageForumTopicIsClosedToggled forumTopicIsClosedToggled, bool active)
        {
            // TopicWasClosedAction, TopicWasReopenedAction
            // TopicClosed2, TopicRestarted2

            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var fromUser = message.GetSender();

            if (true)
            {
                content = string.Format(forumTopicIsClosedToggled.IsClosed
                    ? Strings.Resources.TopicClosed2
                    : Strings.Resources.TopicRestarted2, "un1");
                content = ReplaceWithLink(content, "un1", fromUser, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateGameScore(MessageViewModel message, MessageGameScore gameScore, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var game = GetGame(message);
            if (game == null)
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        content = string.Format(Strings.Resources.ActionYouScored, Locale.Declension("Points", gameScore.Score));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserScored, Locale.Declension("Points", gameScore.Score)), "un1", senderUser, ref entities);
                    }
                }
            }
            else
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        content = string.Format(Strings.Resources.ActionYouScoredInGame, Locale.Declension("Points", gameScore.Score));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserScoredInGame, Locale.Declension("Points", gameScore.Score)), "un1", senderUser, ref entities);
                    }
                }

                content = ReplaceWithLink(content, "un2", game, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateGiftedPremium(MessageViewModel message, MessageGiftedPremium giftedPremium, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.SenderId is MessageSenderUser user && user.UserId == message.ClientService.Options.MyId)
            {
                content = ReplaceWithLink(Strings.Resources.ActionGiftOutbound, "un2", giftedPremium, ref entities);
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                content = ReplaceWithLink(Strings.Resources.ActionGiftInbound, "un1", senderUser, ref entities);
                content = ReplaceWithLink(content, "un2", giftedPremium, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatEnded(MessageViewModel message, MessageVideoChatEnded videoChatEnded, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                content = string.Format(Strings.Resources.ActionGroupCallEndedByYou, videoChatEnded.GetDuration());
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                content = ReplaceWithLink(string.Format(Strings.Resources.ActionGroupCallEndedBy, videoChatEnded.GetDuration()), "un1", senderUser, ref entities);
            }
            else
            {
                content = string.Format(Strings.Resources.ActionGroupCallEnded, videoChatEnded.GetDuration());
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatScheduled(MessageViewModel message, MessageVideoChatScheduled videoChatScheduled, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsChannelPost)
            {
                content = string.Format(Strings.Resources.ActionChannelCallScheduled, videoChatScheduled.GetStartsAt());
            }
            else
            {
                content = string.Format(Strings.Resources.ActionGroupCallScheduled, videoChatScheduled.GetStartsAt());
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateVideoChatStarted(MessageViewModel message, MessageVideoChatStarted videoChatStarted, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsChannelPost)
            {
                content = Strings.Resources.ActionChannelCallJustStarted;
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                if (senderUser.Id == message.ClientService.Options.MyId)
                {
                    content = Strings.Resources.ActionGroupCallStartedByYou;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallStarted, "un1", senderUser, ref entities);
                }
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                content = ReplaceWithLink(Strings.Resources.ActionGroupCallStarted, "un1", senderChat, ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateInviteVideoChatParticipants(MessageViewModel message, MessageInviteVideoChatParticipants inviteVideoChatParticipants, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            long singleUserId = 0;
            if (singleUserId == 0 && inviteVideoChatParticipants.UserIds.Count == 1)
            {
                singleUserId = inviteVideoChatParticipants.UserIds[0];
            }

            var fromUser = message.GetSender();

            if (singleUserId != 0)
            {
                var whoUser = message.ClientService.GetUser(singleUserId);
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallYouInvited, "un2", whoUser, ref entities);
                }
                else if (singleUserId == message.ClientService.Options.MyId)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallInvitedYou, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallInvited, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallYouInvited, "un2", inviteVideoChatParticipants.UserIds, message.ClientService, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionGroupCallInvited, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", inviteVideoChatParticipants.UserIds, message.ClientService, ref entities);
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateProximityAlertTriggered(MessageViewModel message, MessageProximityAlertTriggered proximityAlertTriggered, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            User traveler;
            User watcher;

            message.ClientService.TryGetUser(proximityAlertTriggered.TravelerId, out traveler);
            message.ClientService.TryGetUser(proximityAlertTriggered.WatcherId, out watcher);

            if (traveler != null && watcher != null)
            {
                if (traveler.Id == message.ClientService.Options.MyId)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserWithinYouRadius, Converter.Distance(proximityAlertTriggered.Distance, false)), "un1", watcher, ref entities);
                }
                else if (watcher.Id == message.ClientService.Options.MyId)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserWithinRadius, Converter.Distance(proximityAlertTriggered.Distance, false)), "un1", traveler, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionUserWithinOtherRadius, Converter.Distance(proximityAlertTriggered.Distance, false)), "un1", traveler, ref entities);
                    content = ReplaceWithLink(content, "un2", watcher, ref entities);
                }
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

            var chat = message.ClientService.GetChat(message.ChatId);
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
                content = string.Format(Strings.Resources.PaymentSuccessfullyPaid, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat), invoice.Title);
            }
            else
            {
                content = string.Format(Strings.Resources.PaymentSuccessfullyPaidNoItem, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat));
            }

            return (content, null);
        }

        private static (string, IList<TextEntity>) UpdatePinMessage(MessageViewModel message, MessagePinMessage pinMessage, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            var sender = message.GetSender();

            var reply = message.ReplyToMessage;
            if (reply == null)
            {
                content = ReplaceWithLink(Strings.Resources.ActionPinnedNoText, "un1", sender, ref entities);
            }
            else
            {
                if (reply.Content is MessageAnimatedEmoji animatedEmoji)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionPinnedText, animatedEmoji.Emoji), "un1", sender, ref entities);
                }
                else if (reply.Content is MessageAudio)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedMusic, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageVideo)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedVideo, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageAnimation)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedGif, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageVoiceNote)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedVoice, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageVideoNote)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedRound, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageSticker)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedSticker, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageDocument)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedFile, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageLocation location)
                {
                    if (location.LivePeriod > 0)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedGeoLive, "un1", sender, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedGeo, "un1", sender, ref entities);
                    }
                }
                else if (reply.Content is MessageVenue)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedGeo, "un1", sender, ref entities);
                }
                else if (reply.Content is MessageContact)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedContact, "un1", sender, ref entities);
                }
                else if (reply.Content is MessagePhoto)
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedPhoto, "un1", sender, ref entities);
                }
                else if (reply.Content is MessagePoll poll)
                {
                    if (poll.Poll.Type is PollTypeRegular)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedPoll, "un1", sender, ref entities);
                    }
                    else if (poll.Poll.Type is PollTypeQuiz)
                    {
                        content = ReplaceWithLink(Strings.Resources.ActionPinnedQuiz, "un1", sender, ref entities);
                    }
                }
                else if (reply.Content is MessageGame game)
                {
                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionPinnedGame, "\uD83C\uDFAE " + game.Game.Title), "un1", sender, ref entities);
                }
                else if (reply.Content is MessageText text)
                {
                    var mess = text.Text.Text.Replace('\n', ' ');
                    if (mess.Length > 20)
                    {
                        mess = mess.Substring(0, Math.Min(mess.Length, 20)) + "...";
                    }

                    content = ReplaceWithLink(string.Format(Strings.Resources.ActionPinnedText, mess), "un1", sender, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Resources.ActionPinnedNoText, "un1", sender, ref entities);
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
                content = ReplaceWithLink(Strings.Resources.ActionTakeScreenshoot, "un1", message.GetSender(), ref entities);
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateSuggestProfilePhoto(MessageViewModel message, MessageSuggestProfilePhoto suggestProfilePhoto, bool active)
        {
            var content = string.Empty;
            var entities = active ? new List<TextEntity>() : null;

            if (message.IsOutgoing)
            {
                if (message.ClientService.TryGetUser(message.ChatId, out User user))
                {
                    content = string.Format(Strings.Resources.ActionSuggestPhotoFromYouDescription, user.FirstName);
                    entities?.Add(new TextEntity(Strings.Resources.ActionSuggestPhotoFromYouDescription.IndexOf("{0}"), user.FirstName.Length, new TextEntityTypeBold()));
                }
            }
            else
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    content = string.Format(Strings.Resources.ActionSuggestPhotoToYouDescription, user.FirstName);
                    entities?.Add(new TextEntity(Strings.Resources.ActionSuggestPhotoToYouDescription.IndexOf("{0}"), user.FirstName.Length, new TextEntityTypeBold()));
                }
            }

            return (content, entities);
        }

        private static (string, IList<TextEntity>) UpdateSupergroupChatCreate(MessageViewModel message, MessageSupergroupChatCreate supergroupChatCreate, bool active)
        {
            var content = string.Empty;

            if (message.IsChannelPost)
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

        private static (string Text, IList<TextEntity> Entities) UpdateWebAppDataSent(MessageViewModel message, MessageWebAppDataSent webAppDataSent, bool active)
        {
            var content = string.Format(Strings.Resources.ActionBotWebViewData, webAppDataSent.ButtonText);
            var entities = active ? new List<TextEntity>() : null;

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
                    name = user.FullName();
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
                else if (obj is MessageGiftedPremium giftedPremium)
                {
                    name = Locale.FormatCurrency(giftedPremium.Amount, giftedPremium.Currency);
                    id = "tg-premium://";
                }
                else if (obj is ForumTopicInfo forumTopicInfo)
                {
                    name = $"\U0001F4C3 {forumTopicInfo.Name}";
                    id = "tg-topic://";
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
                    else if (obj is ForumTopicInfo forumTopicInfo)
                    {
                        entities.Add(new TextEntity(start, 2, new TextEntityTypeCustomEmoji(forumTopicInfo.Icon.CustomEmojiId)));
                        entities.Add(new TextEntity(start + 3, name.Length - 3, new TextEntityTypeTextUrl(id)));
                    }
                    else
                    {
                        entities.Add(new TextEntity(start, name.Length, new TextEntityTypeTextUrl(id)));
                    }
                }
            }

            return source;
        }

        private static string ReplaceWithLink(string source, string param, IList<long> uids, IClientService clientService, ref List<TextEntity> entities)
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
                        user = clientService.GetUser(uids[a]);
                    }
                    if (user != null)
                    {
                        var name = user.FullName();
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
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = Windows.UI.Text.FontStyle.Italic });
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
