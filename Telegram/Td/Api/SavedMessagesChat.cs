namespace Telegram.Td.Api
{
    public class SavedMessagesChat
    {
        public SavedMessagesChat(long id, Message lastMessage, bool isPinned, long order)
        {
            Id = id;
            LastMessage = lastMessage;
            IsPinned = isPinned;
            Order = order;

            Topic = id switch
            {
                1 => new SavedMessagesTopicMyNotes(),
                2 => new SavedMessagesTopicAuthorHidden(),
                _ => new SavedMessagesTopicSavedFromChat(id)
            };
        }

        public long Id { get; }

        public SavedMessagesTopic Topic { get; }

        public Message LastMessage { get; set; }

        public bool IsPinned { get; set; }

        public long Order { get; set; }
    }
}
