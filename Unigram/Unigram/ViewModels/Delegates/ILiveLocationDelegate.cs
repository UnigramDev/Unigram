using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface ILiveLocationDelegate : IViewModelDelegate
    {
        void UpdateNewMessage(Message message);
        void UpdateMessageContent(Message message);
    }
}
