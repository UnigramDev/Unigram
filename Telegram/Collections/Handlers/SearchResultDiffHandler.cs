using Rg.DiffUtils;
using Telegram.ViewModels;

namespace Telegram.Collections.Handlers
{
    public class SearchResultDiffHandler : IDiffHandler<SearchResult>
    {
        public bool CompareItems(SearchResult oldItem, SearchResult newItem)
        {
            if (oldItem.IsPublic != newItem.IsPublic)
            {
                return false;
            }

            if (oldItem.Chat != null && oldItem.Chat.Id == newItem.Chat?.Id)
            {
                return true;
            }
            else if (oldItem.User != null && oldItem.User.Id == newItem.User?.Id)
            {
                return true;
            }

            return false;
        }

        public void UpdateItem(SearchResult oldItem, SearchResult newItem)
        {
            oldItem.Query = newItem.Query;
        }
    }
}
