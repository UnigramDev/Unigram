using System.Collections.Generic;

namespace Unigram.Navigation.Services
{
    public class NavigationState : Dictionary<string, object>
    {
        public static NavigationState GetInvoice(long chatId, long messageId)
        {
            return new NavigationState { { "chatId", chatId }, { "messageId", messageId } };
        }

        public static NavigationState GetChatMember(long chatId, int userId)
        {
            return new NavigationState { { "chatId", chatId }, { "userId", userId } };
        }

        public static NavigationState GetSwitchQuery(string query, int botId)
        {
            return new NavigationState { { "switch_query", query }, { "switch_bot", botId } };
        }
    }
}
