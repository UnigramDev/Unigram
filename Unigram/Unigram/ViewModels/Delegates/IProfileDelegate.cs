using TdWindows;

namespace Unigram.ViewModels.Delegates
{
    public interface IProfileDelegate : IChatDelegate, IUserDelegate, ISupergroupDelegate, IBasicGroupDelegate, IFileDelegate
    {
        void UpdateSecretChat(Chat chat, SecretChat secretChat);
    }
}
