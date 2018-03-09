using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IChatDelegate : IViewModelDelegate
    {
        void UpdateChat(Chat chat);
        void UpdateChatTitle(Chat chat);
        void UpdateChatPhoto(Chat chat);
    }
}
