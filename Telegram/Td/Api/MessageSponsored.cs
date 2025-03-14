//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Td.Api
{
    public partial class MessageSponsored : MessageContent
    {
        public MessageSponsored(SponsoredMessage message)
        {
            AdditionalInfo = message.AdditionalInfo;
            Sponsor = message.Sponsor;
            Content = message.Content;
            IsRecommended = message.IsRecommended;
            MessageId = message.MessageId;
        }

        /// <summary>
        /// If non-empty, additional information about the sponsored message to be shown
        /// along with the message.
        /// </summary>
        public string AdditionalInfo { get; set; }

        /// <summary>
        /// Information about the sponsor of the message.
        /// </summary>
        public MessageSponsor Sponsor { get; set; }

        /// <summary>
        /// Content of the message. Currently, can be only of the type messageText.
        /// </summary>
        public MessageContent Content { get; set; }

        /// <summary>
        /// True, if the message needs to be labeled as "recommended" instead of "sponsored".
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Message identifier; unique for the chat to which the sponsored message belongs
        /// among both ordinary and sponsored messages.
        /// </summary>
        public long MessageId { get; set; }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return nameof(MessageSponsored);
        }
    }
}
