using Telegram.Td.Api;

namespace Unigram.Services.Updates
{
    public class UpdatePlaybackItem
    {
        public UpdatePlaybackItem(Message currentItem)
        {
            CurrentItem = currentItem;
        }

        public Message CurrentItem { get; private set; }
    }
}
