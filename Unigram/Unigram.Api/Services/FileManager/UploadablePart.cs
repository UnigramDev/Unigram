using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.Services.FileManager
{
    public class UploadablePart
    {
        public UploadableItem ParentItem { get; protected set; }

        public int FilePart { get; protected set; }

        public PartStatus Status { get; set; }

        public byte[] Bytes { get; protected set; }

        public long Position { get; protected set; }

        public long Count { get; protected set; }

        public string IV { get; set; }

        public void ClearBuffer()
        {
            Bytes = null;
        }

        public UploadablePart(UploadableItem item, int filePart, byte[] bytes)
        {
            ParentItem = item;
            FilePart = filePart;
            Bytes = bytes;
        }

        public UploadablePart(UploadableItem item, int filePart, long position, long count)
        {
            ParentItem = item;
            FilePart = filePart;
            Position = position;
            Count = count;
        }

        public UploadablePart(UploadableItem item, int filePart, byte[] bytes, long position, long count)
        {
            ParentItem = item;
            FilePart = filePart;
            Bytes = bytes;
            Position = position;
            Count = count;
        }

        public override string ToString()
        {
            return string.Format("Part={0}, Status={1}, Position={2}, Count={3}", FilePart, Status, Position, Count);
        }

        public void SetBuffer(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void SetParentItem(UploadableItem item)
        {
            ParentItem = item;
        }
    }
}
