using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDocumentFileManager
    {
        void DownloadFileAsync(TLString fileName, TLInt dcId, TLInputDocumentFileLocation file, TLObject owner, TLInt fileSize, Action<double> callback);
        void CancelDownloadFileAsync(TLObject owner);
    }
}