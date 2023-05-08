//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Td.Api
{
    public class MessageSponsored : MessageContent
    {
        public MessageSponsored(SponsoredMessage message)
        {
            Content = message.Content;
            Link = message.Link;
            ShowChatPhoto = message.ShowChatPhoto;
            SponsorChatInfo = message.SponsorChatInfo;
            SponsorChatId = message.SponsorChatId;
            IsRecommended = message.IsRecommended;
        }

        /// <summary>
        /// Content of the message. Currently, can be only of the type messageText.
        /// </summary>
        public MessageContent Content { get; set; }

        /// <summary>
        /// An internal link to be opened when the sponsored message is clicked; may be null
        /// if the sponsor chat needs to be opened instead.
        /// </summary>
        public InternalLinkType Link { get; set; }

        /// <summary>
        /// True, if the sponsor's chat photo must be shown.
        /// </summary>
        public bool ShowChatPhoto { get; set; }

        /// <summary>
        /// Information about the sponsor chat; may be null unless SponsorChatId == 0.
        /// </summary>
        public ChatInviteLinkInfo SponsorChatInfo { get; set; }

        /// <summary>
        /// Sponsor chat identifier; 0 if the sponsor chat is accessible through an invite
        /// link.
        /// </summary>
        public long SponsorChatId { get; set; }

        /// <summary>
        /// True, if the message needs to be labeled as "recommended" instead of "sponsored".
        /// </summary>
        public bool IsRecommended { get; set; }

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
