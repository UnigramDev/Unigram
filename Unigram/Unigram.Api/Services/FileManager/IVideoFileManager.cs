using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IVideoFileManager
    {
        void DownloadFileAsync(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<double> callback);
        void CancelDownloadFileAsync(TLObject owner);
    }
}
