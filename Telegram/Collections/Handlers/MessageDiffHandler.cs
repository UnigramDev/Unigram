//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using Telegram.Td.Api;

namespace Telegram.Collections.Handlers
{
    public partial class MessageDiffHandler : IDiffHandler<Message>
    {
        public bool CompareItems(Message oldItem, Message newItem)
        {
            // Message ids are per chat, so the chat is part of the identity.
            return oldItem.Id == newItem.Id && oldItem.ChatId == newItem.ChatId;
        }

        public void UpdateItem(Message oldItem, Message newItem)
        {

        }
    }
}
