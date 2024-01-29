namespace Telegram.Td.Api
{
    public class UpdateSavedMessagesChatOrder
    {
        public UpdateSavedMessagesChatOrder(SavedMessagesChat topic, long order)
        {
            Topic = topic;
            Order = order;
        }

        public SavedMessagesChat Topic { get; }

        public long Order { get; }
    }
}
