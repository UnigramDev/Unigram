using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IAudioFileManager
    {
        void DownloadFile(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<DownloadableItem> callback = null);
        void CancelDownloadFile(TLObject owner);
    }
}