namespace Telegram.ViewModels.Delegates
{
    public interface IReactionsDelegate
    {
        void UpdateMessageInteractionInfo(MessageViewModel message);
        void UpdateMessageReactions(MessageViewModel message, bool animate);
        bool IsLoaded { get; }
    }
}
