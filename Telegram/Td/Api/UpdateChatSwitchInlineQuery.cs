//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Td.Api
{
    public partial class UpdateChatSwitchInlineQuery
    {
        public long ChatId { get; set; }

        public long BotUserId { get; set; }

        public string Query { get; set; }

        public UpdateChatSwitchInlineQuery(long chatId, long botUserId, string query)
        {
            ChatId = chatId;
            BotUserId = botUserId;
            Query = query;
        }
    }
}
