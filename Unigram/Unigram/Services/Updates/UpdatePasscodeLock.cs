namespace Unigram.Services.Updates
{
    public class UpdatePasscodeLock
    {
        public UpdatePasscodeLock(bool enabled)
        {
            IsEnabled = enabled;
        }

        public bool IsEnabled { get; private set; }
    }
}
