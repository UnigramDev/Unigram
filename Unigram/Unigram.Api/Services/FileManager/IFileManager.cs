using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IFileManager
    {
        void DownloadFile(TLFileLocation file, TLObject owner, TLInt fileSize);
        void CancelDownloadFile(TLObject owner);
    }
}
