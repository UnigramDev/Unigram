//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services.Calls;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    internal static class PaidReactionToast
    {
        public static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

        public static FormattedText FormatSent(IClientService clientService, long pendingCount)
        {
            var title = clientService.Options.IsPaidReactionAnonymous
                ? Strings.StarsSentAnonymouslyTitle
                : Strings.StarsSentTitle;

            return ClientEx.ParseMarkdown(string.Format("**{0}**\n{1}", title, Locale.Declension(Strings.R.StarsSentText, pendingCount)));
        }
    }

    public partial class PaidReactionService
    {
        private readonly IClientService _clientService;

        private readonly int _sessionId;
        private readonly long _chatId;
        private readonly long _messageId;

        private int _pendingCount;
        private UndoToastPopup _pendingToast;

        private static readonly ConditionalWeakTable<XamlRoot, PaidReactionService> _instances = new();

        public static Task<Object> AddPendingAsync(XamlRoot xamlRoot, MessageViewModel message, int starCount, PaidReactionType type)
        {
            _instances.TryGetValue(xamlRoot, out PaidReactionService instance);

            if (instance == null || !instance.IsValid || !instance.AreTheSame(message))
            {
                _instances.AddOrUpdate(xamlRoot, instance = new(message));
            }

            return instance.AddPendingImpl(xamlRoot, message, starCount, type);
        }

        public bool IsValid => _pendingToast?.IsOpen is true;

        public bool AreTheSame(MessageViewModel other)
        {
            return _sessionId == other.ClientService.SessionId
                && _chatId == other.ChatId
                && _messageId == other.Id;
        }

        private PaidReactionService(MessageViewModel message)
        {
            _clientService = message.ClientService;

            _sessionId = message.ClientService.SessionId;
            _chatId = message.ChatId;
            _messageId = message.Id;
        }

        private async Task<Object> AddPendingImpl(XamlRoot xamlRoot, MessageViewModel message, int starCount, PaidReactionType type)
        {
            if (_clientService.OwnedStarCount.StarCount < _pendingCount + starCount)
            {
                _ = message.Delegate.NavigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(starCount, _chatId));
                return null;
            }

            _pendingCount += starCount;
            await _clientService.SendAsync(new AddPendingPaidMessageReaction(_chatId, _messageId, starCount, type));

            var text = PaidReactionToast.FormatSent(_clientService, _pendingCount);

            if (_pendingToast != null && _pendingToast.IsOpen)
            {
                _pendingToast.Extend(text);
            }
            else
            {
                _pendingToast = UndoToastPopup.Show(xamlRoot, text, ToastPopupIcon.StarsSent, Strings.StarsSentUndo, PaidReactionToast.Duration);

                if (_pendingToast != null)
                {
                    _pendingToast.Committed += OnCommitted;
                    _pendingToast.Undone += OnUndone;
                }
            }

            return new Ok();
        }

        private void OnCommitted(UndoToastPopup sender, object args)
        {
            Logger.Info("expired");

            Detach(sender);
            _clientService.Send(new CommitPendingPaidMessageReactions(_chatId, _messageId));
        }

        private void OnUndone(UndoToastPopup sender, object args)
        {
            Logger.Info("closed");

            Detach(sender);
            _clientService.Send(new RemovePendingPaidMessageReactions(_chatId, _messageId));
        }

        private void Detach(UndoToastPopup sender)
        {
            sender.Committed -= OnCommitted;
            sender.Undone -= OnUndone;

            _pendingToast = null;
            _pendingCount = 0;
        }
    }

    public partial class GroupCallPaidReactionService
    {
        private readonly IClientService _clientService;

        private readonly int _sessionId;
        private readonly int _groupCallId;

        private long _pendingCount;
        private UndoToastPopup _pendingToast;

        private static readonly ConditionalWeakTable<XamlRoot, GroupCallPaidReactionService> _instances = new();

        /// <summary>
        /// Answers the instance to watch when this call put a toast up, and null when it joined the
        /// one already showing - the caller is subscribed to that one already.
        /// </summary>
        public static GroupCallPaidReactionService AddPending(INavigationService navigationService, VoipGroupCall groupCall, long starCount, PaidReactionType type)
        {
            _instances.TryGetValue(navigationService.XamlRoot, out GroupCallPaidReactionService instance);

            if (instance == null || !instance.IsValid || !instance.AreTheSame(groupCall))
            {
                _instances.AddOrUpdate(navigationService.XamlRoot, instance = new(groupCall));
            }

            if (instance.AddPendingImpl(navigationService, groupCall, starCount, type))
            {
                return instance;
            }

            return null;
        }

        public bool IsValid => _pendingToast?.IsOpen is true;

        public bool AreTheSame(VoipGroupCall other)
        {
            return _sessionId == other.ClientService.SessionId
                && _groupCallId == other.Id;
        }

        private GroupCallPaidReactionService(VoipGroupCall groupCall)
        {
            _clientService = groupCall.ClientService;

            _sessionId = groupCall.ClientService.SessionId;
            _groupCallId = groupCall.Id;
        }

        /// <summary>
        /// Raised on both exits, so a view can put back whatever it changed while the reaction was
        /// pending.
        /// </summary>
        public event TypedEventHandler<GroupCallPaidReactionService, object> Completed;

        private bool AddPendingImpl(INavigationService navigationService, VoipGroupCall groupCall, long starCount, PaidReactionType type)
        {
            if (_clientService.OwnedStarCount.StarCount < _pendingCount + starCount)
            {
                _ = navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(starCount, 0));
                return false;
            }

            _pendingCount += starCount;
            _ = _clientService.SendAsync(new AddPendingLiveStoryReaction(_groupCallId, starCount));

            var text = PaidReactionToast.FormatSent(_clientService, _pendingCount);

            if (_pendingToast != null && _pendingToast.IsOpen)
            {
                _pendingToast.Extend(text);
                return false;
            }

            _pendingToast = UndoToastPopup.Show(navigationService.XamlRoot, text, ToastPopupIcon.StarsSent, Strings.StarsSentUndo, PaidReactionToast.Duration);

            if (_pendingToast == null)
            {
                return false;
            }

            _pendingToast.Committed += OnCommitted;
            _pendingToast.Undone += OnUndone;

            return true;
        }

        private void OnCommitted(UndoToastPopup sender, object args)
        {
            Logger.Info("expired");

            Detach(sender);
            _clientService.Send(new CommitPendingLiveStoryReactions(_groupCallId));

            Completed?.Invoke(this, null);
        }

        private void OnUndone(UndoToastPopup sender, object args)
        {
            Logger.Info("closed");

            Detach(sender);
            _clientService.Send(new RemovePendingLiveStoryReactions(_groupCallId));

            Completed?.Invoke(this, null);
        }

        private void Detach(UndoToastPopup sender)
        {
            sender.Committed -= OnCommitted;
            sender.Undone -= OnUndone;

            _pendingToast = null;
            _pendingCount = 0;
        }
    }
}
