//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface IClientService
    {
        /// <summary>
        /// Takes the chat out of every list it is in, and remembers what the confirmation
        /// promised, until <see cref="RemovePendingDeleteChat"/> or
        /// <see cref="CommitPendingDeleteChat"/> resolves it. Nothing reaches the server
        /// meanwhile, so the chat keeps the positions the undo restores it with.
        /// </summary>
        /// <param name="removeFromChatList">
        /// False when only the history is being cleared: the chat keeps its place in the list.
        /// </param>
        /// <param name="revoke">Also delete the messages for the other party.</param>
        /// <param name="blockUser">Also block the bot. Ignored for anything but a bot.</param>
        void AddPendingDeleteChat(long chatId, bool removeFromChatList, bool revoke, bool blockUser);

        /// <summary>
        /// Undo: puts the chat back where it was, and forgets what was promised.
        /// </summary>
        void RemovePendingDeleteChat(long chatId);

        /// <summary>
        /// Carries out what was promised. The chat is already out of the lists, so the updates
        /// this produces only confirm what every view has been showing since.
        /// </summary>
        Task CommitPendingDeleteChat(long chatId);
    }

    public partial class ClientService
    {
        // Guarded by _chatLists, the lock the per-list services are reached through.
        private readonly Dictionary<long, PendingDeleteChat> _pendingDeleteChats = new();

        private readonly record struct PendingDeleteChat(bool RemoveFromChatList, bool Revoke, bool BlockUser);

        public void AddPendingDeleteChat(long chatId, bool removeFromChatList, bool revoke, bool blockUser)
        {
            if (!_chats.TryGetValue(chatId, out var chat))
            {
                return;
            }

            Vector<ChatPosition> positions;

            // chat then _chatLists, the order the update handler takes them in.
            lock (chat)
            {
                positions = chat.Positions;

                lock (_chatLists)
                {
                    _pendingDeleteChats[chatId] = new PendingDeleteChat(removeFromChatList, revoke, blockUser);
                }

                if (removeFromChatList)
                {
                    foreach (var position in positions)
                    {
                        GetChatList(position.List).SetOrder(chat, 0, false);
                    }
                }
            }

            if (removeFromChatList)
            {
                PublishPositions(chatId, positions, 0);
            }
        }

        public void RemovePendingDeleteChat(long chatId)
        {
            if (!_chats.TryGetValue(chatId, out var chat))
            {
                return;
            }

            PendingDeleteChat pending;
            Vector<ChatPosition> positions;

            lock (chat)
            {
                // Nothing was sent, so these are still the positions the server knows about,
                // including any move that landed while the delete was pending.
                positions = chat.Positions;

                lock (_chatLists)
                {
                    if (!_pendingDeleteChats.TryGetValue(chatId, out pending))
                    {
                        return;
                    }

                    _pendingDeleteChats.Remove(chatId);
                }

                if (pending.RemoveFromChatList)
                {
                    foreach (var position in positions)
                    {
                        GetChatList(position.List).SetOrder(chat, position.Order, false);
                    }
                }
            }

            if (pending.RemoveFromChatList)
            {
                PublishPositions(chatId, positions, null);
            }
        }

        public async Task CommitPendingDeleteChat(long chatId)
        {
            if (!_chats.TryGetValue(chatId, out var chat))
            {
                return;
            }

            PendingDeleteChat pending;

            lock (_chatLists)
            {
                if (!_pendingDeleteChats.TryGetValue(chatId, out pending))
                {
                    return;
                }
            }

            // Held until the requests are through, and dropped in the finally below. While
            // there is no entry there is no suppression in SetChatPositions either, so a
            // position update landing in between would put the chat back in the list for the
            // moment it takes the server to confirm what it is being told to do.
            //
            // The caller commits a chat once: a second commit for the same one while this is
            // in flight would send its requests twice.
            try
            {
                await CommitAsync(chat, pending);
            }
            finally
            {
                lock (_chatLists)
                {
                    _pendingDeleteChats.Remove(chatId);
                }
            }
        }

        private async Task CommitAsync(Chat chat, PendingDeleteChat pending)
        {
            var chatId = chat.Id;

            if (!pending.RemoveFromChatList)
            {
                await SendAsync(new DeleteChatHistory(chatId, false, pending.Revoke));
                return;
            }

            if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            {
                await SendAsync(new LeaveChat(chatId));
                await SendAsync(new DeleteChatHistory(chatId, true, false));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                await SendAsync(new DeleteChat(chatId));

                // Deleting the chat does not end the secret chat session: DeleteSelectedChats
                // closed it too and DeleteChat did not, and leaving it open is the odd one out.
                await SendAsync(new CloseSecretChat(secret.SecretChatId));
            }
            else
            {
                var user = GetUser(chat);
                if (user?.Type is UserTypeRegular)
                {
                    await SendAsync(new DeleteChatHistory(chatId, true, pending.Revoke));
                }
                else
                {
                    if (user?.Type is UserTypeBot && pending.BlockUser)
                    {
                        await SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), new BlockListMain()));
                    }

                    await SendAsync(new DeleteChatHistory(chatId, true, false));
                }
            }
        }

        /// <summary>
        /// Republishes the positions, with <paramref name="order"/> in place of the real one
        /// when it is given. Every view already watches these, so hiding and restoring a pending
        /// chat asks nothing of them.
        /// </summary>
        /// <param name="positions">
        /// Read under the chat's lock by the caller: re-reading here could publish a set that
        /// does not match the one the lists were adjusted for.
        /// </param>
        private void PublishPositions(long chatId, Vector<ChatPosition> positions, long? order)
        {
            foreach (var position in positions)
            {
                _aggregator.Publish(new UpdateChatPosition(chatId,
                    order is long value
                        ? new ChatPosition(position.List, value, false, null)
                        : position));
            }
        }
    }
}
