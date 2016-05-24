#if WP8
using Windows.Storage;
#endif
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadFileManager
    {
        void UploadFile(TLLong fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(TLLong fileId, TLObject owner, StorageFile file);
#endif
        void CancelUploadFile(TLLong fileId);
    }

    public interface IUploadDocumentFileManager
    {
        void UploadFile(TLLong fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(TLLong fileId, TLObject owner, StorageFile file);
        void UploadFile(TLLong fileId, TLObject owner, StorageFile file, TLString key, TLString iv);
#endif
        void CancelUploadFile(TLLong fileId);
    }
}
