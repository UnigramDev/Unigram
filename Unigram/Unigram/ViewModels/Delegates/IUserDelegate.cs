using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IUserDelegate : IChatDelegate
    {
        void UpdateUser(Chat chat, User user, bool secret);
        void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken);

        void UpdateUserStatus(Chat chat, User user);
    }
}
