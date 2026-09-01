//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Telegram.Controls;
using Telegram.Td;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    /// <summary>
    /// Holds a chat deletion for as long as the undo is on offer, the way
    /// <see cref="PaidReactionService"/> holds a paid reaction.
    /// </summary>
    /// <remarks>
    /// The chat leaves every list the moment this is called, so every window agrees it is gone;
    /// what the confirmation promised is carried out only once the countdown runs out. The toast
    /// belongs to the window that asked, so closing that window abandons the deletion and the chat
    /// comes back - the same outcome as the undo, and the same as paid reactions.
    /// </remarks>
    public partial class DeleteChatService
    {
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

        private readonly IClientService _clientService;

        private readonly int _sessionId;

        // Every chat pending on this toast. A deletion while one is still up joins it rather
        // than putting a second toast on top: one countdown, and the undo puts them all back.
        private readonly List<long> _chatIds = new();

        private UndoToastPopup _pendingToast;

        private static readonly ConditionalWeakTable<XamlRoot, DeleteChatService> _instances = new();

        /// <param name="chatIds">
        /// Every chat this one confirmation covered. They join whatever is already pending on
        /// this window, and the whole set resolves together.
        /// </param>
        /// <param name="title">
        /// Already worded for the chat and for whether this deletes it or only clears it, which is
        /// the caller's to decide.
        /// </param>
        /// <param name="removeFromChatList">
        /// False when only the history is being cleared: the chat keeps its place in the list, and
        /// nothing is hidden.
        /// </param>
        /// <param name="revoke">Also delete the messages for the other party.</param>
        /// <param name="blockUser">Also block the bot. Ignored for anything but a bot.</param>
        public static void AddPending(XamlRoot xamlRoot, IClientService clientService, IList<long> chatIds, string title, bool removeFromChatList, bool revoke, bool blockUser)
        {
            _instances.TryGetValue(xamlRoot, out DeleteChatService instance);

            // By session alone: the same chat is never deleted twice, but several are deleted
            // one after another, and those belong on the one toast.
            if (instance == null || !instance.IsValid || !instance.AreTheSame(clientService))
            {
                _instances.AddOrUpdate(xamlRoot, instance = new(clientService));
            }

            instance.AddPendingImpl(xamlRoot, chatIds, title, removeFromChatList, revoke, blockUser);
        }

        public bool IsValid => _pendingToast?.IsOpen is true;

        public bool AreTheSame(IClientService clientService)
        {
            return _sessionId == clientService.SessionId;
        }

        private DeleteChatService(IClientService clientService)
        {
            _clientService = clientService;
            _sessionId = clientService.SessionId;
        }

        private void AddPendingImpl(XamlRoot xamlRoot, IList<long> chatIds, string title, bool removeFromChatList, bool revoke, bool blockUser)
        {
            foreach (var chatId in chatIds)
            {
                // The flags stay with the service that carries them out, one set per chat, so
                // chats joining an existing toast keep the ones their own confirmation offered.
                _clientService.AddPendingDeleteChat(chatId, removeFromChatList, revoke, blockUser);

                if (!_chatIds.Contains(chatId))
                {
                    _chatIds.Add(chatId);
                }
            }

            var text = ClientEx.ParseMarkdown(title);

            if (_pendingToast != null && _pendingToast.IsOpen)
            {
                // The newest wording wins: it is the action the user just took.
                _pendingToast.Extend(text);
                return;
            }

            // The countdown is the toast's own affordance here, so there is no icon beside it.
            _pendingToast = UndoToastPopup.Show(xamlRoot, text, null, Strings.Undo, Duration);

            if (_pendingToast == null)
            {
                // No toast means no way to offer the undo, and chats hidden with nothing left to
                // resolve them. Carry it out now rather than leave them pending forever.
                Commit();
                return;
            }

            _pendingToast.Committed += OnCommitted;
            _pendingToast.Undone += OnUndone;
        }

        private void OnCommitted(UndoToastPopup sender, object args)
        {
            Logger.Info("expired");

            Detach(sender);
            Commit();
        }

        private void OnUndone(UndoToastPopup sender, object args)
        {
            Logger.Info("closed");

            Detach(sender);

            foreach (var chatId in _chatIds)
            {
                _clientService.RemovePendingDeleteChat(chatId);
            }
        }

        private void Commit()
        {
            foreach (var chatId in _chatIds)
            {
                _ = _clientService.CommitPendingDeleteChat(chatId);
            }
        }

        // The instance is spent once its toast has gone: IsValid answers false, so the next
        // deletion starts a new one rather than reusing this list.
        private void Detach(UndoToastPopup sender)
        {
            sender.Committed -= OnCommitted;
            sender.Undone -= OnUndone;

            _pendingToast = null;
        }
    }
}
