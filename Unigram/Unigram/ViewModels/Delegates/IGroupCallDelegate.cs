using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IGroupCallDelegate : IViewModelDelegate
    {
        void UpdateGroupCallParticipant(GroupCallParticipant participant);

        void VideoInfoAdded(GroupCallParticipant participant, params GroupCallParticipantVideoInfo[] videoInfos);
        void VideoInfoRemoved(GroupCallParticipant participant, params string[] endpointIds);
    }
}
