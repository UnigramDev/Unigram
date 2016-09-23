using System.Collections.Generic;
#if WP8
using Windows.Storage;
#endif
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadVideoFileManager
    {
        void UploadFile(long fileId, TLObject owner, string fileName);
        void UploadFile(long fileId, TLObject owner, string fileName, IList<UploadablePart> parts);

#if WP8
        void UploadFile(long fileId, bool isGif, TLObject owner, StorageFile file);
#endif

        void CancelUploadFile(long fileId);
    }
}