﻿using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Upload;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void SaveFilePartAsync(TLLong fileId, TLInt filePart, TLString bytes, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var filePartValue = filePart.Value;
            var bytesLength = bytes.Data.Length;

            var obj = new TLSaveFilePart{ FileId = fileId, FilePart = filePart, Bytes = bytes };

            SendInformativeMessage("upload.saveFilePart" + " " + filePart.Value, obj, callback, faultCallback);
        }

        public void SaveBigFilePartAsync(TLLong fileId, TLInt filePart, TLInt fileTotalParts, TLString bytes, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSaveBigFilePart { FileId = fileId, FilePart = filePart, FileTotalParts = fileTotalParts, Bytes = bytes };

            SendInformativeMessage("upload.saveBigFilePart " + filePart + " " + fileTotalParts, obj, callback, faultCallback);
        }

        public void GetFileAsync(TLInputFileLocationBase location, TLInt offset, TLInt limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetFile { Location = location, Offset = offset, Limit = limit };

            SendInformativeMessage("upload.getFile", obj, callback, faultCallback);
        }
	}
}
