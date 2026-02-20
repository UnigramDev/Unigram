//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Td.Api
{
    public class ChatProjection : Object
    {
        public ChatProjection(Chat chat)
        {
            ClientData = chat.ClientData;
            DraftMessage = chat.DraftMessage;
            ReplyMarkupMessageId = chat.ReplyMarkupMessageId;
            PendingJoinRequests = chat.PendingJoinRequests;
            VideoChat = chat.VideoChat;
            BusinessBotManageBar = chat.BusinessBotManageBar;
            ActionBar = chat.ActionBar;
            Theme = chat.Theme;
            Background = chat.Background;
            EmojiStatus = chat.EmojiStatus;
            MessageAutoDeleteTime = chat.MessageAutoDeleteTime;
            AvailableReactions = chat.AvailableReactions;
            NotificationSettings = chat.NotificationSettings;
            UnreadReactionCount = chat.UnreadReactionCount;
            UnreadMentionCount = chat.UnreadMentionCount;
            LastReadOutboxMessageId = chat.LastReadOutboxMessageId;
            LastReadInboxMessageId = chat.LastReadInboxMessageId;
            UnreadCount = chat.UnreadCount;
            DefaultDisableNotification = chat.DefaultDisableNotification;
            CanBeReported = chat.CanBeReported;
            CanBeDeletedForAllUsers = chat.CanBeDeletedForAllUsers;
            CanBeDeletedOnlyForSelf = chat.CanBeDeletedOnlyForSelf;
            HasScheduledMessages = chat.HasScheduledMessages;
            ViewAsTopics = chat.ViewAsTopics;
            IsMarkedAsUnread = chat.IsMarkedAsUnread;
            IsTranslatable = chat.IsTranslatable;
            HasProtectedContent = chat.HasProtectedContent;
            BlockList = chat.BlockList;
            MessageSenderId = chat.MessageSenderId;
            ChatLists = chat.ChatLists;
            Positions = chat.Positions;
            LastMessage = chat.LastMessage;
            Permissions = chat.Permissions;
            ProfileBackgroundCustomEmojiId = chat.ProfileBackgroundCustomEmojiId;
            ProfileAccentColorId = chat.ProfileAccentColorId;
            UpgradedGiftColors = chat.UpgradedGiftColors;
            BackgroundCustomEmojiId = chat.BackgroundCustomEmojiId;
            AccentColorId = chat.AccentColorId;
            Photo = chat.Photo;
            Title = chat.Title;
            Type = chat.Type;
            Id = chat.Id;
        }

        //
        // Summary:
        //     Application-specific data associated with the chat. (For example, the chat scroll
        //     position or local chat notification settings can be stored here.) Persistent
        //     if the message database is used.
        public string ClientData { get; set; }

        //
        // Summary:
        //     A draft of a message in the chat; may be null if none.
        public DraftMessage DraftMessage { get; set; }

        //
        // Summary:
        //     Identifier of the message from which reply markup needs to be used; 0 if there
        //     is no default custom reply markup in the chat.
        public long ReplyMarkupMessageId { get; set; }

        //
        // Summary:
        //     Information about pending join requests; may be null if none.
        public ChatJoinRequestsInfo PendingJoinRequests { get; set; }

        //
        // Summary:
        //     Information about video chat of the chat.
        public VideoChat VideoChat { get; set; }

        //
        // Summary:
        //     Information about bar for managing a business bot in the chat; may be null if
        //     none.
        public BusinessBotManageBar BusinessBotManageBar { get; set; }

        //
        // Summary:
        //     Information about actions which must be possible to do through the chat action
        //     bar; may be null if none.
        public ChatActionBar ActionBar { get; set; }

        //
        // Summary:
        //     Theme set for the chat; may be null if none.
        public ChatTheme Theme { get; set; }

        //
        // Summary:
        //     Background set for the chat; may be null if none.
        public ChatBackground Background { get; set; }

        //
        // Summary:
        //     Emoji status to be shown along with chat title; may be null.
        public EmojiStatus EmojiStatus { get; set; }

        //
        // Summary:
        //     Current message auto-delete or self-destruct timer setting for the chat, in seconds;
        //     0 if disabled. Self-destruct timer in secret chats starts after the message or
        //     its content is viewed. Auto-delete timer in other chats starts from the send
        //     date.
        public int MessageAutoDeleteTime { get; set; }

        //
        // Summary:
        //     Types of reaction, available in the chat.
        public ChatAvailableReactions AvailableReactions { get; set; }

        //
        // Summary:
        //     Notification settings for the chat.
        public ChatNotificationSettings NotificationSettings { get; set; }

        //
        // Summary:
        //     Number of messages with unread reactions in the chat.
        public int UnreadReactionCount { get; set; }

        //
        // Summary:
        //     Number of unread messages with a mention/reply in the chat.
        public int UnreadMentionCount { get; set; }

        //
        // Summary:
        //     Identifier of the last read outgoing message.
        public long LastReadOutboxMessageId { get; set; }

        //
        // Summary:
        //     Identifier of the last read incoming message.
        public long LastReadInboxMessageId { get; set; }

        //
        // Summary:
        //     Number of unread messages in the chat.
        public int UnreadCount { get; set; }

        //
        // Summary:
        //     Default value of the DisableNotification parameter, used when a message is sent
        //     to the chat.
        public bool DefaultDisableNotification { get; set; }

        //
        // Summary:
        //     True, if the chat can be reported to Telegram moderators through reportChat or
        //     reportChatPhoto.
        public bool CanBeReported { get; set; }

        //
        // Summary:
        //     True, if the chat messages can be deleted for all users.
        public bool CanBeDeletedForAllUsers { get; set; }

        //
        // Summary:
        //     True, if the chat messages can be deleted only for the current user while other
        //     users will continue to see the messages.
        public bool CanBeDeletedOnlyForSelf { get; set; }

        //
        // Summary:
        //     True, if the chat has scheduled messages.
        public bool HasScheduledMessages { get; set; }

        //
        // Summary:
        //     True, if the chat is a forum supergroup that must be shown in the "View as topics"
        //     mode, or Saved Messages chat that must be shown in the "View as chats".
        public bool ViewAsTopics { get; set; }

        //
        // Summary:
        //     True, if the chat is marked as unread.
        public bool IsMarkedAsUnread { get; set; }

        //
        // Summary:
        //     True, if translation of all messages in the chat must be suggested to the user.
        public bool IsTranslatable { get; set; }

        //
        // Summary:
        //     True, if chat content can't be saved locally, forwarded, or copied.
        public bool HasProtectedContent { get; set; }

        //
        // Summary:
        //     Block list to which the chat is added; may be null if none.
        public BlockList BlockList { get; set; }

        //
        // Summary:
        //     Identifier of a user or chat that is selected to send messages in the chat; may
        //     be null if the user can't change message sender.
        public MessageSender MessageSenderId { get; set; }

        //
        // Summary:
        //     Chat lists to which the chat belongs. A chat can have a non-zero position in
        //     a chat list even if it doesn't belong to the chat list and have no position in
        //     a chat list even if it belongs to the chat list.
        public IList<ChatList> ChatLists { get; set; }

        //
        // Summary:
        //     Positions of the chat in chat lists.
        public IList<ChatPosition> Positions { get; set; }

        //
        // Summary:
        //     Last message in the chat; may be null if none or unknown.
        public Message LastMessage { get; set; }

        //
        // Summary:
        //     Actions that non-administrator chat members are allowed to take in the chat.
        public ChatPermissions Permissions { get; set; }

        //
        // Summary:
        //     Identifier of a custom emoji to be shown on the background of the chat's profile;
        //     0 if none.
        public long ProfileBackgroundCustomEmojiId { get; set; }

        //
        // Summary:
        //     Identifier of the profile accent color for the chat's profile; -1 if none.
        public int ProfileAccentColorId { get; set; }

        //
        // Summary:
        //     Color scheme based on an upgraded gift to be used for the chat instead of AccentColorId
        //     and BackgroundCustomEmojiId; may be null if none.
        public UpgradedGiftColors UpgradedGiftColors { get; set; }

        //
        // Summary:
        //     Identifier of a custom emoji to be shown on the reply header and link preview
        //     background for messages sent by the chat; 0 if none.
        public long BackgroundCustomEmojiId { get; set; }

        //
        // Summary:
        //     Identifier of the accent color for message sender name, and backgrounds of chat
        //     photo, reply header, and link preview.
        public int AccentColorId { get; set; }

        //
        // Summary:
        //     Chat photo; may be null.
        public ChatPhotoInfo Photo { get; set; }

        //
        // Summary:
        //     Chat title.
        public string Title { get; set; }

        //
        // Summary:
        //     Type of the chat.
        public ChatType Type { get; set; }

        //
        // Summary:
        //     Chat unique identifier.
        public long Id { get; set; }

        public NativeObject ToUnmanaged()
        {
            return null;
        }
    }
}
