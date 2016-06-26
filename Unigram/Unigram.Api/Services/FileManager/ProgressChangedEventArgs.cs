namespace Telegram.Api.Services.FileManager
{
    public class ProgressChangedEventArgs
    {
        public double Progress { get; protected set; }

        public DownloadableItem Item { get; protected set; }

        public ProgressChangedEventArgs(DownloadableItem item, double progress)
        {
            Item = item;
            Progress = progress;
        }
    }
}