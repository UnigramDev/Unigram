namespace Telegram.Services.Calls
{
    public class VoipCallRemoteBatteryLevelIsLowChangedEventArgs
    {
        public VoipCallRemoteBatteryLevelIsLowChangedEventArgs(bool isLow)
        {
            IsLow = isLow;
        }

        public bool IsLow { get; }
    }
}
