using Telegram.Api.TL;
using Telegram.Api.TL.Upload;

namespace Telegram.Api.Services.FileManager
{
    public enum PartStatus
    {
        Ready,
        Processing,
        Processed,
    }

    public class DownloadablePart
    {
        public int Number { get; protected set; }

        public DownloadableItem ParentItem { get; protected set; }

        public int Offset { get; protected set; }

        public int Limit { get; protected set; }

        public PartStatus Status { get; set; }

        public TLUploadFile File { get; set; }

        public TLUploadWebFile WebFile { get; set; }

        public DownloadablePart(DownloadableItem item, int offset, int limit, int number = 0)
        {
            ParentItem = item;
            Offset = offset;
            Limit = limit;
            Number = number;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", Number, Offset, Limit);
        }
    }
}