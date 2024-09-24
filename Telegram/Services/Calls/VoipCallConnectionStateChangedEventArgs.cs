namespace Telegram.Services.Calls
{
    public class VoipCallConnectionStateChangedEventArgs
    {
        public VoipCallConnectionStateChangedEventArgs(VoipConnectionState state)
        {
            State = state;
        }

        public VoipConnectionState State { get; }
    }
}
