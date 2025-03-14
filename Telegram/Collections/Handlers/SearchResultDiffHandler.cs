//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using Telegram.ViewModels;

namespace Telegram.Collections.Handlers
{
    public partial class SearchResultDiffHandler : IDiffHandler<SearchResult>
    {
        public bool CompareItems(SearchResult oldItem, SearchResult newItem)
        {
            if (oldItem.IsPublic != newItem.IsPublic)
            {
                return false;
            }

            if (oldItem.Chat != null && newItem.Chat != null)
            {
                return oldItem.Chat.Id == newItem.Chat.Id;
            }
            else if (oldItem.User != null && newItem.User != null)
            {
                return oldItem.User.Id == newItem.User.Id;
            }

            return false;
        }

        public void UpdateItem(SearchResult oldItem, SearchResult newItem)
        {
            oldItem.Query = newItem.Query;
        }
    }
}
