//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public enum MessageStoryState
    {
        None,
        Loading,
        Expired
    }

    public partial class MessageAsyncStory : MessageContent
    {
        /// <summary>
        /// True, if the story was automatically forwarded because of a mention of the user.
        /// </summary>
        public bool ViaMention { get; set; }

        /// <summary>
        /// Story identifier.
        /// </summary>
        public int StoryId { get; set; }

        /// <summary>
        /// Identifier of the chat that posted the story.
        /// </summary>
        public long StoryPosterChatId { get; set; }

        public MessageStoryState State { get; set; }

        public Story Story { get; set; }

        public override string ToString()
        {
            return nameof(MessageAsyncStory);
        }
    }
}
