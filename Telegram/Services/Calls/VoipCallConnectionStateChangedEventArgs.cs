namespace Telegram.Services.Calls
{
    public partial class VoipCallConnectionStateChangedEventArgs
    {
        public VoipCallConnectionStateChangedEventArgs(VoipConnectionState state)
        {
            State = state;
        }

        public VoipConnectionState State { get; }
    }
}
