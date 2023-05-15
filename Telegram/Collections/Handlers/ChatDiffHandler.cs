using Rg.DiffUtils;
using Telegram.Td.Api;

namespace Telegram.Collections.Handlers
{
    public class ChatDiffHandler : IDiffHandler<Chat>
    {
        public bool CompareItems(Chat oldItem, Chat newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(Chat oldItem, Chat newItem)
        {

        }
    }
}
