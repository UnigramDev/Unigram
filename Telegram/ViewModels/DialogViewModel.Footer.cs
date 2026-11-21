//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
        Link
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
            ApplyFooter(chat, ResolveFrozenFooter() ?? ResolveUserFooter(chat, user, fullInfo, secretChat));
        }

        public void UpdateFooter(Chat chat, BasicGroup group)
        {
            ApplyFooter(chat, ResolveFrozenFooter() ?? ResolveBasicGroupFooter(chat, group));
        }

        public void UpdateFooter(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ApplyFooter(chat, ResolveFrozenFooter() ?? ResolveSupergroupFooter(chat, group, fullInfo));
        }

        private void ApplyFooter(Chat chat, ChatFooterState state)
        {
            _footer = state;
            Delegate?.UpdateFooter(chat, state);
        }

        private ChatFooterState ResolveFrozenFooter()
        {
            return ClientService.FreezeState.IsFrozen
                ? new ChatFooterState(ChatFooterKind.Link, ChatFooterAction.Frozen, null)
                : null;
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
                return ChatFooterState.Button(ChatFooterAction.Unpin, Strings.UnpinAllMessages);
            }
            else if (user.Type is UserTypeDeleted)
            {
                return ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisChat);
            }
            else if (chat.Id == ClientService.Options.RepliesBotChatId || chat.Id == ClientService.Options.VerificationCodesBotChatId)
            {
                return ChatFooterState.Button(ChatFooterAction.ToggleMute, ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute);
            }
            else if (chat.BlockList is BlockListMain)
            {
                return ChatFooterState.Button(ChatFooterAction.Unblock, user.Type is UserTypeBot ? Strings.BotUnblock : Strings.Unblock);
            }
            else if (secretChat != null)
            {
                return secretChat.State switch
                {
                    SecretChatStatePending => ChatFooterState.Info(string.Format(Strings.AwaitingEncryption, ClientService.GetTitle(chat))),
                    SecretChatStateClosed => ChatFooterState.Info(Strings.EncryptionRejected),
                    _ => ChatFooterState.Composer(0)
                };
            }
            else if (user.Type is UserTypeBot && (_accessToken != null || chat.LastMessage == null))
            {
                return new ChatFooterState(ChatFooterKind.StartButton, ChatFooterAction.Start, Strings.BotStart);
            }
            else if (user.RestrictsNewChats)
            {
                return _canSendMessageToUser switch
                {
                    CanSendMessageToUserResultOk => ChatFooterState.Composer(0),
                    CanSendMessageToUserResultUserHasPaidMessages userHasPaidMessages => ChatFooterState.Composer(userHasPaidMessages.OutgoingPaidMessageStarCount),
                    CanSendMessageToUserResultUserIsDeleted => ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisChat),
                    CanSendMessageToUserResultUserRestrictsNewChats => new ChatFooterState(ChatFooterKind.Link, ChatFooterAction.None, string.Format(Strings.OnlyPremiumCanMessage, user.FirstName)),
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
            else if (group.UpgradedToSupergroupId != 0)
            {
                return ChatFooterState.Button(ChatFooterAction.OpenSupergroup, Strings.OpenSupergroup);
            }
            else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                return ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteThisGroup);
            }
            else if (group.Status is ChatMemberStatusCreator { IsMember: false })
            {
                return new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, Strings.ChannelJoin);
            }
            else if (Type == DialogType.Pinned)
            {
                return ChatFooterState.Button(ChatFooterAction.Unpin, chat.CanPinMessages(ClientService) ? Strings.UnpinAllMessages : Strings.HidePinnedMessages);
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
                return ChatFooterState.Button(ChatFooterAction.Unpin, chat.CanPinMessages(ClientService) ? Strings.UnpinAllMessages : Strings.HidePinnedMessages);
            }
            else if (group.IsChannel || group.IsBroadcastGroup)
            {
                if ((group.Status is ChatMemberStatusLeft && (group.HasActiveUsername() || ClientService.IsChatAccessible(chat))) || group.Status is ChatMemberStatusCreator { IsMember: false })
                {
                    return new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, Strings.ChannelJoin);
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator { Rights.CanPostMessages: true })
                {
                    return ChatFooterState.Composer(0);
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    return ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteChat);
                }

                return ChatFooterState.Button(ChatFooterAction.ToggleMute, ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute);
            }

            if ((group.Status is ChatMemberStatusLeft && (group.IsPublic() || ClientService.IsChatAccessible(chat))) || group.Status is ChatMemberStatusCreator { IsMember: false })
            {
                if (Type == DialogType.Thread)
                {
                    if (group.JoinToSendMessages)
                    {
                        return new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, group.JoinByRequest ? Strings.ChannelJoinRequest : Strings.JoinGroup);
                    }
                    else if (!chat.Permissions.CanSendBasicMessages)
                    {
                        return ChatFooterState.Info(Strings.GlobalSendMessageRestricted);
                    }

                    return ChatFooterState.Composer(group.PaidMessageStarCount);
                }
                else if (group.IsDirectMessagesGroup)
                {
                    return ChatFooterState.Composer(group.PaidMessageStarCount);
                }

                return new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, group.JoinByRequest ? Strings.ChannelJoinRequest : Strings.ChannelJoin);
            }
            else if (group.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                if (Type != DialogType.Thread && group.IsDirectMessagesGroup && group.IsAdministeredDirectMessagesGroup)
                {
                    return ChatFooterState.Info(Strings.ForumReplyToMessagesInTopic, true);
                }

                return ChatFooterState.Composer(0);
            }
            else if (group.Status is ChatMemberStatusRestricted restricted)
            {
                if (!restricted.IsMember && group.HasActiveUsername())
                {
                    return new ChatFooterState(ChatFooterKind.AccentButton, ChatFooterAction.Join, Strings.ChannelJoin);
                }
                else if (!restricted.Permissions.CanSendBasicMessages)
                {
                    return ChatFooterState.Info(restricted.IsForever()
                        ? Strings.SendMessageRestrictedForever
                        : string.Format(Strings.SendMessageRestricted, Formatter.BannedUntil(restricted.RestrictedUntilDate)));
                }

                return ChatFooterState.Composer(group.PaidMessageStarCount);
            }
            else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                if (group.IsDirectMessagesGroup)
                {
                    return ChatFooterState.Composer(group.PaidMessageStarCount);
                }

                return ChatFooterState.Button(ChatFooterAction.DeleteChat, Strings.DeleteChat);
            }
            else if (!chat.Permissions.CanSendBasicMessages && (fullInfo == null || fullInfo.MyBoostCount < fullInfo.UnrestrictBoostCount))
            {
                if (fullInfo != null)
                {
                    return ChatFooterState.Button(ChatFooterAction.Boost, Strings.BoostingBoostToSendMessages);
                }

                return ChatFooterState.Info(Strings.GlobalSendMessageRestricted);
            }
            else if (Type != DialogType.Thread && group.IsDirectMessagesGroup && group.IsAdministeredDirectMessagesGroup)
            {
                return ChatFooterState.Info(Strings.ForumReplyToMessagesInTopic, true);
            }
            else if (ForumTopic is ForumTopic { Info.IsClosed: true })
            {
                return ChatFooterState.Info(Strings.TopicClosedByAdmin);
            }

            return ChatFooterState.Composer(group.IsAdministeredDirectMessagesGroup ? 0 : group.PaidMessageStarCount);
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
