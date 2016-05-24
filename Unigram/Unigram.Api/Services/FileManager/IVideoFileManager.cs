using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IVideoFileManager
    {
        void DownloadFileAsync(TLInt dcId, TLInputVideoFileLocation file, TLObject owner, TLInt fileSize, Action<double> callback);
        void CancelDownloadFileAsync(TLObject owner);
    }
}
