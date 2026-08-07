//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public class InputMessageReplyToEphemeralMessage : InputMessageReplyTo
    {
        public InputMessageReplyToEphemeralMessage(long messageId, long receiverUserId, InputTextQuote quote, int checklistTaskId, string pollOptionId)
        {
            MessageId = messageId;
            ReceiverUserId = receiverUserId;
            Quote = quote;
            ChecklistTaskId = checklistTaskId;
            PollOptionId = pollOptionId;
        }

        public long MessageId { get; set; }

        public long ReceiverUserId { get; set; }

        public InputTextQuote Quote { get; set; }

        public int ChecklistTaskId { get; set; }

        public string PollOptionId { get; set; }
    }
}
