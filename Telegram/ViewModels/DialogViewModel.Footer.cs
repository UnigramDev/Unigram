//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.Views.Popups;

namespace Telegram.ViewModels
{
    public enum ChatFooterKind
    {
        Composer,
        Button,
        AccentButton,
        StartButton,
        Info,
        FormattedInfo,
        Link,
        Progress
    }

    public enum ChatFooterAction
    {
        None,
        Frozen,
        ToggleSavedTag,
        OpenSavedChat,
        Unpin,
        DeleteChat,
        ToggleMute,
        Unblock,
        Start,
        OpenSupergroup,
        Join,
        Boost,
        EventLogFilters
    }

    public sealed record ChatFooterState(ChatFooterKind Kind, ChatFooterAction Action, string Text, bool ReplyRestoresComposer = false, long PaidMessageStarCount = 0)
    {
        public static ChatFooterState Composer(long paidMessageStarCount)
        {
            return new ChatFooterState(ChatFooterKind.Composer, ChatFooterAction.None, null, PaidMessageStarCount: paidMessageStarCount);
        }

        public static ChatFooterState Button(ChatFooterAction action, string text)
        {
            return new ChatFooterState(ChatFooterKind.Button, action, text);
        }

        public static ChatFooterState Info(string text, bool replyRestoresComposer = false)
        {
            return new ChatFooterState(ChatFooterKind.Info, ChatFooterAction.None, text, replyRestoresComposer);
        }
    }

    public partial class DialogViewModel
    {
        private CanSendMessageToUserResult _canSendMessageToUser;

        private ChatFooterState _footer;
        private bool _footerEditing;
        private bool _joiningChat;
        private bool _joinRequestSent;
        private int _businessMessageCount;
        private ForumTopic _forumGeneralTopic;

        // For the triggers that carry none of the chat's objects. Where a handler already holds them
        // it passes them to an overload below, so each cache read takes ClientService's lock once.
        public void UpdateFooter()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            switch (chat.Type)
            {
                case ChatTypePrivate privata:
                    UpdateFooter(chat, ClientService.GetUser(privata.UserId), ClientService.GetUserFull(privata.UserId), null);
                    break;
                case ChatTypeSecret secret:
                    UpdateFooter(chat, ClientService.GetUser(secret.UserId), ClientService.GetUserFull(secret.UserId), ClientService.GetSecretChat(secret.SecretChatId));
                    break;
                case ChatTypeBasicGroup basic:
                    UpdateFooter(chat, ClientService.GetBasicGroup(basic.BasicGroupId));
                    break;
                case ChatTypeSupergroup super:
                    UpdateFooter(chat, ClientService.GetSupergroup(super.SupergroupId), ClientService.GetSupergroupFull(super.SupergroupId));
                    break;
            }
        }

        public void UpdateFooter(Chat chat, User user, UserFullInfo fullInfo, SecretChat secretChat)
        {
            if (secretChat?.State is SecretChatStateClosed && chat.DraftMessage != null)
            {
                ClientService.Send(new SetChatDraftMessage(chat.Id, null, null));
            }

            ApplyFooter(chat, ResolveCommonFooter(user) ?? ResolveUserFooter(chat, user, fullInfo, secretChat));
        }

        public void UpdateFooter(Chat chat, BasicGroup group)
        {
            ApplyFooter(chat, ResolveCommonFooter(null) ?? ResolveBasicGroupFooter(chat, group));
        }

        public void UpdateFooter(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ApplyFooter(chat, ResolveCommonFooter(null) ?? ResolveSupergroupFooter(chat, group, fullInfo));
        }

        private void ApplyFooter(Chat chat, ChatFooterState state)
        {
            _footer = state;
            Delegate?.UpdateFooter(chat, state);
        }

        private void UpdateFooterEditing(MessageComposerHeader header)
        {
            var editing = header?.Editing != null;
            if (editing != _footerEditing)
            {
                _footerEditing = editing;
                UpdateFooter();
            }
        }

        protected void UpdateBusinessMessageCount(int count)
        {
            if (count != _businessMessageCount)
            {
                _businessMessageCount = count;
                UpdateFooter();
            }
        }

        private bool IsEditing => _composerHeader?.Editing != null;

