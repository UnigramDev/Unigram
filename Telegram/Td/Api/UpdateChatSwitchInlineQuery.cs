namespace Telegram.Td.Api
{
    public class UpdateChatSwitchInlineQuery
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
