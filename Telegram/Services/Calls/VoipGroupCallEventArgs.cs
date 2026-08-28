//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Services.Calls
{
    public record VoipGroupCallJoinedStateChangedEventArgs(bool IsJoined, bool NeedRejoin)
    {
        // TODO: handle in StoryContent/Window to close the view
        public bool IsClosed => !IsJoined && !NeedRejoin;
    }

    public record VoipGroupCallNetworkStateChangedEventArgs(bool IsConnected, bool IsTransitioningFromBroadcastToRtc);

    public record VoipGroupCallStreamStateChangedEventArgs(VoipGroupCallStreamState StreamState);

    public record VoipGroupCallVerificationStateChangedEventArgs(int Generation, Vector<string> Emojis);

    public record VoipGroupCallMessagesChangedEventArgs(GroupCallMessage Message, bool Deleted);

    public record VoipGroupCallReactionsChangedEventArgs(MessageSender SenderId, long StarCount);

    public record VoipGroupCallTopDonorsChangedEventArgs(Vector<PaidReactor> Donors);

    public record VoipGroupCallTotalStarCountChangedEventArgs(long TotalStarCount);

    public record VoipGroupCallStreamerChangedEventArgs(GroupCallParticipant Streamer);

    /// <param name="Participant">The instance the model holds, already merged.</param>
    /// <param name="Order">The order at the time of the update, empty once the participant has left. Not read off <paramref name="Participant"/>, which a later update can have moved again before a handler runs.</param>
    /// <param name="RemovedVideoInfo">Endpoints that went away, screen sharing first, or null.</param>
    /// <param name="AddedVideoInfo">Endpoints that appeared, screen sharing first, or null.</param>
    public record VoipGroupCallParticipantChangedEventArgs(GroupCallParticipant Participant, string Order, string[] RemovedVideoInfo, GroupCallParticipantVideoInfo[] AddedVideoInfo);

    public record VoipGroupCallParticipantsSlice(Vector<GroupCallParticipant> Participants, bool HasMore);
}
