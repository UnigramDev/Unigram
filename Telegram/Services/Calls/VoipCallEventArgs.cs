//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Native.Calls;

namespace Telegram.Services.Calls
{
    public class VoipCallAudioLevelUpdatedEventArgs(float audioLevel)
    {
        public float AudioLevel { get; set; } = audioLevel;
    }

    public record VoipCallConnectionStateChangedEventArgs(VoipConnectionState State);

    public record VoipCallMediaStateChangedEventArgs(VoipAudioState Audio, VoipVideoState Video, bool IsScreenSharing);

    public record VoipCallRemoteBatteryLevelIsLowChangedEventArgs(bool IsLow);

    public class VoipCallSignalBarsUpdatedEventArgs(int count)
    {
        public int Count { get; set; } = count;
    }

    public record VoipCallStateChangedEventArgs(VoipState State, VoipReadyState ReadyState);
}
