using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IEncryptedFileManager
    {
        void DownloadFile(TLEncryptedFile file, TLObject owner);
        void CancelDownloadFile(TLObject owner);
    }
}
