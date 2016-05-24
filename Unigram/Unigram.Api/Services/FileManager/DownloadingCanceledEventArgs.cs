namespace Telegram.Api.Services.FileManager
{
    public class DownloadingCanceledEventArgs
    {
        public DownloadableItem Item { get; protected set; }

        public DownloadingCanceledEventArgs(DownloadableItem item)
        {
            Item = item;
        }
    }
}