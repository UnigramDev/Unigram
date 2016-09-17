using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDocumentFileManager
    {
        void DownloadFileAsync(string fileName, int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<double> startCallback, Action<DownloadableItem> callback = null);
        void CancelDownloadFileAsync(TLObject owner);
    }
}