        // Editing a message brings the composer back over a button, but not over the restriction
        // and secret chat texts, nor over the bot start, saved chat and link states.
        private ChatFooterState Bar(ChatFooterState state)
        {
            return IsEditing ? ChatFooterState.Composer(0) : state;
        }

        private ChatFooterState ResolveCommonFooter(User user)
        {
            if (Type == DialogType.ScheduledMessages)
            {
                return ChatFooterState.Composer(0);
            }
            else if (Type is DialogType.History or DialogType.Thread && ClientService.FreezeState.IsFrozen && !IsFreezeAppealBot(user))
            {
                return new ChatFooterState(ChatFooterKind.Link, ChatFooterAction.Frozen, null);
            }
            else if (IsEditing)
            {
                return null;
            }
            else if (Type == DialogType.BusinessReplies)
            {
                var max = ClientService.Options.QuickReplyShortcutMessageCountMax;
                if (max > 0 && _businessMessageCount >= max)
                {
                    return new ChatFooterState(ChatFooterKind.FormattedInfo, ChatFooterAction.None, Locale.Declension(Strings.R.BusinessRepliesLimit, max));
                }
            }
            else if (Type == DialogType.WelcomeMessages)
            {
                var max = ClientService.Options.WelcomeMessageCountMax;
                if (max > 0 && _businessMessageCount >= max)
                {
                    return new ChatFooterState(ChatFooterKind.FormattedInfo, ChatFooterAction.None, Locale.Declension(Strings.R.WelcomeMessagesLimit, max));
                }
            }

            return null;
        }

