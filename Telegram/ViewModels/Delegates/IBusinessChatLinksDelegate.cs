using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IBusinessChatLinksDelegate : IViewModelDelegate
    {
        void UpdateBusinessChatLink(BusinessChatLink chatLink);
    }
}
