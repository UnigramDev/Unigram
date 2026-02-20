//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public partial class MessageSponsored : MessageContent
    {
        public MessageSponsored(SponsoredMessage message)
        {
            AdditionalInfo = message.AdditionalInfo;
            BackgroundCustomEmojiId = message.BackgroundCustomEmojiId;
            AccentColorId = message.AccentColorId;
            ButtonText = message.ButtonText;
            Title = message.Title;
            Sponsor = message.Sponsor;
            Content = message.Content;
            CanBeReported = message.CanBeReported;
            IsRecommended = message.IsRecommended;
            MessageId = message.MessageId;
        }

        /// <summary>
        /// If non-empty, additional information about the sponsored message to be shown
        /// along with the message.
        /// </summary>
        public string AdditionalInfo { get; set; }

        /// <summary>
        /// Identifier of a custom emoji to be shown on the message background; 0 if none.
        /// </summary>
        public long BackgroundCustomEmojiId { get; set; }

        /// <summary>
        /// Identifier of the accent color for title, button text and message background.
        /// </summary>
        public int AccentColorId { get; set; }

        /// <summary>
        /// Text for the message action button.
        /// </summary>
        public string ButtonText { get; set; }

        /// <summary>
        /// Title of the sponsored message.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Information about the sponsor of the message.
        /// </summary>
        public AdvertisementSponsor Sponsor { get; set; }

        /// <summary>
        /// Content of the message. Currently, can be only of the types messageText, messageAnimation,
        /// messagePhoto, or messageVideo. Video messages can be viewed fullscreen.
        /// </summary>
        public MessageContent Content { get; set; }

        /// <summary>
        /// True, if the message can be reported to Telegram moderators through reportChatSponsoredMessage.
        /// </summary>
        public bool CanBeReported { get; set; }

        /// <summary>
        /// True, if the message needs to be labeled as "recommended" instead of "sponsored".
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Message identifier; unique for the chat to which the sponsored message belongs
        /// among both ordinary and sponsored messages.
        /// </summary>
        public long MessageId { get; set; }

        public override string ToString()
        {
            return nameof(MessageSponsored);
        }
    }
}
