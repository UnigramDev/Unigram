using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IGroupCallDelegate : IViewModelDelegate
    {
        void UpdateGroupCallParticipant(GroupCallParticipant participant);
        void UpdateGroupCallParticipants();
    }
}
