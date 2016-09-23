using System;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadEncryptedFileManager
    {
        Task<DownloadableItem> DownloadFileAsync(TLEncryptedFile file, TLObject owner);

        void DownloadFile(TLEncryptedFile file, TLObject owner, Action<DownloadableItem> callback = null);

        void CancelDownloadFile(TLObject owner);
    }
}
