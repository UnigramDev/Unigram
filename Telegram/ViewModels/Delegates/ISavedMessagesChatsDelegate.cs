using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface ISavedMessagesChatsDelegate : IViewModelDelegate
    {
        void UpdateSavedMessagesTopicLastMessage(SavedMessagesChat topic);
    }
}
