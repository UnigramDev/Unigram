//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public class InputMessageReplyToTopicMessage : InputMessageReplyTo
    {
        public InputMessageReplyToTopicMessage(long messageId, MessageTopic topicId, InputTextQuote quote, int checklistTaskId)
        {
            MessageId = messageId;
            TopicId = topicId;
            Quote = quote;
            ChecklistTaskId = checklistTaskId;
        }

        /// <summary>
        /// The identifier of the message to be replied in the same chat and forum topic.
        /// A message can be replied in the same chat and forum topic only if messageProperties.CanBeReplied.
        /// </summary>
        public long MessageId { get; set; }

        public MessageTopic TopicId { get; set; }

        /// <summary>
        /// Quote from the message to be replied; pass null if none. Must always be null
        /// for replies in secret chats.
        /// </summary>
        public InputTextQuote Quote { get; set; }

        public int ChecklistTaskId { get; set; }

        public override string ToString()
        {
            return nameof(MessageBigEmoji);
        }
    }
}
