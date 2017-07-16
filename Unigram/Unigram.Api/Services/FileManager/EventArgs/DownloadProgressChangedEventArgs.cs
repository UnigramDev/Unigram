namespace Telegram.Api.Services.FileManager.EventArgs
{
    public class DownloadProgressChangedEventArgs
    {
        public double Progress { get; protected set; }

        public DownloadableItem Item { get; protected set; }

        public DownloadProgressChangedEventArgs(DownloadableItem item, double progress)
        {
            Item = item;
            Progress = progress;
        }
    }
}