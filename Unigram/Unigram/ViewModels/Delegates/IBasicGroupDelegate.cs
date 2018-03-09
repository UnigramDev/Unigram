using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IBasicGroupDelegate : IChatDelegate
    {
        void UpdateBasicGroup(Chat chat, BasicGroup group);
        void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo);
    }
}
