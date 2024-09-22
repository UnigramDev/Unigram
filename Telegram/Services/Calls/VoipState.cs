namespace Telegram.Services.Calls
{
    public enum VoipState
    {
        None,
        Requesting,
        Waiting,
        Ringing,
        Connecting,
        Ready,
        HangingUp,
        Discarded,
        Error
    }
}
