using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IAudioFileManager
    {
        void DownloadFile(TLInt dcId, TLInputAudioFileLocation file, TLObject owner, TLInt fileSize);
        void CancelDownloadFile(TLObject owner);
    }
}