        // The bot named by the appeal link stays writable, so that a frozen account can appeal.
        private bool IsFreezeAppealBot(User user)
        {
            var username = user?.ActiveUsername();
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            var link = ClientService.FreezeState.AppealLink ?? string.Empty;
            var index = link.IndexOf("t.me/", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var start = index + 5;
            var end = start;

            while (end < link.Length && link[end] < 128 && char.IsLetterOrDigit(link[end]))
            {
                end++;
            }

            return end - start == username.Length && string.Compare(link, start, username, 0, username.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private ChatFooterState ResolveUserFooter(Chat chat, User user, UserFullInfo fullInfo, SecretChat secretChat)
        {
            if (user == null)
            {
                return ChatFooterState.Composer(0);
            }

            var paidMessageStarCount = fullInfo?.OutgoingPaidMessageStarCount ?? user.PaidMessageStarCount;

            if (Search?.SavedMessagesTag != null)
            {
                return ChatFooterState.Button(ChatFooterAction.ToggleSavedTag, Search.FilterByTag ? Strings.SavedTagShowOtherMessages : Strings.SavedTagHideOtherMessages);
            }
            else if (Type is DialogType.History or DialogType.Thread && user.Id != ClientService.Options.MyId && user.RestrictsNewChats && _canSendMessageToUser is CanSendMessageToUserResultUserRestrictsNewChats)
            {
                return new ChatFooterState(ChatFooterKind.Link, ChatFooterAction.None, string.Format(Strings.OnlyPremiumCanMessage, user.FirstName));
            }
            else if (SavedMessagesTopic != null)
            {
                if (SavedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    return ChatFooterState.Info(Strings.AuthorHiddenDescription);
                }
                else if (SavedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    return ChatFooterState.Button(ChatFooterAction.OpenSavedChat, savedChat.Type switch
                    {
                        ChatTypePrivate => Strings.SavedOpenChat,
                        ChatTypeSupergroup { IsChannel: true } => Strings.SavedOpenChannel,
                        _ => Strings.SavedOpenGroup
                    });
                }

                return ChatFooterState.Composer(paidMessageStarCount);
            }
            else if (Type == DialogType.Pinned)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.Unpin, Strings.UnpinAllMessages));
            }
            else if (chat.Id == ClientService.Options.VerificationCodesBotChatId)
            {
                return ChatFooterState.Button(ChatFooterAction.ToggleMute, ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute);
            }
            else if (chat.BlockList is BlockListMain)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.Unblock, user.Type is UserTypeBot ? Strings.BotUnblock : Strings.Unblock));
            }
            else if (chat.Id == ClientService.Options.RepliesBotChatId)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.ToggleMute, ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute));
            }
            else if (user.Type is UserTypeBot && (_accessToken != null || chat.LastMessage == null))
            {
                return new ChatFooterState(ChatFooterKind.StartButton, ChatFooterAction.Start, Strings.BotStart);
            }
            else if (user.Type is UserTypeDeleted)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisChat));
            }
            else if (secretChat != null)
            {
                return secretChat.State switch
                {
                    SecretChatStatePending when secretChat.IsOutbound => new ChatFooterState(ChatFooterKind.FormattedInfo, ChatFooterAction.None, string.Format(Strings.AwaitingEncryption, "**" + user.FirstName + "**")),
                    SecretChatStatePending => ChatFooterState.Info(Strings.EncryptionProcessing),
                    SecretChatStateClosed => ChatFooterState.Info(Strings.EncryptionRejected),
                    _ => ChatFooterState.Composer(0)
                };
            }
            else if (user.RestrictsNewChats)
            {
                return _canSendMessageToUser switch
                {
                    CanSendMessageToUserResultOk => ChatFooterState.Composer(0),
                    CanSendMessageToUserResultUserHasPaidMessages userHasPaidMessages => ChatFooterState.Composer(userHasPaidMessages.OutgoingPaidMessageStarCount),
                    CanSendMessageToUserResultUserIsDeleted => Bar(ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisChat)),
                    _ => ChatFooterState.Composer(paidMessageStarCount)
                };
            }

            return ChatFooterState.Composer(paidMessageStarCount);
        }

        private ChatFooterState ResolveBasicGroupFooter(Chat chat, BasicGroup group)
        {
            if (group == null)
            {
                return ChatFooterState.Composer(0);
            }
            else if (Type == DialogType.Pinned)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.Unpin, CanPinMessages(chat, group.Status) ? Strings.UnpinAllMessages : Strings.HidePinnedMessages));
            }
            else if (group.UpgradedToSupergroupId != 0)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.OpenSupergroup, Strings.OpenSupergroup));
            }
            else if (group.Status is ChatMemberStatusCreator { IsMember: false })
            {
                return Bar(new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, Strings.GroupJoin));
            }
            else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisGroup));
            }
            else if (group.Status is ChatMemberStatusMember && !CanSendAnything(chat.Permissions, null))
            {
                // A basic group has no personal restrictions and no boosts, so only the default rights apply.
                return ChatFooterState.Info(Strings.GlobalSendMessageRestricted);
            }

            return ChatFooterState.Composer(0);
        }

        private ChatFooterState ResolveSupergroupFooter(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (group == null)
            {
                return ChatFooterState.Composer(0);
            }
            else if (Type == DialogType.EventLog)
            {
                return ChatFooterState.Button(ChatFooterAction.EventLogFilters, Strings.Settings);
            }
            else if (Type == DialogType.Pinned)
            {
                return Bar(ChatFooterState.Button(ChatFooterAction.Unpin, CanPinMessages(chat, group.Status) && !group.IsDirectMessagesGroup ? Strings.UnpinAllMessages : Strings.HidePinnedMessages));
            }

            var status = group.Status;
            var notInChat = status is ChatMemberStatusLeft or ChatMemberStatusBanned or ChatMemberStatusRestricted { IsMember: false } or ChatMemberStatusCreator { IsMember: false };
            if (!notInChat || !group.JoinByRequest)
            {
                _joinRequestSent = false;
            }

            // A subscriber's view of a direct messages group is their own thread, which never asks to join.
            if (notInChat && !(group.IsDirectMessagesGroup && !group.IsAdministeredDirectMessagesGroup))
            {
                if (!group.IsDirectMessagesGroup && (status is ChatMemberStatusBanned || (status is ChatMemberStatusLeft && !group.IsPublic() && !ClientService.IsChatAccessible(chat))))
                {
                    return Bar(ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteChat));
                }
                else if (group.IsForum || Type != DialogType.Thread || group.JoinToSendMessages)
                {
                    if (_joiningChat)
                    {
                        return Bar(new ChatFooterState(ChatFooterKind.Progress, ChatFooterAction.None, null));
                    }
                    else if (group.JoinByRequest)
                    {
                        if (_joinRequestSent)
                        {
                            return Bar(ChatFooterState.Info(group.IsChannel ? Strings.ChannelJoinRequestSent : Strings.GroupJoinRequestSent));
                        }

                        return Bar(new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, group.IsChannel ? Strings.ChannelJoinRequest : Strings.GroupJoinRequest));
                    }

                    return Bar(new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, group.IsChannel ? Strings.ChannelJoin : Strings.GroupJoin));
                }
            }

            var administrator = status is ChatMemberStatusCreator or ChatMemberStatusAdministrator;

            // Any administrator may write to a broadcast group, only those with the right may post to a channel.
            var readOnly = group.IsChannel
                ? !(status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator { Rights.CanPostMessages: true })
                : group.IsBroadcastGroup && !administrator;

            if (Type != DialogType.Thread && IsForumShownAsMessages(group) && !CanPostToGeneralTopic(group))
            {
                return Bar(ChatFooterState.Info(Strings.ForumReplyToMessagesInTopic, true));
            }
            else if (readOnly)
            {
                if (Type != DialogType.Thread)
                {
                    return Bar(ChatFooterState.Button(ChatFooterAction.ToggleMute, ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute));
                }
            }
            else if (Type == DialogType.Thread && ForumTopic is ForumTopic { Info.IsClosed: true } forumTopic && !CanManageTopic(group, forumTopic))
            {
                return Bar(ChatFooterState.Info(Strings.TopicClosedByAdmin));
            }

            if (!group.IsChannel && !group.IsBroadcastGroup && !administrator && !IsUnrestrictedByBoosts(fullInfo))
            {
                var restricted = status as ChatMemberStatusRestricted;
                if (!CanSendAnything(chat.Permissions, restricted?.Permissions))
                {
                    if (!chat.Permissions.CanSendBasicMessages)
                    {
                        return fullInfo?.UnrestrictBoostCount > 0
                            ? ChatFooterState.Button(ChatFooterAction.Boost, Strings.BoostingBoostToSendMessages)
                            : ChatFooterState.Info(Strings.GlobalSendMessageRestricted);
                    }
                    else if (restricted != null)
                    {
                        return ChatFooterState.Info(restricted.IsForever()
                            ? Strings.SendMessageRestrictedForever
                            : string.Format(Strings.SendMessageRestricted, Formatter.BannedUntil(restricted.RestrictedUntilDate)));
                    }

                    return ChatFooterState.Info(Strings.GlobalSendMessageRestricted);
                }
            }

            return ChatFooterState.Composer(administrator || group.IsAdministeredDirectMessagesGroup ? 0 : group.PaidMessageStarCount);
        }

        private bool IsForumShownAsMessages(Supergroup group)
        {
            return (group.IsForum && Type == DialogType.History) || (group.IsDirectMessagesGroup && group.IsAdministeredDirectMessagesGroup);
        }

        // A direct messages group has no General topic. A forum's General is requested when the chat
        // opens; until it arrives it counts as postable, so that the composer does not flicker away.
        private bool CanPostToGeneralTopic(Supergroup group)
        {
            if (group.IsDirectMessagesGroup)
            {
                return false;
            }

            var general = _forumGeneralTopic;
            return general == null || !general.Info.IsClosed || CanManageTopic(group, general);
        }

        private static bool CanManageTopic(Supergroup group, ForumTopic topic)
        {
            return group.CanManageTopics() || topic.Info.IsOutgoing;
        }

        private static bool IsUnrestrictedByBoosts(SupergroupFullInfo fullInfo)
        {
            return fullInfo != null && fullInfo.UnrestrictBoostCount > 0 && fullInfo.MyBoostCount >= fullInfo.UnrestrictBoostCount;
        }

        // Polls are not counted: being able to send only a poll still leaves the chat read-only.
        private static bool CanSendAnything(ChatPermissions permissions, ChatPermissions restricted)
        {
            return (permissions.CanSendBasicMessages && (restricted == null || restricted.CanSendBasicMessages))
                || (permissions.CanSendPhotos && (restricted == null || restricted.CanSendPhotos))
                || (permissions.CanSendVideos && (restricted == null || restricted.CanSendVideos))
                || (permissions.CanSendVideoNotes && (restricted == null || restricted.CanSendVideoNotes))
                || (permissions.CanSendVoiceNotes && (restricted == null || restricted.CanSendVoiceNotes))
                || (permissions.CanSendDocuments && (restricted == null || restricted.CanSendDocuments))
                || (permissions.CanSendAudios && (restricted == null || restricted.CanSendAudios))
                || (permissions.CanSendOtherMessages && (restricted == null || restricted.CanSendOtherMessages));
        }

        private static bool CanPinMessages(Chat chat, ChatMemberStatus status)
        {
            return status switch
            {
                ChatMemberStatusMember => chat.Permissions.CanPinMessages,
                ChatMemberStatusCreator => true,
                ChatMemberStatusAdministrator administrator => administrator.Rights.CanPinMessages,
                ChatMemberStatusRestricted restricted => restricted.Permissions.CanPinMessages,
                _ => false
            };
        }

        private void LoadForumGeneralTopic(Chat chat, Supergroup group)
        {
            _forumGeneralTopic = null;

            if (group?.IsForum is true && Type == DialogType.History)
            {
                ClientService.Send(new GetForumTopic(chat.Id, 1), result =>
                {
                    if (result is ForumTopic topic)
                    {
                        BeginOnUIThread(() =>
                        {
                            if (_chat?.Id == chat.Id)
                            {
                                _forumGeneralTopic = topic;
                                UpdateFooter();
                            }
                        });
                    }
                });
            }
        }

        private void UpdateForumTopicFooter(ForumTopicInfo info)
        {
            if (info.ForumTopicId == 1 && _forumGeneralTopic != null)
            {
                _forumGeneralTopic.Info = info;
                UpdateFooter();
            }
            else if (info.ForumTopicId == ForumTopic?.Info.ForumTopicId)
            {
                UpdateFooter();
            }
        }

        public async void Action()
        {
            var chat = _chat;
            var footer = _footer;

            if (chat == null || footer == null)
            {
                return;
            }

            if (Constants.DEBUG && chat.Type is ChatTypeSupergroup { IsChannel: true } && footer.Action is ChatFooterAction.Join or ChatFooterAction.ToggleMute or ChatFooterAction.DeleteChat)
            {
                ClientService.Send(new AddLocalMessage(chat.Id, new MessageSenderChat(chat.Id), null, true, new InputMessageContact(new Contact("999888777666", "SIMILAR", "CHANNELS", string.Empty, ClientService.Options.MyId))));
                return;
            }

            switch (footer.Action)
            {
                case ChatFooterAction.Frozen:
                    ShowPopup(new FrozenPopup(ClientService.FreezeState));
                    break;
                case ChatFooterAction.ToggleSavedTag:
                    if (Search != null)
                    {
                        Search.FilterByTag = !Search.FilterByTag;
                    }
                    break;
                case ChatFooterAction.OpenSavedChat:
                    if (SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                    {
                        NavigationService.NavigateToChat(savedChat);
                    }
                    break;
                case ChatFooterAction.Unpin:
                    UnpinMessages();
                    break;
                case ChatFooterAction.DeleteChat:
                    DeleteChat();
                    break;
                case ChatFooterAction.ToggleMute:
                    ToggleMute();
                    break;
                case ChatFooterAction.Unblock:
                    Unblock();
                    break;
                case ChatFooterAction.Start:
                    Start();
                    break;
                case ChatFooterAction.OpenSupergroup:
                    if (ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup) && basicGroup.UpgradedToSupergroupId != 0)
                    {
                        var response = await ClientService.SendAsync(new CreateSupergroupChat(basicGroup.UpgradedToSupergroupId, false));
                        if (response is Chat migratedChat)
                        {
                            NavigationService.NavigateToChat(migratedChat);
                        }
                    }
                    break;
                case ChatFooterAction.Join:
                    JoinChannel();
                    break;
                case ChatFooterAction.Boost:
                    if (ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
                    {
                        Boost(fullInfo.UnrestrictBoostCount);
                    }
                    break;
                case ChatFooterAction.EventLogFilters:
                    FilterExecute();
                    break;
            }
        }
    }
}
