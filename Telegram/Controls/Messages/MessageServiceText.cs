//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages
{
    /// <summary>
    /// Renders a service message (a chat event, a gift, a pinned message, ...) as text.
    /// Kept apart from <see cref="MessageService"/> because the chat list, the reply
    /// preview and the automation peers all need the text without the control.
    /// </summary>
    public static class MessageServiceText
    {
        public static string GetText(MessageWithOwner message)
        {
            return GetEntities(message, false).Text;
        }

        public static FormattedText GetEntities(MessageWithOwner message, bool history)
        {
            return message.Content switch
            {
                MessageBasicGroupChatCreate basicGroupChatCreate => UpdateBasicGroupChatCreate(message, basicGroupChatCreate, history),
                MessageBotWriteAccessAllowed botWriteAccessAllowed => UpdateBotWriteAccessAllowed(message, botWriteAccessAllowed, history),
                MessageChatAddMembers chatAddMembers => UpdateChatAddMembers(message, chatAddMembers, history),
                MessageChatAddedToCommunity chatAddedToCommunity => UpdateChatAddedToCommunity(message, chatAddedToCommunity, history),
                MessageChatRemovedFromCommunity chatRemovedFromCommunity => UpdateChatRemovedFromCommunity(message, chatRemovedFromCommunity, history),
                MessageChatJoinFromCommunity chatJoinFromCommunity => UpdateChatJoinFromCommunity(message, chatJoinFromCommunity, history),
                MessageChatChangePhoto chatChangePhoto => UpdateChatChangePhoto(message, chatChangePhoto, history),
                MessageChatChangeTitle chatChangeTitle => UpdateChatChangeTitle(message, chatChangeTitle, history),
                MessageChatSetTheme chatSetTheme => UpdateChatSetTheme(message, chatSetTheme, history),
                MessageChatDeleteMember chatDeleteMember => UpdateChatDeleteMember(message, chatDeleteMember, history),
                MessageChatDeletePhoto chatDeletePhoto => UpdateChatDeletePhoto(message, chatDeletePhoto, history),
                MessageChatHasProtectedContentToggled chatHasProtectedContentToggled => UpdateChatHasProtectedContentToggled(message, chatHasProtectedContentToggled, history),
                MessageChatHasProtectedContentDisableRequested chatHasProtectedContentDisableRequested => UpdateChatHasProtectedContentDisableRequested(message, chatHasProtectedContentDisableRequested, history),
                MessageChatJoinByLink chatJoinByLink => UpdateChatJoinByLink(message, chatJoinByLink, history),
                MessageChatJoinByRequest chatJoinByRequest => UpdateChatJoinByRequest(message, chatJoinByRequest, history),
                MessageChatSetBackground chatSetBackground => UpdateChatSetBackground(message, chatSetBackground, history),
                MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime => UpdateChatSetMessageAutoDeleteTime(message, chatSetMessageAutoDeleteTime, history),
                MessageChatShared chatShared => UpdateChatShared(message, chatShared, history),
                MessageChatUpgradeFrom chatUpgradeFrom => UpdateChatUpgradeFrom(message, chatUpgradeFrom, history),
                MessageChatUpgradeTo chatUpgradeTo => UpdateChatUpgradeTo(message, chatUpgradeTo, history),
                MessageContactRegistered contactRegistered => UpdateContactRegistered(message, contactRegistered, history),
                MessageCustomServiceAction customServiceAction => UpdateCustomServiceAction(message, customServiceAction, history),
                MessageDirectMessagePriceChanged directMessagePriceChanged => UpdateDirectMessagePriceChanged(message, directMessagePriceChanged, history),
                MessageForumTopicCreated forumTopicCreated => UpdateForumTopicCreated(message, forumTopicCreated, history),
                MessageForumTopicEdited forumTopicEdited => UpdateForumTopicEdited(message, forumTopicEdited, history),
                MessageForumTopicIsClosedToggled forumTopicIsClosedToggled => UpdateForumTopicIsClosedToggled(message, forumTopicIsClosedToggled, history),
                MessageForumTopicIsHiddenToggled forumTopicIsHiddenToggled => UpdateForumTopicIsHiddenToggled(message, forumTopicIsHiddenToggled, history),
                MessageGameScore gameScore => UpdateGameScore(message, gameScore, history),
                MessageGift gift => UpdateGift(message, gift, history),
                MessageGiftedPremium giftedPremium => UpdateGiftedPremium(message, giftedPremium, history),
                MessageGiftedStars giftedStars => UpdateGiftedStars(message, giftedStars, history),
                MessageGiveawayCreated giveawayCreated => UpdateGiveawayCreated(message, giveawayCreated, history),
                MessageGiveawayCompleted giveawayCompleted => UpdateGiveawayCompleted(message, giveawayCompleted, history),
                MessageGiveawayPrizeStars giveawayPrizeStars => UpdateGiveawayPrizeStars(message, giveawayPrizeStars, history),
                MessageInviteVideoChatParticipants inviteVideoChatParticipants => UpdateInviteVideoChatParticipants(message, inviteVideoChatParticipants, history),
                MessageProximityAlertTriggered proximityAlertTriggered => UpdateProximityAlertTriggered(message, proximityAlertTriggered, history),
                MessagePremiumGiftCode premiumGiftCode => UpdatePremiumGiftCode(message, premiumGiftCode, history),
                MessagePaidMessagePriceChanged paidMessagePriceChanged => UpdatePaidMessagePriceChanged(message, paidMessagePriceChanged, history),
                MessagePaidMessagesRefunded paidMessagesRefunded => UpdatePaidMessagesRefunded(message, paidMessagesRefunded, history),
                MessagePassportDataSent passportDataSent => UpdatePassportDataSent(message, passportDataSent, history),
                MessagePaymentSuccessful paymentSuccessful => UpdatePaymentSuccessful(message, paymentSuccessful, history),
                MessagePaymentRefunded paymentRefunded => UpdatePaymentRefunded(message, paymentRefunded, history),
                MessagePinMessage pinMessage => UpdatePinMessage(message, pinMessage, history),
                MessageScreenshotTaken screenshotTaken => UpdateScreenshotTaken(message, screenshotTaken, history),
                MessageSuggestBirthdate suggestBirthdate => UpdateSuggestBirthdate(message, suggestBirthdate, history),
                MessageSuggestProfilePhoto suggestProfilePhoto => UpdateSuggestProfilePhoto(message, suggestProfilePhoto, history),
                MessageSupergroupChatCreate supergroupChatCreate => UpdateSupergroupChatCreate(message, supergroupChatCreate, history),
                MessageUpgradedGift upgradedGift => UpdateUpgradedGift(message, upgradedGift, history),
                MessageUpgradedGiftPurchaseOffer upgradedGiftPurchaseOffer => UpdateUpgradedGiftPurchaseOffer(message, upgradedGiftPurchaseOffer, history),
                MessageUpgradedGiftPurchaseOfferRejected upgradedGiftPurchaseOfferRejected => UpdateUpgradedGiftPurchaseOfferRejected(message, upgradedGiftPurchaseOfferRejected, history),
                MessageUsersShared usersShared => UpdateUsersShared(message, usersShared, history),
                MessageVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, history),
                MessageVideoChatScheduled videoChatScheduled => UpdateVideoChatScheduled(message, videoChatScheduled, history),
                MessageVideoChatStarted videoChatStarted => UpdateVideoChatStarted(message, videoChatStarted, history),
                MessageWebAppDataSent webAppDataSent => UpdateWebAppDataSent(message, webAppDataSent, history),
                MessageExpiredPhoto expiredPhoto => UpdateExpiredPhoto(message, expiredPhoto, history),
                MessageExpiredVideo expiredVideo => UpdateExpiredVideo(message, expiredVideo, history),
                MessageExpiredVideoNote expiredVideoNote => UpdateExpiredVideoNote(message, expiredVideoNote, history),
                MessageExpiredVoiceNote expiredVoiceNote => UpdateExpiredVoiceNote(message, expiredVoiceNote, history),
                MessageChatBoost chatBoost => UpdateChatBoost(message, chatBoost, history),
                MessageChecklistTasksAdded checklistTasksAdded => UpdateChecklistTasksAdded(message, checklistTasksAdded, history),
                MessageChecklistTasksDone checklistTasksDone => UpdateChecklistTasksDone(message, checklistTasksDone, history),
                MessagePollOptionAdded pollOptionAdded => UpdatePollOptionAdded(message, pollOptionAdded, history),
                MessagePollOptionDeleted pollOptionDeleted => UpdatePollOptionDeleted(message, pollOptionDeleted, history),
                MessageSuggestedPostPaid suggestedPostPaid => UpdateSuggestedPostPaid(message, suggestedPostPaid, history),
                MessageSuggestedPostRefunded suggestedPostRefunded => UpdateSuggestedPostRefunded(message, suggestedPostRefunded, history),
                MessageAsyncStory story => UpdateStory(message, story, history),
                MessageStory story => UpdateStory(message, story, history),
                // Local types:
                MessageChatEvent chatEvent => chatEvent.Action switch
                {
                    ChatEventAutomaticTranslationToggled automaticTranslationToggled => UpdateAutomaticTranslationToggled(message, automaticTranslationToggled, history),
                    ChatEventAvailableReactionsChanged availableReactionsChanged => UpdateAvailableReactionsChanged(message, availableReactionsChanged, history),
                    ChatEventHasProtectedContentToggled hasProtectedContentToggled => UpdateHasProtectedContentToggled(message, hasProtectedContentToggled, history),
                    ChatEventSignMessagesToggled signMessagesToggled => UpdateSignMessagesToggled(message, signMessagesToggled, history),
                    ChatEventShowMessageSenderToggled showMessageSenderToggled => UpdateShowMessageSenderToggled(message, showMessageSenderToggled, history),
                    ChatEventStickerSetChanged stickerSetChanged => UpdateStickerSetChanged(message, stickerSetChanged, history),
                    ChatEventCustomEmojiStickerSetChanged customemojiStickerSetChanged => UpdateCustomEmojiStickerSetChanged(message, customemojiStickerSetChanged, history),
                    ChatEventInvitesToggled invitesToggled => UpdateInvitesToggled(message, invitesToggled, history),
                    ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled => UpdateIsAllHistoryAvailableToggled(message, isAllHistoryAvailableToggled, history),
                    ChatEventLinkedChatChanged linkedChatChanged => UpdateLinkedChatChanged(message, linkedChatChanged, history),
                    ChatEventLocationChanged locationChanged => UpdateLocationChanged(message, locationChanged, history),
                    ChatEventMemberJoinedByInviteLink memberJoinedByInviteLink => UpdateMemberJoinedByInviteLink(message, memberJoinedByInviteLink, history),
                    ChatEventMessageUnpinned messageUnpinned => UpdateMessageUnpinned(message, messageUnpinned, history),
                    ChatEventMessageDeleted messageDeleted => UpdateMessageDeleted(message, messageDeleted, history),
                    ChatEventMessageEdited messageEdited => UpdateMessageEdited(message, messageEdited, history),
                    ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged => UpdateMessageAutoDeleteTimeChanged(message, messageAutoDeleteTimeChanged, history),
                    ChatEventDescriptionChanged descriptionChanged => UpdateDescriptionChanged(message, descriptionChanged, history),
                    ChatEventInviteLinkDeleted inviteLinkDeleted => UpdateInviteLinkDeleted(message, inviteLinkDeleted, history),
                    ChatEventInviteLinkEdited inviteLinkEdited => UpdateInviteLinkEdited(message, inviteLinkEdited, history),
                    ChatEventInviteLinkRevoked inviteLinkRevoked => UpdateInviteLinkRevoked(message, inviteLinkRevoked, history),
                    ChatEventMessagePinned messagePinned => UpdateMessagePinned(message, messagePinned, history),
                    ChatEventUsernameChanged usernameChanged => UpdateUsernameChanged(message, usernameChanged, history),
                    ChatEventPollStopped pollStopped => UpdatePollStopped(message, pollStopped, history),
                    ChatEventSlowModeDelayChanged slowModeDelayChanged => UpdateSlowModeDelayChanged(message, slowModeDelayChanged, history),
                    ChatEventVideoChatCreated videoChatCreated => UpdateVideoChatCreated(message, videoChatCreated, history),
                    ChatEventVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, history),
                    ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled => UpdateVideoChatMuteNewParticipantsToggled(message, videoChatMuteNewParticipantsToggled, history),
                    ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled => UpdateVideoChatParticipantIsMutedToggled(message, videoChatParticipantIsMutedToggled, history),
                    ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged => UpdateVideoChatParticipantVolumeLevelChanged(message, videoChatParticipantVolumeLevelChanged, history),
                    ChatEventIsForumToggled isForumToggled => UpdateChatEventIsForumToggled(message, isForumToggled, history),
                    ChatEventForumTopicCreated forumTopicCreated => UpdateChatEventForumTopicCreated(message, forumTopicCreated, history),
                    ChatEventForumTopicDeleted forumTopicDeleted => UpdateChatEventForumTopicDeleted(message, forumTopicDeleted, history),
                    ChatEventForumTopicEdited forumTopicEdited => UpdateChatEventForumTopicEdited(message, forumTopicEdited, history),
                    ChatEventForumTopicPinned forumTopicPinned => UpdateChatEventForumTopicPinned(message, forumTopicPinned, history),
                    ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed => UpdateChatEventForumTopicToggleIsClosed(message, forumTopicToggleIsClosed, history),
                    ChatEventAccentColorChanged accentColorChanged => UpdateChatEventAccentColorChanged(message, accentColorChanged, history),
                    ChatEventProfileAccentColorChanged profileAccentColorChanged => UpdateChatEventProfileAccentColorChanged(message, profileAccentColorChanged, history),
                    ChatEventEmojiStatusChanged emojiStatusChanged => UpdateChatEventEmojiStatusChanged(message, emojiStatusChanged, history),
                    ChatEventMemberTagChanged memberTagChanged => UpdateChatEventMemberTagChanged(message, memberTagChanged, history),
                    ChatEventBackgroundChanged backgroundChanged => UpdateChatEventBackgroundChanged(message, backgroundChanged, history),
                    //ChatEventActiveUsernamesChanged activeUsernamesChanged => UpdateChatEventActiveUsernames(messageUsernamesChanged),
                    _ => _emptyString
                },
                MessageHeaderDate headerDate => UpdateHeaderDate(message, headerDate),
                _ => _emptyString
            };
        }

        #region Local

        private static FormattedText UpdateHeaderDate(MessageWithOwner message, MessageHeaderDate headerDate)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                return string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(sendAtDate.SendDate))).AsFormattedText();
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed sendWhenVideoProcessed)
            {
                return string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(sendWhenVideoProcessed.SendDate))).AsFormattedText();
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                return Strings.MessageScheduledUntilOnline.AsFormattedText();
            }

            return Formatter.DayGrouping(Formatter.ToLocalTime(headerDate.Date)).AsFormattedText();
        }

        #endregion

        #region Event log

        private static FormattedText UpdateChatEventAccentColorChanged(MessageWithOwner message, ChatEventAccentColorChanged accentColorChanged, bool history)
        {
            FormattedText oldEmoji;
            FormattedText newEmoji;

            if (accentColorChanged.OldBackgroundCustomEmojiId != 0)
            {
                oldEmoji = new FormattedText("{0}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(accentColorChanged.OldBackgroundCustomEmojiId)) });
            }
            else
            {
                oldEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            if (accentColorChanged.NewBackgroundCustomEmojiId != 0)
            {
                newEmoji = new FormattedText("{1}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(accentColorChanged.NewBackgroundCustomEmojiId)) });
            }
            else
            {
                newEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            return ReplaceWithLink(ClientEx.Format(Strings.EventLogChangedPeerColorIcon, oldEmoji, newEmoji), message.GetSender());
        }

        private static FormattedText UpdateChatEventProfileAccentColorChanged(MessageWithOwner message, ChatEventProfileAccentColorChanged profileAccentColorChanged, bool history)
        {
            FormattedText oldEmoji;
            FormattedText newEmoji;

            if (profileAccentColorChanged.OldProfileBackgroundCustomEmojiId != 0)
            {
                oldEmoji = new FormattedText("{0}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(profileAccentColorChanged.OldProfileBackgroundCustomEmojiId)) });
            }
            else
            {
                oldEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            if (profileAccentColorChanged.NewProfileBackgroundCustomEmojiId != 0)
            {
                newEmoji = new FormattedText("{1}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(profileAccentColorChanged.NewProfileBackgroundCustomEmojiId)) });
            }
            else
            {
                newEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            return ReplaceWithLink(ClientEx.Format(Strings.EventLogChangedProfileColorIcon, oldEmoji, newEmoji), message.GetSender());
        }

        private static FormattedText UpdateChatEventEmojiStatusChanged(MessageWithOwner message, ChatEventEmojiStatusChanged emojiStatusChanged, bool history)
        {
            return _emptyString;

            //FormattedText oldEmoji;
            //FormattedText newEmoji;

            //if (emojiStatusChanged.NewEmojiStatus != null)
            //{
            //    // TODO: FormatTtl may not return the right value
            //    if (emojiStatusChanged.NewEmojiStatus.ExpirationDate != 0)
            //    {
            //        if (emojiStatusChanged.OldEmojiStatus != null)
            //        {
            //            content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFromFor, "un1", fromUser, entities);
            //            content = string.Format(content, "{0}", "{1}", Locale.FormatTtl(emojiStatusChanged.NewEmojiStatus.ExpirationDate - message.Date));
            //        }
            //        else
            //        {
            //            content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFor, "un1", fromUser, entities);
            //            content = string.Format(content, "{0}", "{1}", Locale.FormatTtl(emojiStatusChanged.NewEmojiStatus.ExpirationDate - message.Date));
            //        }
            //    }
            //    else if (emojiStatusChanged.OldEmojiStatus != null)
            //    {
            //        content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFrom, "un1", fromUser, entities);
            //    }
            //    else
            //    {
            //        content = ReplaceWithLink(Strings.EventLogChangedEmojiStatus, "un1", fromUser, entities);
            //    }
            //}
            //else
            //{
            //    content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFrom, "un1", fromUser, entities);
            //}

            //var index1 = content.IndexOf("{0}");
            //if (index1 != -1)
            //{
            //    if (emojiStatusChanged.OldEmojiStatus?.Type is EmojiStatusTypeCustomEmoji oldCustomEmoji)
            //    {
            //        entities.Add(new TextEntity(index1, 3, new TextEntityTypeCustomEmoji(oldCustomEmoji.CustomEmojiId)));
            //    }
            //    else if (emojiStatusChanged.OldEmojiStatus?.Type is EmojiStatusTypeUpgradedGift oldUpgradedGift)
            //    {
            //        entities.Add(new TextEntity(index1, 3, new TextEntityTypeCustomEmoji(oldUpgradedGift.ModelCustomEmojiId)));
            //    }
            //    else
            //    {
            //        content = content.Remove(index1, 3);
            //        content = content.Insert(index1, Strings.EventLogEmojiNone);
            //    }
            //}

            //var index2 = content.IndexOf("{1}");
            //if (index2 != -1)
            //{
            //    if (emojiStatusChanged.NewEmojiStatus?.Type is EmojiStatusTypeCustomEmoji newCustomEmoji)
            //    {
            //        entities.Add(new TextEntity(index2, 3, new TextEntityTypeCustomEmoji(newCustomEmoji.CustomEmojiId)));
            //    }
            //    else if (emojiStatusChanged.NewEmojiStatus?.Type is EmojiStatusTypeUpgradedGift newUpgradedGift)
            //    {
            //        entities.Add(new TextEntity(index2, 3, new TextEntityTypeCustomEmoji(newUpgradedGift.ModelCustomEmojiId)));
            //    }
            //    else
            //    {
            //        content = content.Remove(index2, 3);
            //        content = content.Insert(index2, Strings.EventLogEmojiNone);
            //    }
            //}

            //return new FormattedText(content, entities);
        }

        private static FormattedText UpdateChatEventMemberTagChanged(MessageWithOwner message, ChatEventMemberTagChanged memberTagChanged, bool history)
        {
            var newValue = !string.IsNullOrEmpty(memberTagChanged.NewTag);
            var oldValue = !string.IsNullOrEmpty(memberTagChanged.OldTag);

            var outgoing = message.SenderId.IsUser(memberTagChanged.UserId);

            if (newValue && oldValue)
            {
                return outgoing
                    ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfEdit, message.GetSender()), memberTagChanged.OldTag, memberTagChanged.NewTag)
                    : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankEdit, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.OldTag, memberTagChanged.NewTag);
            }
            else if (newValue && !oldValue)
            {
                return outgoing
                    ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfAdd, message.GetSender()), memberTagChanged.NewTag)
                    : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankAdd, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.NewTag);
            }

            return outgoing
                ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfRemove, message.GetSender()), memberTagChanged.OldTag)
                : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankRemove, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.OldTag);
        }

        private static FormattedText UpdateChatEventBackgroundChanged(MessageWithOwner message, ChatEventBackgroundChanged backgroundChanged, bool history)
        {
            if (backgroundChanged.NewBackground != null)
            {
                return ReplaceWithLink(Strings.EventLogChangedWallpaper, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogRemovedWallpaper, message.GetSender());
            }
        }

        private static FormattedText UpdateSlowModeDelayChanged(MessageWithOwner message, ChatEventSlowModeDelayChanged slowModeDelayChanged, bool history)
        {
            if (slowModeDelayChanged.NewSlowModeDelay > 0)
            {
                if (slowModeDelayChanged.NewSlowModeDelay < 60)
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeSeconds, slowModeDelayChanged.NewSlowModeDelay)), message.GetSender());
                }
                else if (slowModeDelayChanged.NewSlowModeDelay < 60 * 60)
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeMinutes, slowModeDelayChanged.NewSlowModeDelay / 60)), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeHours, slowModeDelayChanged.NewSlowModeDelay / 60 / 60)), message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSlowmodeOff, message.GetSender());
            }
        }

        private static FormattedText UpdateAutomaticTranslationToggled(MessageWithOwner message, ChatEventAutomaticTranslationToggled automaticTranslationToggled, bool history)
        {
            if (automaticTranslationToggled.HasAutomaticTranslation)
            {
                return ReplaceWithLink(Strings.EventLogToggledAutotranslationOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledAutotranslationOff, message.GetSender());
            }
        }

        private static FormattedText UpdateAvailableReactionsChanged(MessageWithOwner message, ChatEventAvailableReactionsChanged availableReactionsChanged, bool history)
        {
            var oldAllOrNone = availableReactionsChanged.OldAvailableReactions is ChatAvailableReactionsAll or ChatAvailableReactionsSome { Reactions.Count: 0 };
            var newAllOrNone = availableReactionsChanged.NewAvailableReactions is ChatAvailableReactionsAll or ChatAvailableReactionsSome { Reactions.Count: 0 };

            static FormattedText ToString(ChatAvailableReactions reactions)
            {
                if (reactions is ChatAvailableReactionsAll || reactions is not ChatAvailableReactionsSome some)
                {
                    return Strings.AllReactions.AsFormattedText();
                }

                if (some.Reactions.Count > 0)
                {
                    var content = new StringBuilder();
                    var entities = new MutableVector<TextEntity>();

                    foreach (var item in some.Reactions)
                    {
                        if (item is ReactionTypeEmoji emoji)
                        {
                            content.Append(emoji.Emoji);
                        }
                        else if (item is ReactionTypeCustomEmoji customEmoji)
                        {
                            entities.Add(new TextEntity(content.Length, 2, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId)));
                            content.Append("\U0001F921");
                        }
                    }

                    return new FormattedText(content.ToString(), entities);
                }

                return Strings.NoReactions.AsFormattedText();
            }

            if (oldAllOrNone || newAllOrNone)
            {
                var oldText = ToString(availableReactionsChanged.OldAvailableReactions);
                var newText = ToString(availableReactionsChanged.NewAvailableReactions);

                var content = ClientEx.Format(Strings.ActionReactionsChanged, oldText, newText);
                return ReplaceWithLink(content, message.GetSender());
            }
            else
            {
                var content = ClientEx.Format(Strings.ActionReactionsChangedList, ToString(availableReactionsChanged.NewAvailableReactions));
                return ReplaceWithLink(content, message.GetSender());
            }
        }

        private static FormattedText UpdateHasProtectedContentToggled(MessageWithOwner message, ChatEventHasProtectedContentToggled hasProtectedContentToggled, bool history)
        {
            if (hasProtectedContentToggled.HasProtectedContent)
            {
                return ReplaceWithLink(message.IsChannelPost
                    ? Strings.ActionForwardsRestrictedChannel
                    : Strings.ActionForwardsRestrictedGroup, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(message.IsChannelPost
                    ? Strings.ActionForwardsEnabledChannel
                    : Strings.ActionForwardsEnabledGroup, message.GetSender());
            }
        }

        private static FormattedText UpdateSignMessagesToggled(MessageWithOwner message, ChatEventSignMessagesToggled signMessagesToggled, bool history)
        {
            if (signMessagesToggled.SignMessages)
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateShowMessageSenderToggled(MessageWithOwner message, ChatEventShowMessageSenderToggled showMessageSenderToggled, bool history)
        {
            if (showMessageSenderToggled.ShowMessageSender)
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesProfilesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesProfilesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateStickerSetChanged(MessageWithOwner message, ChatEventStickerSetChanged stickerSetChanged, bool history)
        {
            if (stickerSetChanged.NewStickerSetId == 0)
            {
                return ReplaceWithLink(Strings.EventLogRemovedStickersSet, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedStickersSet, message.GetSender());
            }
        }

        private static FormattedText UpdateCustomEmojiStickerSetChanged(MessageWithOwner message, ChatEventCustomEmojiStickerSetChanged customEmojiStickerSetChanged, bool history)
        {
            if (customEmojiStickerSetChanged.NewStickerSetId == 0)
            {
                return ReplaceWithLink(Strings.EventLogRemovedEmojiPack, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedEmojiPack, message.GetSender());
            }
        }

        private static FormattedText UpdateInvitesToggled(MessageWithOwner message, ChatEventInvitesToggled invitesToggled, bool history)
        {
            if (invitesToggled.CanInviteUsers)
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateIsAllHistoryAvailableToggled(MessageWithOwner message, ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled, bool history)
        {
            if (isAllHistoryAvailableToggled.IsAllHistoryAvailable)
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesHistoryOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesHistoryOff, message.GetSender());
            }
        }

        private static FormattedText UpdateLinkedChatChanged(MessageWithOwner message, ChatEventLinkedChatChanged linkedChatChanged, bool history)
        {
            if (message.IsChannelPost)
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    return ReplaceWithLink(Strings.EventLogChangedLinkedGroup, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId));
                }
                else
                {
                    return ReplaceWithLink(Strings.EventLogRemovedLinkedGroup, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId));
                }
            }
            else
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    return ReplaceWithLink(Strings.EventLogChangedLinkedChannel, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId));
                }
                else
                {
                    return ReplaceWithLink(Strings.EventLogRemovedLinkedChannel, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId));
                }
            }
        }

        private static FormattedText UpdateLocationChanged(MessageWithOwner message, ChatEventLocationChanged locationChanged, bool history)
        {
            if (locationChanged.NewLocation != null)
            {
                return ReplaceWithLink(string.Format(Strings.EventLogChangedLocation, locationChanged.NewLocation.Address), message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogRemovedLocation, message.GetSender());
            }
        }

        private static FormattedText UpdateMemberJoinedByInviteLink(MessageWithOwner message, ChatEventMemberJoinedByInviteLink memberJoinedByInviteLink, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionInviteYou.AsFormattedText();
            }
            else
            {
                if (memberJoinedByInviteLink.ViaChatFolderInviteLink)
                {
                    return ReplaceWithLink(Strings.ActionInviteUserFolder, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionInviteUser, message.GetSender());
                }
            }
        }

        private static FormattedText UpdateMessageUnpinned(MessageWithOwner message, ChatEventMessageUnpinned messageUnpinned, bool history)
        {
            return ReplaceWithLink(Strings.EventLogUnpinnedMessages, message.GetSender());
        }

        private static FormattedText UpdateMessageDeleted(MessageWithOwner message, ChatEventMessageDeleted messageDeleted, bool history)
        {
            return ReplaceWithLink(Strings.EventLogDeletedMessages, message.GetSender());
        }

        private static FormattedText UpdateMessageEdited(MessageWithOwner message, ChatEventMessageEdited messageEdited, bool history)
        {
            if (messageEdited.NewMessage.Content is MessageText)
            {
                return ReplaceWithLink(Strings.EventLogEditedMessages, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEditedCaption, message.GetSender());
            }
        }

        private static FormattedText UpdateMessageAutoDeleteTimeChanged(MessageWithOwner message, ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged, bool history)
        {
            if (messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime > 0)
            {
                return ReplaceWithLink(string.Format(Strings.ActionTTLChanged, Locale.FormatTtl(messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime)), message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.ActionTTLDisabled, message.GetSender());
            }
        }

        private static FormattedText UpdateDescriptionChanged(MessageWithOwner message, ChatEventDescriptionChanged descriptionChanged, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogEditedChannelDescription, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEditedGroupDescription, message.GetSender());
            }
        }

        private static FormattedText UpdateInviteLinkDeleted(MessageWithOwner message, ChatEventInviteLinkDeleted inviteLinkDeleted, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionDeletedInviteLink, inviteLinkDeleted.InviteLink.InviteLink), message.GetSender());
        }

        private static FormattedText UpdateInviteLinkEdited(MessageWithOwner message, ChatEventInviteLinkEdited inviteLinkEdited, bool history)
        {
            //if (inviteLinkEdited.)
            //{
            //}
            //else
            {
                return ReplaceWithLink(string.Format(Strings.ActionEditedInviteLinkToSame, inviteLinkEdited.NewInviteLink.InviteLink), message.GetSender());
            }
        }

        private static FormattedText UpdateInviteLinkRevoked(MessageWithOwner message, ChatEventInviteLinkRevoked inviteLinkRevoked, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionRevokedInviteLink, inviteLinkRevoked.InviteLink.InviteLink), message.GetSender());
        }

        private static FormattedText UpdateMessagePinned(MessageWithOwner message, ChatEventMessagePinned messagePinned, bool history)
        {
            return ReplaceWithLink(Strings.EventLogPinnedMessages, message.GetSender());
        }

        private static FormattedText UpdateUsernameChanged(MessageWithOwner message, ChatEventUsernameChanged usernameChanged, bool history)
        {
            if (string.IsNullOrEmpty(usernameChanged.NewUsername))
            {
                return ReplaceWithLink(Strings.EventLogRemovedGroupLink, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedGroupLink, message.GetSender());
            }
        }

        private static FormattedText UpdatePollStopped(MessageWithOwner message, ChatEventPollStopped pollStopped, bool history)
        {
            if (pollStopped.Message.Content is not MessagePoll poll)
            {
                return _emptyString;
            }

            if (poll.Poll.Type is PollTypeRegular)
            {
                return ReplaceWithLink(Strings.EventLogStopPoll, message.GetSender());
            }
            else if (poll.Poll.Type is PollTypeQuiz)
            {
                return ReplaceWithLink(Strings.EventLogStopQuiz, message.GetSender());
            }

            return _emptyString;
        }

        private static FormattedText UpdateVideoChatCreated(MessageWithOwner message, ChatEventVideoChatCreated videoChatCreated, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogStartedLiveStream, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogStartedVoiceChat, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatEnded(MessageWithOwner message, ChatEventVideoChatEnded videoChatEnded, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogEndedLiveStream, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEndedVoiceChat, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatMuteNewParticipantsToggled(MessageWithOwner message, ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled, bool history)
        {
            if (videoChatMuteNewParticipantsToggled.MuteNewParticipants)
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatNotAllowedToSpeak, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatAllowedToSpeak, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatParticipantIsMutedToggled(MessageWithOwner message, ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled, bool history)
        {
            var fromUser = message.GetSender();
            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantIsMutedToggled.ParticipantId);

            if (videoChatParticipantIsMutedToggled.IsMuted)
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatMuted, fromUser, whoUser);
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatUnmuted, fromUser, whoUser);
            }
        }

        private static FormattedText UpdateVideoChatParticipantVolumeLevelChanged(MessageWithOwner message, ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged, bool history)
        {
            var fromUser = message.GetSender();
            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantVolumeLevelChanged.ParticipantId);

            return ReplaceWithLink(string.Format(Strings.ActionVolumeChanged, videoChatParticipantVolumeLevelChanged.VolumeLevel), fromUser, whoUser);
        }

        private static FormattedText UpdateChatEventIsForumToggled(MessageWithOwner message, ChatEventIsForumToggled isForumToggled, bool history)
        {
            return ReplaceWithLink(isForumToggled.IsForum
                ? Strings.EventLogSwitchToForum
                : Strings.EventLogSwitchToGroup, message.GetSender());
        }

        private static FormattedText UpdateChatEventForumTopicCreated(MessageWithOwner message, ChatEventForumTopicCreated forumTopicCreated, bool history)
        {
            return ReplaceWithLink(Strings.EventLogCreateTopic, message.GetSender(), forumTopicCreated.TopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicDeleted(MessageWithOwner message, ChatEventForumTopicDeleted forumTopicDeleted, bool history)
        {
            return ReplaceWithLink(Strings.EventLogDeleteTopic, message.GetSender(), forumTopicDeleted.TopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicEdited(MessageWithOwner message, ChatEventForumTopicEdited forumTopicEdited, bool history)
        {
            return ReplaceWithLink(Strings.EventLogEditTopic, message.GetSender(), forumTopicEdited.OldTopicInfo, forumTopicEdited.NewTopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicPinned(MessageWithOwner message, ChatEventForumTopicPinned forumTopicPinned, bool history)
        {
            if (forumTopicPinned.NewTopicInfo != null)
            {
                return ReplaceWithLink(Strings.EventLogPinTopic, message.GetSender(), forumTopicPinned.NewTopicInfo);
            }
            else if (forumTopicPinned.OldTopicInfo != null)
            {
                return ReplaceWithLink(Strings.EventLogUnpinTopic, message.GetSender(), forumTopicPinned.OldTopicInfo);
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatEventForumTopicToggleIsClosed(MessageWithOwner message, ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed, bool history)
        {
            // TODO
            return _emptyString;
        }

        //private static FormattedText UpdateChatEventActiveUsernames(MessageWithOwner message, ChatEventActiveUsernamesChanged activeUsernamesChanged)
        //{
        //    //var content = string.Empty;
        //    //var entities = active ? new List<TextEntity>() : null;

        //    //var fromUser = message.GetSender();

        //    //content = ReplaceWithLink(isForumToggled.IsForum
        //    //    ? Strings.EventLogSwitchToForum
        //    //    : Strings.EventLogSwitchToGroup, "un1", fromUser, entities);

        //    //return (content, entities);
        //}

        #endregion

        private static FormattedText UpdateBasicGroupChatCreate(MessageWithOwner message, MessageBasicGroupChatCreate basicGroupChatCreate, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionYouCreateGroup.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionCreateGroup, message.GetSender());
            }
        }

        private static FormattedText UpdateBotWriteAccessAllowed(MessageWithOwner message, MessageBotWriteAccessAllowed botWriteAccessAllowed, bool history)
        {
            if (botWriteAccessAllowed.Reason is BotWriteAccessAllowReasonConnectedWebsite websiteConnected)
            {
                var content = Strings.ActionBotAllowed;
                var entities = new MutableVector<TextEntity>();

                var start = content.IndexOf("{0}");
                content = string.Format(content, websiteConnected.DomainName);

                if (start >= 0)
                {
                    entities.Add(new TextEntity(start, websiteConnected.DomainName.Length, new TextEntityTypeUrl()));
                }

                return new FormattedText(content, entities);
            }

            return Strings.ActionBotAllowedWebapp.AsFormattedText();
        }

        private static FormattedText UpdateChatAddMembers(MessageWithOwner message, MessageChatAddMembers chatAddMembers, bool history)
        {
            try
            {
                long singleUserId = 0;
                if (chatAddMembers.MemberUserIds.Count == 1)
                {
                    singleUserId = chatAddMembers.MemberUserIds[0];
                }

                if (singleUserId != 0)
                {
                    if (message.SenderId is MessageSenderUser senderUser && singleUserId == senderUser.UserId)
                    {
                        if (message.Chat.Type is ChatTypeSupergroup { IsChannel: true })
                        {
                            if (singleUserId == message.ClientService.Options.MyId)
                            {
                                return Strings.ChannelJoined.AsFormattedText();
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.EventLogChannelJoined, message.GetSender());
                            }
                        }
                        else if (message.Chat.Type is ChatTypeSupergroup)
                        {
                            if (singleUserId == message.ClientService.Options.MyId)
                            {
                                return Strings.ChannelMegaJoined.AsFormattedText();
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.ActionAddUserSelfMega, message.GetSender());
                            }
                        }
                        else if (message.IsOutgoing)
                        {
                            return Strings.ActionAddUserSelfYou.AsFormattedText();
                        }
                        else
                        {
                            return ReplaceWithLink(Strings.ActionAddUserSelf, message.GetSender());
                        }
                    }
                    else
                    {
                        var whoUser = message.ClientService.GetUser(singleUserId);

                        if (message.IsOutgoing)
                        {
                            return ReplaceWithLink(Strings.ActionYouAddUser, "un2", whoUser);
                        }
                        else if (singleUserId == message.ClientService.Options.MyId)
                        {
                            if (message.Chat?.Type is ChatTypeSupergroup { IsChannel: true })
                            {
                                return ReplaceWithLink(Strings.ChannelAddedBy, message.GetSender());
                            }
                            else if (message.Chat?.Type is ChatTypeSupergroup)
                            {
                                return ReplaceWithLink(Strings.MegaAddedBy, message.GetSender());
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.ActionAddUserYou, message.GetSender());
                            }
                        }
                        else
                        {
                            return ReplaceWithLink(Strings.ActionAddUser, message.GetSender(), whoUser);
                        }
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return ReplaceWithLinks(Strings.ActionYouAddUser, "un2", chatAddMembers.MemberUserIds, message.ClientService);
                    }
                    else
                    {
                        var content = ReplaceWithLink(Strings.ActionAddUser, message.GetSender());
                        return ReplaceWithLinks(content, "un2", chatAddMembers.MemberUserIds, message.ClientService);
                    }
                }
            }
            catch
            {
                Logger.Info(message.Content);
                throw;
            }
        }

        private static FormattedText UpdateChatAddedToCommunity(MessageWithOwner message, MessageChatAddedToCommunity chatAddedToCommunity, bool history)
        {
            var community = message.ClientService.GetCommunity(chatAddedToCommunity.CommunityId);
            var name = community?.Name ?? string.Empty;

            var member = GetCommunityMember(message);
            var sender = GetCommunityActor(message);

            FormattedText formatted;
            if (sender != null && message.IsOutgoing)
            {
                formatted = ClientEx.Format(member switch
                {
                    CommunityMember.Channel => Strings.CommunityServiceMessageChannelYouAdded,
                    CommunityMember.Bot => Strings.CommunityServiceMessageBotYouAdded,
                    _ => Strings.CommunityServiceMessageGroupYouAdded
                }, name);
            }
            else if (sender != null)
            {
                // un1 goes in as the sender placeholder: the name is substituted by
                // ReplaceWithLink below, once the bold run around it has been parsed.
                formatted = ClientEx.Format(member switch
                {
                    CommunityMember.Channel => Strings.CommunityServiceMessageChannelAdded,
                    CommunityMember.Bot => Strings.CommunityServiceMessageBotAdded,
                    _ => Strings.CommunityServiceMessageGroupAdded
                }, "un1", name);
            }
            else
            {
                formatted = ClientEx.Format(member switch
                {
                    CommunityMember.Channel => Strings.CommunityServiceMessageChannelAddedUnknown,
                    CommunityMember.Bot => Strings.CommunityServiceMessageBotAddedUnknown,
                    _ => Strings.CommunityServiceMessageGroupAddedUnknown
                }, name);
            }

            formatted = ClientEx.ParseMarkdown(formatted);

            if (sender != null)
            {
                return ReplaceWithLink(formatted, sender);
            }

            return formatted;
        }

        private static FormattedText UpdateChatJoinFromCommunity(MessageWithOwner message, MessageChatJoinFromCommunity chatJoinFromCommunity, bool history)
        {
            var community = message.ClientService.GetCommunity(chatJoinFromCommunity.CommunityId);

            if (message.IsOutgoing)
            {
                return ReplaceWithLink(GetCommunityMember(message) == CommunityMember.Channel
                    ? Strings.ActionJoinedFromCommunityYouChannel
                    : Strings.ActionJoinedFromCommunityYou, community);
            }

            // un1 is the community, un2 the member who joined.
            return ReplaceWithLink(Strings.ActionJoinedFromCommunityUser, community, message.GetSender());
        }

        private static FormattedText UpdateChatRemovedFromCommunity(MessageWithOwner message, MessageChatRemovedFromCommunity chatRemovedFromCommunity, bool history)
        {
            // The community isn't part of the update, so these strings don't name it.
            var member = GetCommunityMember(message);
            var sender = GetCommunityActor(message);

            if (sender != null && message.IsOutgoing)
            {
                return ClientEx.ParseMarkdown(member switch
                {
                    CommunityMember.Channel => Strings.CommunityServiceMessageChannelYouRemoved,
                    CommunityMember.Bot => Strings.CommunityServiceMessageBotYouRemoved,
                    _ => Strings.CommunityServiceMessageGroupYouRemoved
                });
            }
            else if (sender != null)
            {
                var formatted = ClientEx.Format(member switch
                {
                    CommunityMember.Channel => Strings.CommunityServiceMessageChannelRemoved,
                    CommunityMember.Bot => Strings.CommunityServiceMessageBotRemoved,
                    _ => Strings.CommunityServiceMessageGroupRemoved
                }, "un1");

                return ReplaceWithLink(ClientEx.ParseMarkdown(formatted), sender);
            }

            return ClientEx.ParseMarkdown(member switch
            {
                CommunityMember.Channel => Strings.CommunityServiceMessageChannelRemovedUnknown,
                CommunityMember.Bot => Strings.CommunityServiceMessageBotRemovedUnknown,
                _ => Strings.CommunityServiceMessageGroupRemovedUnknown
            });
        }

        // Communities word these messages after what the member chat is.
        private enum CommunityMember
        {
            Group,
            Channel,
            Bot
        }

        private static CommunityMember GetCommunityMember(MessageWithOwner message)
        {
            if (message.Chat?.Type is ChatTypeSupergroup { IsChannel: true })
            {
                return CommunityMember.Channel;
            }
            else if (message.ClientService.TryGetUser(message.Chat, out User user) && user.Type is UserTypeBot)
            {
                return CommunityMember.Bot;
            }

            return CommunityMember.Group;
        }

        // The sender is the chat itself whenever an admin acts as it, and naming it would
        // read "This channel added this channel": those messages name no one instead.
        private static object GetCommunityActor(MessageWithOwner message)
        {
            var sender = message.GetSender();

            if (sender is Chat chat)
            {
                return chat.Id == message.Chat?.Id ? null : chat;
            }
            else if (sender is User user)
            {
                return message.Chat?.Type is ChatTypePrivate privata && privata.UserId == user.Id ? null : user;
            }

            return null;
        }

        private static FormattedText UpdateChatChangePhoto(MessageWithOwner message, MessageChatChangePhoto chatChangePhoto, bool history)
        {
            if (message.IsChannelPost)
            {
                return chatChangePhoto.Photo.Animation != null
                    ? Strings.ActionChannelChangedVideo.AsFormattedText()
                    : Strings.ActionChannelChangedPhoto.AsFormattedText();
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return chatChangePhoto.Photo.Animation != null
                        ? Strings.ActionYouChangedVideo.AsFormattedText()
                        : Strings.ActionYouChangedPhoto.AsFormattedText();
                }
                else
                {
                    return chatChangePhoto.Photo.Animation != null
                        ? ReplaceWithLink(Strings.ActionChangedVideo, message.GetSender())
                        : ReplaceWithLink(Strings.ActionChangedPhoto, message.GetSender());
                }
            }
        }

        private static FormattedText UpdateChatChangeTitle(MessageWithOwner message, MessageChatChangeTitle chatChangeTitle, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.ActionChannelChangedTitle, "un2", chatChangeTitle.Title);
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionYouChangedTitle, "un2", chatChangeTitle.Title);
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionChangedTitle.Replace("un2", chatChangeTitle.Title), message.GetSender());
                }
            }
        }

        private static FormattedText UpdateChatSetTheme(MessageWithOwner message, MessageChatSetTheme chatSetTheme, bool history)
        {
            if (message.IsOutgoing)
            {
                if (chatSetTheme.Theme is ChatThemeEmoji emoji)
                {
                    return string.Format(Strings.ChatThemeChangedYou, emoji.Name).AsFormattedText();
                }
                else if (chatSetTheme.Theme is ChatThemeGift gift)
                {
                    return string.Format(Strings.ChatThemeChangedYou, gift.GiftTheme.Gift.ToName()).AsFormattedText();
                }

                return Strings.ChatThemeDisabledYou.AsFormattedText();
            }
            else
            {
                if (chatSetTheme.Theme is ChatThemeEmoji emoji)
                {
                    return ReplaceWithLink(string.Format(Strings.ChatThemeChangedTo, "un1", emoji.Name), message.GetSender());
                }
                else if (chatSetTheme.Theme is ChatThemeGift gift)
                {
                    return ReplaceWithLink(string.Format(Strings.ChatThemeChangedTo, "un1", gift.GiftTheme.Gift.ToName()), message.GetSender());
                }

                return ReplaceWithLink(string.Format(Strings.ChatThemeDisabled, "un1"), message.GetSender());
            }
        }

        private static FormattedText UpdateChatDeleteMember(MessageWithOwner message, MessageChatDeleteMember chatDeleteMember, bool history)
        {
            if (message.SenderId is MessageSenderUser senderUser && chatDeleteMember.UserId == senderUser.UserId)
            {
                if (message.IsOutgoing)
                {
                    return Strings.ActionYouLeftUser.AsFormattedText();
                }
                else
                {
                    if (message.IsChannelPost)
                    {
                        return ReplaceWithLink(Strings.EventLogLeftChannel, message.GetSender());
                    }
                    else
                    {
                        return ReplaceWithLink(Strings.ActionLeftUser, message.GetSender());
                    }
                }
            }
            else
            {
                var whoUser = message.ClientService.GetUser(chatDeleteMember.UserId);
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionYouKickUser, "un2", whoUser);
                }
                else if (chatDeleteMember.UserId == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(Strings.ActionKickUserYou, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionKickUser, message.GetSender(), whoUser);
                }
            }
        }

        private static FormattedText UpdateChatDeletePhoto(MessageWithOwner message, MessageChatDeletePhoto chatDeletePhoto, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionChannelRemovedPhoto.AsFormattedText();
            }
            else if (message.IsOutgoing)
            {
                return Strings.ActionYouRemovedPhoto.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionRemovedPhoto, message.GetSender());
            }
        }


        private static FormattedText UpdateChatHasProtectedContentToggled(MessageWithOwner message, MessageChatHasProtectedContentToggled chatHasProtectedContentToggled, bool history)
        {
            if (chatHasProtectedContentToggled.NewHasProtectedContent == chatHasProtectedContentToggled.OldHasProtectedContent)
            {
                return chatHasProtectedContentToggled.NewHasProtectedContent
                    ? Strings.DisableSharingActionStillDisabled.AsFormattedText()
                    : Strings.DisableSharingActionStillEnabled.AsFormattedText();
            }

            if (message.IsOutgoing)
            {
                return chatHasProtectedContentToggled.NewHasProtectedContent
                    ? Strings.DisableSharingActionYou.AsFormattedText()
                    : Strings.EnableSharingActionYou.AsFormattedText();
            }

            return ReplaceWithName(chatHasProtectedContentToggled.NewHasProtectedContent ? Strings.DisableSharingActionOther : Strings.EnableSharingActionOther, message.GetSender());
        }

        private static FormattedText UpdateChatHasProtectedContentDisableRequested(MessageWithOwner message, MessageChatHasProtectedContentDisableRequested chatHasProtectedContentDisableRequested, bool history)
        {
            return message.IsOutgoing
                ? Strings.SharingOfferEnableHeaderYou.AsFormattedText()
                : ClientEx.ParseMarkdown(ReplaceWithName(Strings.SharingOfferEnableHeaderOther, message.GetSender()));
        }

        private static FormattedText UpdateChatJoinByLink(MessageWithOwner message, MessageChatJoinByLink chatJoinByLink, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionInviteYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionInviteUser, message.GetSender());
            }
        }

        private static FormattedText UpdateChatJoinByRequest(MessageWithOwner message, MessageChatJoinByRequest chatJoinByRequest, bool history)
        {
            return ReplaceWithLink(Strings.UserAcceptedToGroupAction, message.GetSender());
        }

        private static FormattedText UpdateChatSetBackground(MessageWithOwner message, MessageChatSetBackground chatSetBackground, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionSetWallpaperForThisChannel.AsFormattedText();
            }
            else if (chatSetBackground.OldBackgroundMessageId != 0)
            {
                if (message.IsOutgoing)
                {
                    return Strings.ActionSetSameWallpaperForThisChatSelf.AsFormattedText();
                }
                else if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    return string.Format(Strings.ActionSetSameWallpaperForThisChat, user.FullName(true)).AsFormattedText();
                }
            }
            else if (message.IsOutgoing)
            {
                if (chatSetBackground.OnlyForSelf)
                {
                    return Strings.ActionSetWallpaperForThisChatSelf.AsFormattedText();
                }
                else if (message.ClientService.TryGetUser(message.Chat, out User user))
                {
                    return string.Format(Strings.ActionSetWallpaperForThisChatSelfBoth, user.FullName(true)).AsFormattedText();
                }
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                return chatSetBackground.OnlyForSelf
                    ? string.Format(Strings.ActionSetWallpaperForThisChat, user.FullName(true)).AsFormattedText()
                    : string.Format(Strings.ActionSetWallpaperForThisChatBoth, user.FullName(true)).AsFormattedText();
            }
            else
            {
                return Strings.ActionSetWallpaperForThisGroup.AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatSetMessageAutoDeleteTime(MessageWithOwner message, MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime, bool history)
        {
            var chat = message.Chat;
            if (chat?.Type is ChatTypeSecret)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (message.IsOutgoing)
                    {
                        return string.Format(Strings.MessageLifetimeChangedOutgoing, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.MessageLifetimeChanged, "un1", Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), message.GetSender());
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return Strings.MessageLifetimeYouRemoved.AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.MessageLifetimeRemoved, "un1"), message.GetSender());
                    }
                }
            }
            else if (message.IsChannelPost)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    return string.Format(Strings.ActionTTLChannelChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                }
                else
                {
                    return Strings.ActionTTLChannelDisabled.AsFormattedText();
                }
            }
            else
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (chatSetMessageAutoDeleteTime.FromUserId == message.ClientService.Options.MyId)
                    {
                        return string.Format(Strings.AutoDeleteGlobalActionFromYou, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else if (chatSetMessageAutoDeleteTime.FromUserId != 0 && message.ClientService.TryGetUser(chatSetMessageAutoDeleteTime.FromUserId, out User fromUser))
                    {
                        return ReplaceWithLink(string.Format(Strings.AutoDeleteGlobalAction, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), fromUser);
                    }
                    else if (message.IsOutgoing)
                    {
                        return string.Format(Strings.ActionTTLYouChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionTTLChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), message.GetSender());
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return Strings.ActionTTLYouDisabled.AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(Strings.ActionTTLDisabled, message.GetSender());
                    }
                }
            }
        }

        private static FormattedText UpdateChatUpgradeFrom(MessageWithOwner message, MessageChatUpgradeFrom chatUpgradeFrom, bool history)
        {
            return (history ? Strings.GroupUpgradedFrom : Strings.GroupUpgradedTo).AsFormattedText();
        }

        private static FormattedText UpdateChatUpgradeTo(MessageWithOwner message, MessageChatUpgradeTo chatUpgradeTo, bool history)
        {
            return Strings.GroupUpgradedTo.AsFormattedText();
        }

        private static FormattedText UpdateContactRegistered(MessageWithOwner message, MessageContactRegistered contactRegistered, bool history)
        {
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return string.Format(Strings.NotificationContactJoined, senderUser.FullName()).AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateCustomServiceAction(MessageWithOwner message, MessageCustomServiceAction customServiceAction, bool history)
        {
            return customServiceAction.Text.AsFormattedText();
        }

        private static FormattedText UpdateForumTopicCreated(MessageWithOwner message, MessageForumTopicCreated forumTopicCreated, bool history)
        {
            var topicName = new FormattedText($"\U0001F4C3 {forumTopicCreated.Name}", new[]
            {
                new TextEntity(0, 2, new TextEntityTypeCustomEmoji(forumTopicCreated.Icon.CustomEmojiId))
            });

            return ClientEx.Format(Strings.TopicWasCreatedAction, topicName);
        }

        private static FormattedText UpdateForumTopicEdited(MessageWithOwner message, MessageForumTopicEdited forumTopicEdited, bool history)
        {
            // TopicWasIconChangedToAction, TopicWasRenamedToAction TopicWasRenamedToAction2
            // TopicIconChangedToAction, TopicRenamedToAction
            FormattedTextBuilder content;

            if (forumTopicEdited.EditIconCustomEmojiId && forumTopicEdited.Name.Length > 0)
            {
                content = BuildWithLink(new FormattedTextBuilder(string.Format(Strings.TopicWasRenamedToAction2, "un1", $"\U0001F4C3 {forumTopicEdited.Name}")), message.GetSender());
            }
            else if (forumTopicEdited.EditIconCustomEmojiId)
            {
                content = BuildWithLink(new FormattedTextBuilder(string.Format(Strings.TopicWasIconChangedToAction, "un1", "\U0001F4C3")), message.GetSender());
            }
            else
            {
                content = BuildWithLink(new FormattedTextBuilder(string.Format(Strings.TopicWasRenamedToAction, "un1", forumTopicEdited.Name)), message.GetSender());
            }

            var index = content.IndexOf("\U0001F4C3");
            if (index != -1)
            {
                content.AddEntity(index, 2, new TextEntityTypeCustomEmoji(forumTopicEdited.IconCustomEmojiId));
            }

            return content.ToFormattedText();
        }

        private static FormattedText UpdateForumTopicIsClosedToggled(MessageWithOwner message, MessageForumTopicIsClosedToggled forumTopicIsClosedToggled, bool history)
        {
            // TopicWasClosedAction, TopicWasReopenedAction
            // TopicClosed2, TopicRestarted2

            var content = string.Format(forumTopicIsClosedToggled.IsClosed
                ? Strings.TopicClosed2
                : Strings.TopicRestarted2, "un1");
            return ReplaceWithLink(content, message.GetSender());
        }

        private static FormattedText UpdateForumTopicIsHiddenToggled(MessageWithOwner message, MessageForumTopicIsHiddenToggled forumTopicIsHiddenToggled, bool history)
        {
            return ReplaceWithLink(forumTopicIsHiddenToggled.IsHidden
                ? Strings.TopicHidden2
                : Strings.TopicShown2, message.GetSender());
        }

        private static FormattedText UpdateGameScore(MessageWithOwner message, MessageGameScore gameScore, bool history)
        {
            var game = GetGame(message as MessageViewModel);
            if (game == null)
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        return string.Format(Strings.ActionYouScored, Locale.Declension(Strings.R.Points, gameScore.Score)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionUserScored, Locale.Declension(Strings.R.Points, gameScore.Score)), senderUser);
                    }
                }
            }
            else
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionYouScoredInGame, Locale.Declension(Strings.R.Points, gameScore.Score)), "un2", game);
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionUserScoredInGame, Locale.Declension(Strings.R.Points, gameScore.Score)), senderUser, game);
                    }
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateGift(MessageWithOwner message, MessageGift gift, bool history)
        {
            // TODO: markdown

            if (message.ChatId == message.ClientService.Options.MyId)
            {
                return ReplaceWithLink(Strings.ActionGiftSelf, "un2", gift);
            }
            if (message.IsOutgoing)
            {
                if (gift.IsPrepaidUpgrade && message.ClientService.TryGetMessageSender(gift.ReceiverId, out Object receiver))
                {
                    return ReplaceWithLink(Strings.ActionPrepaidGiftOutbound, receiver, gift);
                }

                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", gift);
            }
            else if (message.ClientService.TryGetMessageSender(gift.SenderId, out Object sender))
            {
                if (gift.ReceiverId.IsUser(message.ClientService.Options.MyId))
                {
                    if (gift.IsPrepaidUpgrade)
                    {
                        return ReplaceWithLink(Strings.ActionPrepaidGiftInbound, sender, gift);
                    }

                    return ReplaceWithLink(Strings.ActionGiftInbound, sender, gift);
                }
                else if (message.ClientService.TryGetMessageSender(gift.ReceiverId, out Object outboundUser))
                {
                    return ReplaceWithLink(Locale.Declension(Strings.R.ActionGiftChannel, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount), sender, outboundUser);
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGift2Received, "un2", gift);
            }

            return _emptyString;
        }

        private static FormattedText UpdateGiftedPremium(MessageWithOwner message, MessageGiftedPremium giftedPremium, bool history)
        {
            // TODO: markdown

            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", giftedPremium);
            }
            else if (message.ChatId == message.ClientService.Options.TelegramServiceNotificationsChatId)
            {
                return ReplaceWithLink(Strings.ActionGift2Received, "un2", giftedPremium);
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, message.GetSender(), giftedPremium);
            }
        }

        private static FormattedText UpdateGiftedStars(MessageWithOwner message, MessageGiftedStars giftedStars, bool history)
        {
            // TODO: markdown

            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", giftedStars);
            }
            else if (message.ClientService.TryGetUser(giftedStars.GifterUserId, out User senderUser))
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, senderUser, giftedStars);
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, Strings.StarsTransactionUnknown, giftedStars);
            }
        }

        private static FormattedText UpdateVideoChatEnded(MessageWithOwner message, MessageVideoChatEnded videoChatEnded, bool history)
        {
            if (message.IsOutgoing)
            {
                return string.Format(Strings.ActionGroupCallEndedByYou, videoChatEnded.GetDuration()).AsFormattedText();
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return ReplaceWithLink(string.Format(Strings.ActionGroupCallEndedBy, videoChatEnded.GetDuration()), senderUser);
            }
            else
            {
                return string.Format(Strings.ActionGroupCallEnded, videoChatEnded.GetDuration()).AsFormattedText();
            }
        }

        private static FormattedText UpdateVideoChatScheduled(MessageWithOwner message, MessageVideoChatScheduled videoChatScheduled, bool history)
        {
            if (message.IsChannelPost)
            {
                return string.Format(Strings.ActionChannelCallScheduled, videoChatScheduled.GetStartsAt()).AsFormattedText();
            }
            else
            {
                return string.Format(Strings.ActionGroupCallScheduled, videoChatScheduled.GetStartsAt()).AsFormattedText();
            }
        }

        private static FormattedText UpdateVideoChatStarted(MessageWithOwner message, MessageVideoChatStarted videoChatStarted, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionChannelCallJustStarted.AsFormattedText();
            }
            else if (message.SenderId.IsUser(message.ClientService.Options.MyId))
            {
                return Strings.ActionGroupCallStartedByYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGroupCallStarted, message.GetSender());
            }
        }

        private static FormattedText UpdateInviteVideoChatParticipants(MessageWithOwner message, MessageInviteVideoChatParticipants inviteVideoChatParticipants, bool history)
        {
            long singleUserId = 0;
            if (inviteVideoChatParticipants.UserIds.Count == 1)
            {
                singleUserId = inviteVideoChatParticipants.UserIds[0];
            }

            if (singleUserId != 0)
            {
                var whoUser = message.ClientService.GetUser(singleUserId);
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionGroupCallYouInvited, "un2", whoUser);
                }
                else if (singleUserId == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(Strings.ActionGroupCallInvitedYou, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionGroupCallInvited, message.GetSender(), whoUser);
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return ReplaceWithLinks(Strings.ActionGroupCallYouInvited, "un2", inviteVideoChatParticipants.UserIds, message.ClientService);
                }
                else
                {
                    var content = ReplaceWithLink(Strings.ActionGroupCallInvited, message.GetSender());
                    return ReplaceWithLinks(content, "un2", inviteVideoChatParticipants.UserIds, message.ClientService);
                }
            }
        }

        private static FormattedText UpdateProximityAlertTriggered(MessageWithOwner message, MessageProximityAlertTriggered proximityAlertTriggered, bool history)
        {
            message.ClientService.TryGetUser(proximityAlertTriggered.TravelerId, out User traveler);
            message.ClientService.TryGetUser(proximityAlertTriggered.WatcherId, out User watcher);

            if (traveler != null && watcher != null)
            {
                if (traveler.Id == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinYouRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), watcher);
                }
                else if (watcher.Id == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), traveler);
                }
                else
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinOtherRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), traveler, watcher);
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateGiveawayCreated(MessageWithOwner message, MessageGiveawayCreated giveawayCreated, bool history)
        {
            if (giveawayCreated.StarCount > 0)
            {
                return Locale.Declension(message.IsChannelPost
                    ? Strings.R.BoostingStarsGiveawayJustStarted
                    : Strings.R.BoostingStarsGiveawayJustStartedGroup, giveawayCreated.StarCount, message.Chat.Title).AsFormattedText();
            }
            else
            {
                return string.Format(message.IsChannelPost
                    ? Strings.BoostingGiveawayJustStarted
                    : Strings.BoostingGiveawayJustStartedGroup, message.Chat.Title).AsFormattedText();
            }
        }

        private static FormattedText UpdateGiveawayCompleted(MessageWithOwner message, MessageGiveawayCompleted giveawayCompleted, bool history)
        {
            var content = Locale.Declension(Strings.R.BoostingGiveawayServiceWinnersSelected, giveawayCompleted.WinnerCount);

            if (giveawayCompleted.UnclaimedPrizeCount > 0)
            {
                content = string.Format("{0} {1}", content, Locale.Declension(Strings.R.BoostingGiveawayServiceUndistributed, giveawayCompleted.UnclaimedPrizeCount));
            }

            return content.AsFormattedText();
        }

        private static FormattedText UpdateGiveawayPrizeStars(MessageWithOwner message, MessageGiveawayPrizeStars giveawayPrizeStars, bool history)
        {
            var boostedChat = message.ClientService.GetChat(giveawayPrizeStars.BoostedChatId);

            var content = Locale.Declension(Strings.R.ActionStarGiveawayPrize, giveawayPrizeStars.StarCount);
            return ReplaceWithLink(content, boostedChat);
        }

        private static FormattedText UpdatePremiumGiftCode(MessageWithOwner message, MessagePremiumGiftCode premiumGiftCode, bool history)
        {
            // TODO: parse markdown
            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", premiumGiftCode);
            }
            else if (message.ChatId == message.ClientService.Options.TelegramServiceNotificationsChatId)
            {
                if (premiumGiftCode.Amount > 0)
                {
                    return ReplaceWithLink(Strings.ActionGift2Received, "un2", premiumGiftCode);
                }
                else
                {
                    return Strings.BoostingReceivedGiftNoName.AsFormattedText();
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, message.GetSender(), premiumGiftCode);
            }
        }

        private static FormattedText UpdateDirectMessagePriceChanged(MessageWithOwner message, MessageDirectMessagePriceChanged directMessagePriceChanged, bool history)
        {
            if (directMessagePriceChanged.IsEnabled)
            {
                if (directMessagePriceChanged.PaidMessageStarCount > 0)
                {
                    return ReplaceWithLink(Locale.Declension(Strings.R.PostSuggestionsPriceUpdated, directMessagePriceChanged.PaidMessageStarCount), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.PostSuggestionsEnabledUpdated, message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.PostSuggestionsDisabledUpdated, message.GetSender());
            }
        }

        private static FormattedText UpdatePaidMessagePriceChanged(MessageWithOwner message, MessagePaidMessagePriceChanged paidMessagePriceChanged, bool history)
        {
            if (message.IsOutgoing)
            {
                return Locale.Declension(Strings.R.PaidMessagesPriceUpdatedOut, paidMessagePriceChanged.PaidMessageStarCount).AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Locale.Declension(Strings.R.PaidMessagesPriceUpdated, paidMessagePriceChanged.PaidMessageStarCount), message.GetSender());
            }
        }

        private static FormattedText UpdatePaidMessagesRefunded(MessageWithOwner message, MessagePaidMessagesRefunded paidMessagesRefunded, bool history)
        {
            if (message.IsOutgoing && message.ClientService.TryGetUser(message.Chat, out User receiverUser))
            {
                var content = Locale.Declension(Strings.R.PaidMessagesRefundedOut, paidMessagesRefunded.StarCount);
                return ReplaceWithLink(content, receiverUser);
            }
            else if (message.ClientService.TryGetMessageSender(message.SenderId, out Object senderUser))
            {
                var content = Locale.Declension(Strings.R.PaidMessagesRefunded, paidMessagesRefunded.StarCount);
                return ReplaceWithLink(content, senderUser);
            }

            return _emptyString;
        }

        private static FormattedText UpdatePassportDataSent(MessageWithOwner message, MessagePassportDataSent passportDataSent, bool history)
        {
            string content;

            StringBuilder str = new();
            for (int a = 0, size = passportDataSent.Types.Count; a < size; a++)
            {
                var type = passportDataSent.Types[a];
                if (str.Length > 0)
                {
                    str.Append(", ");
                }
                if (type is PassportElementTypePhoneNumber)
                {
                    str.Append(Strings.ActionBotDocumentPhone);
                }
                else if (type is PassportElementTypeEmailAddress)
                {
                    str.Append(Strings.ActionBotDocumentEmail);
                }
                else if (type is PassportElementTypeAddress)
                {
                    str.Append(Strings.ActionBotDocumentAddress);
                }
                else if (type is PassportElementTypePersonalDetails)
                {
                    str.Append(Strings.ActionBotDocumentIdentity);
                }
                else if (type is PassportElementTypePassport)
                {
                    str.Append(Strings.ActionBotDocumentPassport);
                }
                else if (type is PassportElementTypeDriverLicense)
                {
                    str.Append(Strings.ActionBotDocumentDriverLicence);
                }
                else if (type is PassportElementTypeIdentityCard)
                {
                    str.Append(Strings.ActionBotDocumentIdentityCard);
                }
                else if (type is PassportElementTypeUtilityBill)
                {
                    str.Append(Strings.ActionBotDocumentUtilityBill);
                }
                else if (type is PassportElementTypeBankStatement)
                {
                    str.Append(Strings.ActionBotDocumentBankStatement);
                }
                else if (type is PassportElementTypeRentalAgreement)
                {
                    str.Append(Strings.ActionBotDocumentRentalAgreement);
                }
                else if (type is PassportElementTypeInternalPassport)
                {
                    str.Append(Strings.ActionBotDocumentInternalPassport);
                }
                else if (type is PassportElementTypePassportRegistration)
                {
                    str.Append(Strings.ActionBotDocumentPassportRegistration);
                }
                else if (type is PassportElementTypeTemporaryRegistration)
                {
                    str.Append(Strings.ActionBotDocumentTemporaryRegistration);
                }
            }

            var chat = message.Chat;
            content = string.Format(Strings.ActionBotDocuments, chat?.Title ?? string.Empty, str.ToString());

            return content.AsFormattedText();
        }

        private static FormattedText UpdatePaymentSuccessful(MessageWithOwner message, MessagePaymentSuccessful paymentSuccessful, bool history)
        {
            var invoice = GetInvoice(message as MessageViewModel);
            var chat = message.Chat;

            if (invoice != null)
            {
                return string.Format(Strings.PaymentSuccessfullyPaid, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat), invoice.ProductInfo.Title).AsFormattedText();
            }
            else
            {
                return string.Format(Strings.PaymentSuccessfullyPaidNoItem, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat)).AsFormattedText();
            }
        }

        private static FormattedText UpdatePaymentRefunded(MessageWithOwner message, MessagePaymentRefunded paymentRefunded, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionRefunded, Locale.FormatCurrency(paymentRefunded.TotalAmount, paymentRefunded.Currency)), message.GetSender());
        }

        private static FormattedText UpdatePinMessage(MessageWithOwner message, MessagePinMessage pinMessage, bool history)
        {
            if (message is MessageViewModel { ReplyToItem: MessageViewModel reply })
            {
                if (reply.Content is MessageAnimatedEmoji animatedEmoji)
                {
                    if (animatedEmoji.AnimatedEmoji.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        var emoji = new FormattedText(animatedEmoji.Emoji, new[] { new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId)) });
                        return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, emoji), message.GetSender());
                    }

                    return ReplaceWithLink(string.Format(Strings.ActionPinnedText, animatedEmoji.Emoji), message.GetSender());
                }
                else if (reply.Content is MessageAudio)
                {
                    return ReplaceWithLink(Strings.ActionPinnedMusic, message.GetSender());
                }
                else if (reply.Content is MessageVideo)
                {
                    return ReplaceWithLink(Strings.ActionPinnedVideo, message.GetSender());
                }
                else if (reply.Content is MessageAnimation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGif, message.GetSender());
                }
                else if (reply.Content is MessageVoiceNote)
                {
                    return ReplaceWithLink(Strings.ActionPinnedVoice, message.GetSender());
                }
                else if (reply.Content is MessageVideoNote)
                {
                    return ReplaceWithLink(Strings.ActionPinnedRound, message.GetSender());
                }
                else if (reply.Content is MessageSticker)
                {
                    return ReplaceWithLink(Strings.ActionPinnedSticker, message.GetSender());
                }
                else if (reply.Content is MessageDocument)
                {
                    return ReplaceWithLink(Strings.ActionPinnedFile, message.GetSender());
                }
                else if (reply.Content is MessageLiveLocation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeoLive, message.GetSender());
                }
                else if (reply.Content is MessageLocation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeo, message.GetSender());
                }
                else if (reply.Content is MessageVenue)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeo, message.GetSender());
                }
                else if (reply.Content is MessageContact)
                {
                    return ReplaceWithLink(Strings.ActionPinnedContact, message.GetSender());
                }
                else if (reply.Content is MessagePhoto)
                {
                    return ReplaceWithLink(Strings.ActionPinnedPhoto, message.GetSender());
                }
                else if (reply.Content is MessagePoll poll)
                {
                    if (poll.Poll.Type is PollTypeRegular)
                    {
                        return ReplaceWithLink(Strings.ActionPinnedPoll, message.GetSender());
                    }
                    else if (poll.Poll.Type is PollTypeQuiz)
                    {
                        return ReplaceWithLink(Strings.ActionPinnedQuiz, message.GetSender());
                    }
                }
                else if (reply.Content is MessageGame game)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionPinnedGame, "\uD83C\uDFAE " + game.Game.Title), message.GetSender());
                }
                else if (reply.Content is MessageRichMessage richMessage)
                {
                    var mess = richMessage.Message.ToFormattedText().Replace("\n", " ");
                    if (mess.Text.Length > 20)
                    {
                        mess = TdExtensions.Concat(mess.Substring(0, 20), "...".AsFormattedText());
                    }

                    return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, mess), message.GetSender());
                }
                else if (reply.Content is MessageText text)
                {
                    var mess = text.Text.Clone().Replace("\n", " ");
                    if (mess.Text.Length > 20)
                    {
                        mess = TdExtensions.Concat(mess.Substring(0, 20), "...".AsFormattedText());
                    }

                    return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, mess), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionPinnedNoText, message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionPinnedNoText, message.GetSender());
            }

            return _emptyString;
        }

        private static FormattedText UpdateScreenshotTaken(MessageWithOwner message, MessageScreenshotTaken screenshotTaken, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionTakeScreenshootYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionTakeScreenshoot, message.GetSender());
            }
        }

        private static FormattedText UpdateSuggestBirthdate(MessageWithOwner message, MessageSuggestBirthdate suggestBirthdate, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionYouSuggestBirthday.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionSuggestBirthday, message.GetSender());
            }
        }

        private static FormattedText UpdateSuggestProfilePhoto(MessageWithOwner message, MessageSuggestProfilePhoto suggestProfilePhoto, bool history)
        {
            string format;
            User user;

            if (message.IsOutgoing)
            {
                format = Strings.ActionSuggestPhotoFromYouDescription;
                message.ClientService.TryGetUser(message.Chat, out user);
            }
            else
            {
                format = Strings.ActionSuggestPhotoToYouDescription;
                message.ClientService.TryGetUser(message.SenderId, out user);
            }

            if (user == null)
            {
                return _emptyString;
            }

            // A translation that dropped the placeholder would put the entity at -1.
            var index = format.IndexOf("{0}");
            var content = string.Format(format, user.FirstName);

            return index < 0
                ? content.AsFormattedText()
                : new FormattedText(content, new[] { new TextEntity(index, user.FirstName.Length, new TextEntityTypeBold()) });
        }

        private static FormattedText UpdateSupergroupChatCreate(MessageWithOwner message, MessageSupergroupChatCreate supergroupChatCreate, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionCreateChannel.AsFormattedText();
            }
            else
            {
                return Strings.ActionCreateMega.AsFormattedText();
            }
        }

        private static FormattedText UpdateUpgradedGift(MessageWithOwner message, MessageUpgradedGift upgradedGift, bool history)
        {
            if (upgradedGift.Origin is UpgradedGiftOriginUpgrade)
            {
                if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId))
                {
                    if (!upgradedGift.ReceiverId.AreTheSame(upgradedGift.SenderId) && message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object outboundUser))
                    {
                        return ReplaceWithLink(Strings.ActionUniqueGiftUpgradeOutbound, outboundUser);
                    }
                    else
                    {
                        return Strings.ActionUniqueGiftUpgradeSelf.AsFormattedText();
                    }
                }
                else if (message.ClientService.TryGetMessageSender(upgradedGift.ReceiverId, out Object inboundUser))
                {
                    return ReplaceWithLink(Strings.ActionUniqueGiftUpgradeInbound, inboundUser);
                }
            }
            else if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId))
            {
                if (message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object inboundUser))
                {
                    return ReplaceWithLink(Strings.ActionUniqueGiftTransferInbound, inboundUser);
                }
            }
            else if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionUniqueGiftTransferOutbound, message.ClientService.GetMessageSender(upgradedGift.ReceiverId));
            }
            else if (message.ClientService.TryGetMessageSender(upgradedGift.ReceiverId, out Object outboundUser)
                && message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object inboundUser))
            {
                return ReplaceWithLink(Strings.ActionUniqueGiftTransferService, inboundUser, outboundUser);
            }

            return _emptyString;
        }

        private static FormattedText UpdateUpgradedGiftPurchaseOffer(MessageWithOwner message, MessageUpgradedGiftPurchaseOffer upgradedGift, bool history)
        {
            if (!message.ClientService.TryGetUser(message.Chat, out User user))
            {
                return _emptyString;
            }

            var content = string.Empty;

            if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(Strings.GiftOfferOfferedTextStarsOut, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName());
                }
                else
                {
                    content = string.Format(Strings.GiftOfferOfferedTextStars, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName());
                }
            }
            else if (upgradedGift.Price is GiftResalePriceGram resalePriceGram)
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(Strings.GiftOfferOfferedTextTONOut, user.FullName(true), resalePriceGram.GramCentCount, upgradedGift.Gift.ToName());
                }
                else
                {
                    content = string.Format(Strings.GiftOfferOfferedTextTON, user.FullName(true), resalePriceGram.GramCentCount, upgradedGift.Gift.ToName());
                }
            }

            if (history)
            {
                if (upgradedGift.State is GiftPurchaseOfferStatePending)
                {
                    var now = DateTime.Now.ToUnixTimeSeconds();
                    if (now >= upgradedGift.ExpirationDate)
                    {
                        content += "\n\n" + Strings.GiftOfferStatusExpired;
                    }
                    else
                    {
                        content += "\n\n" + string.Format(Strings.GiftOfferStatusPending, Formatter.ShortDuration(upgradedGift.ExpirationDate - now));
                    }
                }
                else if (upgradedGift.State is GiftPurchaseOfferStateAccepted)
                {
                    content += "\n\n" + Strings.GiftOfferStatusAccepted;
                }
                else if (upgradedGift.State is GiftPurchaseOfferStateRejected)
                {
                    content += "\n\n" + Strings.GiftOfferStatusRejected;
                }
            }

            return ClientEx.ParseMarkdown(content);
        }

        private static FormattedText UpdateUpgradedGiftPurchaseOfferRejected(MessageWithOwner message, MessageUpgradedGiftPurchaseOfferRejected upgradedGift, bool history)
        {
            if (!message.ClientService.TryGetUser(message.Chat, out User user))
            {
                return _emptyString;
            }

            if (upgradedGift.WasExpired)
            {
                if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceStar.StarCount.ToString("N0")));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejected, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName()));
                    }
                }
                else if (upgradedGift.Price is GiftResalePriceGram resalePriceGram)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceGram.GramCentCount));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejected, user.FullName(true), resalePriceGram.GramCentCount, upgradedGift.Gift.ToName()));
                    }
                }
            }
            else
            {
                if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceStar.StarCount.ToString("N0")));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejected, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName()));
                    }
                }
                else if (upgradedGift.Price is GiftResalePriceGram resalePriceGram)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceGram.GramCentCount));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejected, user.FullName(true), resalePriceGram.GramCentCount, upgradedGift.Gift.ToName()));
                    }
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatShared(MessageWithOwner message, MessageChatShared chatShared, bool history)
        {
            var chat = message.Chat;
            if (chat != null && message.ClientService.TryGetChat(chatShared.Chat.ChatId, out Chat sharedChat))
            {
                if (message.ClientService.TryGetSupergroup(sharedChat, out Supergroup supergroup) && supergroup.IsChannel)
                {
                    return ReplaceWithLink(Strings.ActionRequestedPeerChannel, "un2", chat);
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionRequestedPeerChat, "un2", chat);
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateUsersShared(MessageWithOwner message, MessageUsersShared usersShared, bool history)
        {
            var chat = message.Chat;
            if (chat == null)
            {
                return _emptyString;
            }

            // Without a user to name, ActionRequestedPeer would read "You shared  with un2".
            if (usersShared.Users.Count == 0)
            {
                return ReplaceWithLink(Strings.ActionRequestedPeerUser, "un2", chat);
            }

            var content = ReplaceWithLinks(Strings.ActionRequestedPeer, "un1", usersShared.Users.Select(x => x.UserId), message.ClientService);
            return ReplaceWithLink(content, "un2", chat);
        }

        private static FormattedText UpdateWebAppDataSent(MessageWithOwner message, MessageWebAppDataSent webAppDataSent, bool history)
        {
            return string.Format(Strings.ActionBotWebViewData, webAppDataSent.ButtonText).AsFormattedText();
        }

        private static FormattedText UpdateExpiredPhoto(MessageWithOwner message, MessageExpiredPhoto expiredPhoto, bool history)
        {
            return Strings.AttachPhotoExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVideo(MessageWithOwner message, MessageExpiredVideo expiredVideo, bool history)
        {
            return Strings.AttachVideoExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVideoNote(MessageWithOwner message, MessageExpiredVideoNote expiredVideoNote, bool history)
        {
            return Strings.AttachRoundExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVoiceNote(MessageWithOwner message, MessageExpiredVoiceNote expiredVoiceNote, bool history)
        {
            return Strings.AttachVoiceExpired.AsFormattedText();
        }

        private static FormattedText UpdateChecklistTasksAdded(MessageWithOwner message, MessageChecklistTasksAdded checklistTasksAdded, bool history)
        {
            if (checklistTasksAdded.Tasks.Count == 0)
            {
                return _emptyString;
            }

            Checklist checklist = null;
            if (message is MessageViewModel { ReplyToItem: MessageViewModel { Content: MessageChecklist checklistContent } })
            {
                checklist = checklistContent.List;
            }

            FormattedText formatted;
            if (checklist == null)
            {
                if (checklistTasksAdded.Tasks.Count > 1)
                {
                    var text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoAddedTasksOutUnknown, checklistTasksAdded.Tasks.Count)
                        : Locale.Declension(Strings.R.TodoAddedTasksUnknown, checklistTasksAdded.Tasks.Count);
                    formatted = text.AsFormattedText();
                }
                else
                {
                    var text = message.IsOutgoing
                        ? Strings.TodoAddedTaskOutUnknown
                        : Strings.TodoAddedTaskUnknown;
                    formatted = ClientEx.Format(text, checklistTasksAdded.Tasks[0].Text);
                }
            }
            else if (checklistTasksAdded.Tasks.Count > 1)
            {
                var text = message.IsOutgoing
                    ? Locale.Declension(Strings.R.TodoAddedTasksOut, checklistTasksAdded.Tasks.Count, "{0}")
                    : Locale.Declension(Strings.R.TodoAddedTasks, checklistTasksAdded.Tasks.Count, "{0}");
                formatted = ClientEx.Format(text, checklist.Title);
            }
            else
            {
                var text = message.IsOutgoing
                    ? Strings.TodoAddedTaskOut
                    : Strings.TodoAddedTask;
                formatted = ClientEx.Format(text, checklistTasksAdded.Tasks[0].Text, checklist.Title);
            }

            formatted = ClientEx.ParseMarkdown(formatted);
            formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdateChecklistTasksDone(MessageWithOwner message, MessageChecklistTasksDone checklistTasksDone, bool history)
        {
            var markedAsDone = checklistTasksDone.MarkedAsDoneTaskIds.Count > 0;
            var taskIds = markedAsDone
                ? checklistTasksDone.MarkedAsDoneTaskIds
                : checklistTasksDone.MarkedAsNotDoneTaskIds;

            if (taskIds.Count == 0)
            {
                return _emptyString;
            }

            var taskId = taskIds[0];

            ChecklistTask task = null;
            if (message is MessageViewModel { ReplyToItem: MessageViewModel { Content: MessageChecklist checklist } })
            {
                foreach (var item in checklist.List.Tasks)
                {
                    if (item.Id == taskId)
                    {
                        task = item;
                        break;
                    }
                }
            }

            if (task == null || taskIds.Count > 1)
            {
                string text;
                if (markedAsDone)
                {
                    text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoTasksCompletedOut, checklistTasksDone.MarkedAsDoneTaskIds.Count)
                        : Locale.Declension(Strings.R.TodoTasksCompleted, checklistTasksDone.MarkedAsDoneTaskIds.Count);
                }
                else
                {
                    text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoTasksNotCompletedOut, checklistTasksDone.MarkedAsNotDoneTaskIds.Count)
                        : Locale.Declension(Strings.R.TodoTasksNotCompleted, checklistTasksDone.MarkedAsNotDoneTaskIds.Count);
                }

                var formatted = ClientEx.ParseMarkdown(text);
                formatted = TdExtensions.Concat(ClientEx.CustomEmoji(markedAsDone ? "\uEAD3 " : "\uEAD4 "), formatted);

                return ReplaceWithLink(formatted, message.GetSender());
            }
            else
            {
                string text;
                if (markedAsDone)
                {
                    text = message.IsOutgoing
                        ? Strings.TodoTaskCompletedOut
                        : Strings.TodoTaskCompleted;
                }
                else
                {
                    text = message.IsOutgoing
                        ? Strings.TodoTaskNotCompletedOut
                        : Strings.TodoTaskNotCompleted;
                }

                var formatted = ClientEx.Format(text, task.Text);
                formatted = ClientEx.ParseMarkdown(formatted);
                formatted = TdExtensions.Concat(ClientEx.CustomEmoji(markedAsDone ? "\uEAD3 " : "\uEAD4 "), formatted);

                return ReplaceWithLink(formatted, message.GetSender());
            }
        }
        private static FormattedText UpdatePollOptionAdded(MessageWithOwner message, MessagePollOptionAdded pollOptionAdded, bool history)
        {
            FormattedText formatted;
            var text = message.IsOutgoing
                ? Strings.PollAddingActionYou
                : Strings.PollAddingActionOther;
            formatted = ClientEx.Format(text, pollOptionAdded.Text);
            formatted = ClientEx.ParseMarkdown(formatted);
            //formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdatePollOptionDeleted(MessageWithOwner message, MessagePollOptionDeleted pollOptionDeleted, bool history)
        {
            FormattedText formatted;
            var text = message.IsOutgoing
                ? Strings.PollRemovedActionYou
                : Strings.PollRemovedActionOther;
            formatted = ClientEx.Format(text, pollOptionDeleted.Text);
            formatted = ClientEx.ParseMarkdown(formatted);
            //formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdateSuggestedPostPaid(MessageWithOwner message, MessageSuggestedPostPaid suggestedPostPaid, bool history)
        {
            var sender = message.ClientService.GetTitle(message.SenderId);

            if (suggestedPostPaid.StarAmount.IsPositive())
            {
                return string.Format(Strings.SuggestedOfferCompleteAmountF.ReplaceStar(Icons.Premium), sender, suggestedPostPaid.StarAmount.ToValue()).AsFormattedText();
            }
            else if (suggestedPostPaid.GramAmount > 0)
            {
                return string.Format(Strings.SuggestedOfferCompleteAmountF.ReplaceStar(Icons.Ton), sender, suggestedPostPaid.GramAmount / Constants.ToncoinMin).AsFormattedText();
            }

            return string.Format(Strings.SuggestedOfferCompleteAmountUnknown, sender).AsFormattedText();
        }

        private static FormattedText UpdateSuggestedPostRefunded(MessageWithOwner message, MessageSuggestedPostRefunded suggestedPostRefunded, bool history)
        {
            var sender = message.ClientService.GetTitle(message.SenderId);

            if (suggestedPostRefunded.Reason is SuggestedPostRefundReasonPostDeleted)
            {
                if (message is MessageViewModel { ReplyToItem: MessageViewModel { SuggestedPostInfo: not null } replyTo })
                {
                    if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceStar priceStar)
                    {
                        return string.Format(Strings.SuggestedOfferRefundByAdminAmountF.ReplaceStar(Icons.Premium), sender, message.Chat.Title, priceStar.StarCount).AsFormattedText();
                    }
                    else if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceGram priceGram)
                    {
                        return string.Format(Strings.SuggestedOfferRefundByAdminAmountF.ReplaceStar(Icons.Ton), sender, message.Chat.Title, priceGram.GramCentCount).AsFormattedText();
                    }
                }
                else
                {
                    return string.Format(Strings.SuggestedOfferRefundByAdminAmountUnknown, sender, message.Chat.Title).AsFormattedText();
                }
            }
            else if (message is MessageViewModel { ReplyToItem: MessageViewModel { SuggestedPostInfo: not null } replyTo })
            {
                if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceStar priceStar)
                {
                    return string.Format(Strings.SuggestedOfferRefundByUserAmountF.ReplaceStar(Icons.Premium), sender, message.Chat.Title, priceStar.StarCount).AsFormattedText();
                }
                else if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceGram priceGram)
                {
                    return string.Format(Strings.SuggestedOfferRefundByUserAmountF.ReplaceStar(Icons.Ton), sender, message.Chat.Title, priceGram.GramCentCount).AsFormattedText();
                }
            }
            else
            {
                return string.Format(Strings.SuggestedOfferRefundByUserAmountUnknown, sender, message.Chat.Title).AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatBoost(MessageWithOwner message, MessageChatBoost chatBoost, bool history)
        {
            var content = string.Empty;

            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                content = user.FullName(true);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
            {
                content = chat.Title;
            }

            if (message.IsChannelPost)
            {
                if (chatBoost.BoostCount > 1)
                {
                    return Locale.Declension(message.IsOutgoing ? Strings.R.BoostingBoostsChannelByYouServiceMsgCount : Strings.R.BoostingBoostsChannelByUserServiceMsgCount, chatBoost.BoostCount, content).AsFormattedText();
                }
                else
                {
                    return string.Format(message.IsOutgoing ? Strings.BoostingBoostsChannelByYouServiceMsg : Strings.BoostingBoostsChannelByUserServiceMsg, content).AsFormattedText();
                }
            }
            else
            {
                if (chatBoost.BoostCount > 1)
                {
                    return Locale.Declension(message.IsOutgoing ? Strings.R.BoostingBoostsGroupByYouServiceMsgCount : Strings.R.BoostingBoostsGroupByUserServiceMsgCount, chatBoost.BoostCount, content).AsFormattedText();
                }
                else
                {
                    return string.Format(message.IsOutgoing ? Strings.BoostingBoostsGroupByYouServiceMsg : Strings.BoostingBoostsGroupByUserServiceMsg, content).AsFormattedText();
                }
            }
        }

        private static FormattedText UpdateStory(MessageWithOwner message, MessageAsyncStory story, bool history)
        {
            string content = string.Empty;

            if (message.ClientService.TryGetUser(message.Chat, out User user))
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(story.State == MessageStoryState.Expired ? Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStoryMentioned : Strings.StoryYouMentionedTitle, user.FullName(true));
                }
                else
                {
                    content = string.Format(story.State == MessageStoryState.Expired ? Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStoryMention : Strings.StoryMentionedTitle, user.FullName(true));
                }
            }

            return ClientEx.ParseMarkdown(content);
        }

        private readonly static FormattedText _emptyString = new(string.Empty, Array.Empty<TextEntity>());

        private static FormattedText UpdateStory(MessageWithOwner message, MessageStory story, bool history)
        {
            if (message.IsOutgoing)
            {
                if (message.ClientService.TryGetUser(message.Chat, out User user))
                {
                    return string.Format(Strings.StoryYouMentionInDialog, user.FullName(true)).AsFormattedText();
                }
            }
            else
            {
                return Strings.StoryMentionInDialog.AsFormattedText();
            }

            return _emptyString;
        }

        public static FormattedText ReplaceWithLink(string source, params object[] args)
        {
            return BuildWithLink(new FormattedTextBuilder(source), args).ToFormattedText();
        }

        public static FormattedText ReplaceWithLink(FormattedText source, params object[] args)
        {
            return BuildWithLink(new FormattedTextBuilder(source), args).ToFormattedText();
        }

        /// <summary>
        /// Substitutes un1, un2 ... with the args, handing back the builder rather than the result
        /// so that a caller with more to add can do it before the entities are frozen - see
        /// UpdateForumTopicEdited.
        /// </summary>
        private static FormattedTextBuilder BuildWithLink(FormattedTextBuilder builder, params object[] args)
        {
            builder.Strip("**");

            for (int i = 0; i < args.Length; i++)
            {
                var obj = args[i];
                var param = "un" + (i + 1);

                // Tested before the name is resolved: that can cost a declension or a currency
                // format, and a placeholder the string does not carry is the common case.
                if (builder.IndexOf(param) >= 0)
                {
                    String name;
                    TextEntityType id = null;
                    if (obj is User user)
                    {
                        name = user.FullName();
                        id = new TextEntityTypeMentionName(user.Id);
                    }
                    else if (obj is Chat chat)
                    {
                        name = chat.Title;
                    }
                    else if (obj is Community community)
                    {
                        name = community.Name;
                    }
                    else if (obj is Game game)
                    {
                        name = game.Title;
                    }
                    else if (obj is MessageGift gift)
                    {
                        name = Locale.Declension(Strings.R.StarsCount, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount);
                    }
                    else if (obj is MessageGiftedPremium giftedPremium)
                    {
                        name = Locale.FormatCurrency(giftedPremium.Amount, giftedPremium.Currency);
                    }
                    else if (obj is MessagePremiumGiftCode premiumGiftCode)
                    {
                        name = Locale.FormatCurrency(premiumGiftCode.Amount, premiumGiftCode.Currency);
                    }
                    else if (obj is MessageGiftedStars giftedStars)
                    {
                        name = Locale.FormatCurrency(giftedStars.Amount, giftedStars.Currency);
                    }
                    else if (obj is ForumTopicInfo forumTopicInfo)
                    {
                        name = $"\U0001F4C3 {forumTopicInfo.Name}";

                        // TODO: build text url
                        id = new TextEntityTypeTextUrl("tg-topic://");
                    }
                    else if (obj is string value)
                    {
                        name = value;
                        id = null;
                    }
                    else
                    {
                        name = "";
                        id = null;
                    }

                    builder.Substitute(param, name, id);
                }
            }

            return builder;
        }

        public static FormattedText ReplaceWithName(string source, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var obj = args[i];
                if (obj is User user)
                {
                    args[i] = user.FullName();
                }
                else if (obj is Chat chat)
                {
                    args[i] = chat.Title;
                }
                else if (obj is Game game)
                {
                    args[i] = game.Title;
                }
                else if (obj is MessageGift gift)
                {
                    args[i] = Locale.Declension(Strings.R.StarsCount, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount);
                }
                else if (obj is MessageGiftedPremium giftedPremium)
                {
                    args[i] = Locale.FormatCurrency(giftedPremium.Amount, giftedPremium.Currency);
                }
                else if (obj is MessagePremiumGiftCode premiumGiftCode)
                {
                    args[i] = Locale.FormatCurrency(premiumGiftCode.Amount, premiumGiftCode.Currency);
                }
                else if (obj is MessageGiftedStars giftedStars)
                {
                    args[i] = Locale.FormatCurrency(giftedStars.Amount, giftedStars.Currency);
                }
                else if (obj is ForumTopicInfo forumTopicInfo)
                {
                    args[i] = $"\U0001F4C3 {forumTopicInfo.Name}";
                }
            }

            return string.Format(source, args).AsFormattedText();
        }

        private static FormattedText ReplaceWithLinks(string source, string param, IEnumerable<long> uids, IClientService clientService)
        {
            return ReplaceWithLinks(new FormattedTextBuilder(source), param, uids, clientService);
        }

        private static FormattedText ReplaceWithLinks(FormattedText source, string param, IEnumerable<long> uids, IClientService clientService)
        {
            return ReplaceWithLinks(new FormattedTextBuilder(source), param, uids, clientService);
        }

        private static FormattedText ReplaceWithLinks(FormattedTextBuilder builder, string param, IEnumerable<long> uids, IClientService clientService)
        {
            if (builder.IndexOf(param) < 0)
            {
                return builder.ToFormattedText();
            }

            var names = new StringBuilder();
            var entities = new MutableVector<TextEntity>();

            foreach (var user in clientService.GetUsers(uids))
            {
                var name = user.FullName();
                if (names.Length != 0)
                {
                    names.Append(", ");
                }

                // Relative to the joined names; Substitute moves them to where it lands.
                entities.Add(new TextEntity(names.Length, name.Length, new TextEntityTypeMentionName(user.Id)));
                names.Append(name);
            }

            builder.Substitute(param, new FormattedText(names.ToString(), entities));
            return builder.ToFormattedText();
        }

        private static Game GetGame(MessageViewModel message)
        {
            var reply = message?.ReplyToItem as MessageViewModel;
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
            var reply = message?.ReplyToItem as MessageViewModel;
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
