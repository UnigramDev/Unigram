//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    // TDLib forgets a story whose post failed, so IStoryService keeps it, and raises this when the
    // stories it keeps for a chat change.
    public partial class UpdateFailedStories
    {
        public long ChatId { get; }

        public UpdateFailedStories(long chatId)
        {
            ChatId = chatId;
        }
    }
}
