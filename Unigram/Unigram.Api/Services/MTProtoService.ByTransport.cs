using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Security;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Auth.Methods;
using Telegram.Api.TL.Upload.Methods;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Upload;
using Telegram.Api.TL.Messages;
using Telegram.Api.Native.TL;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };

        }

        public void GetWebFileAsync(int dcId, TLInputWebFileLocation location, int offset, int limit, Action<TLUploadWebFile> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetWebFile { Location = location, Offset = offset, Limit = limit };

        }

        public void SendRequestAsync<T>(string caption, TLObject obj, int dcId, bool cdn, Action<T> callback, Action<TLRPCError> faultCallback = null)
        {

        }
    }
}
