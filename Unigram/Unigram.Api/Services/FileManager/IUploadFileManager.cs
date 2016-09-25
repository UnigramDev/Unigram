#if WP8
using Windows.Storage;
#endif
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadFileManager
    {
        void UploadFile(long fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(long fileId, TLObject owner, StorageFile file);
#endif
        void CancelUploadFile(long fileId);
    }

    public interface IUploadDocumentFileManager
    {
        void UploadFile(long fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(long fileId, TLObject owner, StorageFile file);
        void UploadFile(long fileId, TLObject owner, StorageFile file, string key, string iv);
#endif
        void CancelUploadFile(long fileId);
    }
}
