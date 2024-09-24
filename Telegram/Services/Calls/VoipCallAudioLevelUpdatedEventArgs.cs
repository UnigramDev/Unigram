namespace Telegram.Services.Calls
{
    public partial class VoipCallAudioLevelUpdatedEventArgs
    {
        public VoipCallAudioLevelUpdatedEventArgs(float audioLevel)
        {
            AudioLevel = audioLevel;
        }

        public float AudioLevel { get; set; }
    }
}
