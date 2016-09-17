using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IEncryptedFileManager
    {
        void DownloadFile(TLEncryptedFile file, TLObject owner, Action<DownloadableItem> callback = null);
        void CancelDownloadFile(TLObject owner);
    }
}
