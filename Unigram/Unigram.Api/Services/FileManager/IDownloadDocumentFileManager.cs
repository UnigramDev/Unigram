using System;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadDocumentFileManager
    {
        Task<DownloadableItem> DownloadFileAsync(string fileName, int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<double> startCallback);

        void DownloadFileAsync(string fileName, int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<double> startCallback, Action<DownloadableItem> callback = null);

        void CancelDownloadFileAsync(TLObject owner);
    }
}