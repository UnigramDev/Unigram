using System;
using System.Diagnostics;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload;
using Telegram.Api.TL.Upload.Methods;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void SaveFilePartAsync(long fileId, int filePart, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadSaveFilePart{ FileId = fileId, FilePart = filePart, Bytes = bytes };

            const string caption = "upload.saveFilePart";
            SendInformativeMessage(caption + " " + filePart, obj, callback, faultCallback, null, ConnectionManager.DefaultDatacenterId, ConnectionType.Upload, RequestFlag.ForceDownload | RequestFlag.FailOnServerError, true);
        }

        public void SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadSaveBigFilePart { FileId = fileId, FilePart = filePart, FileTotalParts = fileTotalParts, Bytes = bytes };

            Debug.WriteLine(string.Format("upload.saveBigFilePart file_id={0} file_part={1} file_total_parts={2} bytes={3}", fileId, filePart, fileTotalParts, bytes.Length));

            const string caption = "upload.saveBigFilePart";
            SendInformativeMessage(caption + filePart + " " + fileTotalParts, obj, callback, faultCallback, null, ConnectionManager.DefaultDatacenterId, ConnectionType.Upload, RequestFlag.ForceDownload | RequestFlag.FailOnServerError, true);
        }

        public void GetFileAsync(TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };

            const string caption = "upload.getFile";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetCdnFileAsync(byte[] fileToken, int offset, int limit, Action<TLUploadCdnFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetCdnFile { FileToken = fileToken, Offset = offset, Limit = limit };

            const string caption = "upload.getCdnFile";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReuploadCdnFileAsync(byte[] fileToken, byte[] requestToken, Action<TLVector<TLCdnFileHash>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadReuploadCdnFile { FileToken = fileToken, RequestToken = requestToken };

            const string caption = "upload.reuploadCdnFile";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetCdnFileHashesAsync(byte[] fileToken, int offset, Action<TLVector<TLCdnFileHash>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetCdnFileHashes { FileToken = fileToken, Offset = offset };

            const string caption = "upload.getCdnFileHashes";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
