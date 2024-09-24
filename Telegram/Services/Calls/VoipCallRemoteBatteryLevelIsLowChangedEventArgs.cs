namespace Telegram.Services.Calls
{
    public partial class VoipCallRemoteBatteryLevelIsLowChangedEventArgs
    {
        public VoipCallRemoteBatteryLevelIsLowChangedEventArgs(bool isLow)
        {
            IsLow = isLow;
        }

        public bool IsLow { get; }
    }
}
