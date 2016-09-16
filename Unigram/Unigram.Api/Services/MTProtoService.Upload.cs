using System;
using System.Diagnostics;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Upload;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void SaveFilePartAsync(long? fileId, int? filePart, string bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var filePartValue = filePart.Value;
            var bytesLength = bytes.Data.Length;

            var obj = new TLSaveFilePart{ FileId = fileId, FilePart = filePart, Bytes = bytes };

            SendInformativeMessage("upload.saveFilePart" + " " + filePart.Value, obj, callback, faultCallback);
        }

        public void SaveBigFilePartAsync(long? fileId, int? filePart, int? fileTotalParts, string bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSaveBigFilePart { FileId = fileId, FilePart = filePart, FileTotalParts = fileTotalParts, Bytes = bytes };
            Debug.WriteLine(string.Format("upload.saveBigFilePart file_id={0} file_part={1} file_total_parts={2} bytes={3}", fileId, filePart, fileTotalParts, bytes.Data.Length));
            SendInformativeMessage("upload.saveBigFilePart " + filePart + " " + fileTotalParts, obj, callback, faultCallback);
        }

        public void GetFileAsync(TLInputFileLocationBase location, int? offset, int? limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetFile { Location = location, Offset = offset, Limit = limit };

            SendInformativeMessage("upload.getFile", obj, callback, faultCallback);
        }
	}
}
