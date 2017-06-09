using System;
using System.Diagnostics;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Upload;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void SaveFilePartAsync(long fileId, int filePart, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var filePartValue = filePart;
            var bytesLength = bytes.Length;

            var obj = new TLUploadSaveFilePart{ FileId = fileId, FilePart = filePart, Bytes = bytes };

            SendInformativeMessage("upload.saveFilePart" + " " + filePart, obj, callback, faultCallback);
        }

        public void SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadSaveBigFilePart { FileId = fileId, FilePart = filePart, FileTotalParts = fileTotalParts, Bytes = bytes };
            Debug.WriteLine(string.Format("upload.saveBigFilePart file_id={0} file_part={1} file_total_parts={2} bytes={3}", fileId, filePart, fileTotalParts, bytes.Length));
            SendInformativeMessage("upload.saveBigFilePart " + filePart + " " + fileTotalParts, obj, callback, faultCallback);
        }

        public void GetFileAsync(TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };

            SendInformativeMessage("upload.getFile", obj, callback, faultCallback);
        }

        public void GetCdnFileAsync(byte[] fileToken, int offset, int limit, Action<TLUploadCdnFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetCdnFile { FileToken = fileToken, Offset = offset, Limit = limit };

            const string caption = "upload.getCdnFile";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReuploadCdnFileAsync(byte[] fileToken, byte[] requestToken, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadReuploadCdnFile { FileToken = fileToken, RequestToken = requestToken };

            const string caption = "upload.reuploadCdnFile";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
