using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface ISupergroupDelegate : IChatDelegate
    {
        void UpdateSupergroup(Chat chat, Supergroup group);
        void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo);
    }
}
