using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class UploadableItem
    {
        public bool FileNotFound { get; set; }

        public bool IsSmallFile { get; set; }

        public long FileId { get; protected set; }

        public string IsoFileName { get; protected set; }

        public long IsoFileLength { get; protected set; }

        public TLObject Owner { get; protected set; }

        public TaskCompletionSource<UploadableItem> Source { get; set; }

#if WP8
        public StorageFile File { get; protected set; }

        public string Key { get; protected set; }

        public string IV { get; protected set; }
#endif

        public byte[] Bytes { get; protected set; }

        public List<UploadablePart> Parts { get; set; }

        public bool Canceled { get; set; }

        public UploadableItem(long fileId, TLObject owner, byte[] bytes)
        {
            FileId = fileId;
            Owner = owner;
            Bytes = bytes;
        }

        public UploadableItem(long fileId, TLObject owner, byte[] bytes, TaskCompletionSource<UploadableItem> source)
        {
            FileId = fileId;
            Owner = owner;
            Bytes = bytes;
            Source = source;
        }

#if WP8
        public UploadableItem(long? fileId, TLObject owner, StorageFile file)
        {
            FileId = fileId;
            Owner = owner;
            File = file;
        }

        public UploadableItem(long? fileId, TLObject owner, StorageFile file, string key, string iv)
        {
            FileId = fileId;
            Owner = owner;
            File = file;

            Key = key;
            IV = iv;
        }
#endif

        public UploadableItem(long fileId, TLObject owner, string isoFileName, long isoFileLength)
        {
            FileId = fileId;
            Owner = owner;
            IsoFileName = isoFileName;
            IsoFileLength = isoFileLength;
        }
    }
}
