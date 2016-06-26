using System;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Upload;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public Task<MTProtoResponse<bool>> SaveFilePartAsync(long fileId, int filePart, byte[] bytes)
        {
            var filePartValue = filePart;
            var bytesLength = bytes.Length;

            var obj = new TLUploadSaveFilePart { FileId = fileId, FilePart = filePart, Bytes = bytes };

            return SendInformativeMessage<bool>("upload.saveFilePart " + filePart, obj);
        }

        public Task<MTProtoResponse<bool>> SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes)
        {
            var obj = new TLUploadSaveBigFilePart { FileId = fileId, FilePart = filePart, FileTotalParts = fileTotalParts, Bytes = bytes };

            return SendInformativeMessage<bool>("upload.saveBigFilePart " + filePart + " " + fileTotalParts, obj);
        }

        public Task<MTProtoResponse<TLUploadFile>> GetFileAsync(TLInputFileLocationBase location, int offset, int limit)
        {
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };

            return SendInformativeMessage<TLUploadFile>("upload.getFile", obj);
        }
    }
}
