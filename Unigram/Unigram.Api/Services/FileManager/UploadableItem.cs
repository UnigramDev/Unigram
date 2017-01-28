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

        public string FileName { get; protected set; }

        public long FileLength { get; protected set; }

        public TLObject Owner { get; protected set; }

#if WP8
        public StorageFile File { get; protected set; }

        public TLString Key { get; protected set; }

        public TLString IV { get; protected set; }
#endif

        public byte[] Bytes { get; protected set; }

        public List<UploadablePart> Parts { get; set; }

        public bool IsCancelled { get; set; }

        internal TaskCompletionSource<UploadableItem> Callback { get; set; }

        internal IProgress<double> Progress { get; set; }

        public UploadableItem(long fileId, TLObject owner, byte[] bytes)
        {
            FileId = fileId;
            Owner = owner;
            Bytes = bytes;
        }

#if WP8
        public UploadableItem(TLLong fileId, TLObject owner, StorageFile file)
        {
            FileId = fileId;
            Owner = owner;
            File = file;
        }

        public UploadableItem(TLLong fileId, TLObject owner, StorageFile file, TLString key, TLString iv)
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
            FileName = isoFileName;
            FileLength = isoFileLength;
        }
    }
}
