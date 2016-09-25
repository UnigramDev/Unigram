using System;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadVideoFileManager
    {
        //Task<DownloadableItem> DownloadFileAsync(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize);

        void DownloadFileAsync(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<double> callback);

        void CancelDownloadFileAsync(TLObject owner);
    }
}
