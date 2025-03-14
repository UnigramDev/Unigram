//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using Telegram.Td.Api;

namespace Telegram.Collections.Handlers
{
    public partial class ChatDiffHandler : IDiffHandler<Chat>
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
