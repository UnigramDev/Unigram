//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#nullable enable

namespace Telegram.Td.Api
{
    /// <summary>
    /// User rights changed in a chat; for bots only
    /// </summary>
    public class UpdateChatMember : Update
    {
        /// <summary>
        /// Chat identifier
        /// </summary>
        public long ChatId { get; set; }
        /// <summary>
        /// Identifier of the user, changing the rights
        /// </summary>
        public long ActorUserId { get; set; }
        /// <summary>
        /// Point in time (Unix timestamp) when the user rights were changed
        /// </summary>
        public int Date { get; set; }
        /// <summary>
        /// If user has joined the chat using an invite link, the invite link; may be null
        /// </summary>
        public ChatInviteLink? InviteLink { get; set; }
        /// <summary>
        /// True, if the user has joined the chat after sending a join request and being approved by an administrator
        /// </summary>
        public bool ViaJoinRequest { get; set; }
        /// <summary>
        /// True, if the user has joined the chat using an invite link for a chat folder
        /// </summary>
        public bool ViaChatFolderInviteLink { get; set; }
        /// <summary>
        /// Previous chat member
        /// </summary>
        public ChatMember OldChatMember { get; set; }
        /// <summary>
        /// New chat member
        /// </summary>
        public ChatMember NewChatMember { get; set; }

        /// <summary>
        /// User rights changed in a chat; for bots only
        /// </summary>
        public UpdateChatMember()
        {
        }

        /// <summary>
        /// User rights changed in a chat; for bots only
        /// </summary>
        /// <param name="chatId">Chat identifier</param>
        /// <param name="actorUserId">Identifier of the user, changing the rights</param>
        /// <param name="date">Point in time (Unix timestamp) when the user rights were changed</param>
        /// <param name="inviteLink">If user has joined the chat using an invite link, the invite link; may be null</param>
        /// <param name="viaJoinRequest">True, if the user has joined the chat after sending a join request and being approved by an administrator</param>
        /// <param name="viaChatFolderInviteLink">True, if the user has joined the chat using an invite link for a chat folder</param>
        /// <param name="oldChatMember">Previous chat member</param>
        /// <param name="newChatMember">New chat member</param>
        public UpdateChatMember(long chatId, long actorUserId, int date, ChatInviteLink? inviteLink, bool viaJoinRequest, bool viaChatFolderInviteLink, ChatMember oldChatMember, ChatMember newChatMember)
        {
            ChatId = chatId;
            ActorUserId = actorUserId;
            Date = date;
            InviteLink = inviteLink;
            ViaJoinRequest = viaJoinRequest;
            ViaChatFolderInviteLink = viaChatFolderInviteLink;
            OldChatMember = oldChatMember;
            NewChatMember = newChatMember;
        }
    }
}
