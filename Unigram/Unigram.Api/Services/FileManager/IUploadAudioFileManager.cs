using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadAudioFileManager
    {
        void UploadFile(long fileId, TLObject owner, string fileName);
        void UploadFile(long fileId, TLObject owner, string fileName, IList<UploadablePart> parts);
        void CancelUploadFile(long fileId);
    }
}