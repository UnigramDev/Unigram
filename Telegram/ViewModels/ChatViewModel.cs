//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public class ChatViewModel : BindableBase
    {
        private readonly IClientService _clientService;

        private readonly Chat _chat;

        public ChatViewModel(IClientService clientService, Chat chat)
        {
            _clientService = clientService;

            _chat = chat;
        }

        public IClientService ClientService => _clientService;

        public Chat Native => _chat;



        public string ClientData { get => _chat.ClientData; set => _chat.ClientData = value; }
        public DraftMessage DraftMessage { get => _chat.DraftMessage; set => _chat.DraftMessage = value; }
        public long ReplyMarkupMessageId { get => _chat.ReplyMarkupMessageId; set => _chat.ReplyMarkupMessageId = value; }
        public ChatNotificationSettings NotificationSettings { get => _chat.NotificationSettings; set => _chat.NotificationSettings = value; }
        public int UnreadMentionCount { get => _chat.UnreadMentionCount; set => _chat.UnreadMentionCount = value; }
        public long LastReadOutboxMessageId { get => _chat.LastReadOutboxMessageId; set => _chat.LastReadOutboxMessageId = value; }
        public long LastReadInboxMessageId { get => _chat.LastReadInboxMessageId; set => _chat.LastReadInboxMessageId = value; }
        public int UnreadCount { get => _chat.UnreadCount; set => _chat.UnreadCount = value; }
        public bool DefaultDisableNotification { get => _chat.DefaultDisableNotification; set => _chat.DefaultDisableNotification = value; }
        public bool CanBeReported { get => _chat.CanBeReported; set => _chat.CanBeReported = value; }
        public IList<ChatPosition> Positions { get => _chat.Positions; set => _chat.Positions = value; }
        public Message LastMessage { get => _chat.LastMessage; set => _chat.LastMessage = value; }
        public ChatPhotoInfo Photo { get => _chat.Photo; set => _chat.Photo = value; }
        public string Title { get => _chat.Title; set => _chat.Title = value; }
        public ChatType Type { get => _chat.Type; set => _chat.Type = value; }
        public long Id { get => _chat.Id; set => _chat.Id = value; }
    }
}
