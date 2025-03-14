//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Td.Api
{
    public partial class MessageChatEvent : MessageContent
    {
        /// <summary>
        /// Action performed by the user.
        /// </summary>
        public ChatEventAction Action { get; set; }

        /// <summary>
        /// Identifier of the user who performed the action that triggered the event.
        /// </summary>
        public MessageSender MemberId { get; set; }

        /// <summary>
        /// Point in time (Unix timestamp) when the event happened.
        /// </summary>
        public int Date { get; set; }

        /// <summary>
        /// Chat event identifier.
        /// </summary>
        public long Id { get; set; }

        public MessageChatEvent(ChatEvent chatEvent)
        {
            Action = chatEvent.Action;
            MemberId = chatEvent.MemberId;
            Date = chatEvent.Date;
            Id = chatEvent.Id;
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }
}
