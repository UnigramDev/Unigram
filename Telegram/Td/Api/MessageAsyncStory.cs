using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Td.Api
{
    public enum MessageStoryState
    {
        None,
        Loading,
        Expired
    }

    public class MessageAsyncStory : MessageContent
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
        public long StorySenderChatId { get; set; }

        public MessageStoryState State { get; set; }

        public Story Story { get; set; }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }
}
