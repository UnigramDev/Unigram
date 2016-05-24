using Telegram.Api.TL;

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

        public TLInt Offset { get; protected set; }

        public TLInt Limit { get; protected set; }

        public PartStatus Status { get; set; }

        public TLFile File { get; set; }

        public DownloadablePart(DownloadableItem item, TLInt offset, TLInt limit, int number = 0)
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