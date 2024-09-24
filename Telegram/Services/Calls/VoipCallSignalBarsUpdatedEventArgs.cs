namespace Telegram.Services.Calls
{
    public partial class VoipCallSignalBarsUpdatedEventArgs
    {
        public VoipCallSignalBarsUpdatedEventArgs(int count)
        {
            Count = count;
        }

        public int Count { get; set; }
    }
}
