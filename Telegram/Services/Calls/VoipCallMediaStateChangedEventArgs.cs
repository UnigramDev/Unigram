using Telegram.Native.Calls;

namespace Telegram.Services.Calls
{
    public partial class VoipCallMediaStateChangedEventArgs
    {
        public VoipCallMediaStateChangedEventArgs(VoipAudioState audio, VoipVideoState video, bool screen)
        {
            Audio = audio;
            Video = video;
            IsScreenSharing = screen;
        }

        public VoipAudioState Audio { get; init; }

        public VoipVideoState Video { get; init; }

        public bool IsScreenSharing { get; init; }
    }
}
