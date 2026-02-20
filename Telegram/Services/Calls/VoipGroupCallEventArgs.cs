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

    public record VoipGroupCallVerificationStateChangedEventArgs(int Generation, IList<string> Emojis);

    public record VoipGroupCallMessagesChangedEventArgs(GroupCallMessage Message, bool Deleted);

    public record VoipGroupCallReactionsChangedEventArgs(MessageSender SenderId, long StarCount);

    public record VoipGroupCallTopDonorsChangedEventArgs(IList<PaidReactor> Donors);

    public record VoipGroupCallTotalStarCountChangedEventArgs(long TotalStarCount);

    public record VoipGroupCallStreamerChangedEventArgs(GroupCallParticipant Streamer);
}
