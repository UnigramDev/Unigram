﻿using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadFileManager
    {
        Task<DownloadableItem> DownloadFileAsync(TLFileLocation file, TLObject owner, int fileSize);

        void DownloadFile(TLFileLocation file, TLObject owner, int fileSize);

        void DownloadFile(TLFileLocation file, TLObject owner, int fileSize, System.Action<DownloadableItem> callback);

        void CancelDownloadFile(TLObject owner);
    }
}
