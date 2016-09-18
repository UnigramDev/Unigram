using System;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadAudioFileManager
    {
        Task<DownloadableItem> DownloadFileAsync(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize);

        void DownloadFile(int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize, Action<DownloadableItem> callback = null);

        void CancelDownloadFile(TLObject owner);
    }
}