//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Services.Calls
{
    public partial class VoipGroupCallNetworkStateChangedEventArgs : EventArgs
    {
        public VoipGroupCallNetworkStateChangedEventArgs(bool isConnected, bool isTransitioningFromBroadcastToRtc)
        {
            IsConnected = isConnected;
            IsTransitioningFromBroadcastToRtc = isTransitioningFromBroadcastToRtc;
        }

        public bool IsConnected { get; }

        public bool IsTransitioningFromBroadcastToRtc { get; }
    }
}
