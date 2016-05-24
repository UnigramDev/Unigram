using System.Collections.Generic;
#if WP8
using Windows.Storage;
#endif
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadVideoFileManager
    {
        void UploadFile(TLLong fileId, TLObject owner, string fileName);
        void UploadFile(TLLong fileId, TLObject owner, string fileName, IList<UploadablePart> parts);

#if WP8
        void UploadFile(TLLong fileId, TLObject owner, StorageFile file);
#endif

        void CancelUploadFile(TLLong fileId);
    }
}