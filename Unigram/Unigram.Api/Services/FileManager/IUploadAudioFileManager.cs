using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadAudioFileManager
    {
        void UploadFile(TLLong fileId, TLObject owner, string fileName);
        void UploadFile(TLLong fileId, TLObject owner, string fileName, IList<UploadablePart> parts);
        void CancelUploadFile(TLLong fileId);
    }
}