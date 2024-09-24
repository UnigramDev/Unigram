using Telegram.Native.Calls;

namespace Telegram.Services.Calls
{
    public partial class VoipCallStateChangedEventArgs
    {
        public VoipCallStateChangedEventArgs(VoipState state, VoipReadyState readyState)
        {
            State = state;
            ReadyState = readyState;
        }

        public VoipState State { get; }

        public VoipReadyState ReadyState { get; }
    }
}
