namespace Telegram.Td.Api
{
    public class UpdateSavedMessagesChatLastMessage
    {
        public UpdateSavedMessagesChatLastMessage(SavedMessagesChat topic, Message lastMessage, long order)
        {
            Topic = topic;
            LastMessage = lastMessage;
            Order = order;
        }

        public SavedMessagesChat Topic { get; }

        public Message LastMessage { get; }

        public long Order { get; }
    }
}
