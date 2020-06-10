namespace Unigram.Services.Updates
{
    public class UpdatePasscodeLock
    {
        public UpdatePasscodeLock(bool enabled, bool locked)
        {
            IsEnabled = enabled;
            IsLocked = locked;
        }

        public bool IsEnabled { get; private set; }
        public bool IsLocked { get; private set; }
    }
}
