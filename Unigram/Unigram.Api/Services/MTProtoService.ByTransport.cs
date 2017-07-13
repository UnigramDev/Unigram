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
using Telegram.Api.Native;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };

            Debug.WriteLine("Sending " + "upload.getFile");

            var flags = RequestFlag.ForceDownload | RequestFlag.FailOnServerError;

            var connectionManager = ConnectionManager.Instance;
            var messageToken = connectionManager.SendRequest(obj, (message, ex) =>
            {
                if (message.Object is TLRPCError error)
                {
                    faultCallback?.Invoke(error);
                }
                else if (message.Object is TLUnparsedObject unparsed)
                {
                    callback?.Invoke(TLFactory.Read<TLUploadFileBase>(unparsed.Reader, unparsed.Constructor));
                }
                else
                {
                    callback?.Invoke((TLUploadFileBase)(object)message.Object);
                }
            },
            null, dcId, ConnectionType.Download, flags | RequestFlag.Immediate);
        }

        public void GetWebFileAsync(int dcId, TLInputWebFileLocation location, int offset, int limit, Action<TLUploadWebFile> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadGetWebFile { Location = location, Offset = offset, Limit = limit };

            Debug.WriteLine("Sending " + "upload.getWebFile");

            var flags = RequestFlag.ForceDownload | RequestFlag.FailOnServerError;

            var connectionManager = ConnectionManager.Instance;
            var messageToken = connectionManager.SendRequest(obj, (message, ex) =>
            {
                if (message.Object is TLRPCError error)
                {
                    faultCallback?.Invoke(error);
                }
                else if (message.Object is TLUnparsedObject unparsed)
                {
                    callback?.Invoke(TLFactory.Read<TLUploadWebFile>(unparsed.Reader, unparsed.Constructor));
                }
                else
                {
                    callback?.Invoke((TLUploadWebFile)(object)message.Object);
                }
            },
            null, dcId, ConnectionType.Download, flags | RequestFlag.Immediate);
        }

        public void SendRequestAsync<T>(string caption, TLObject obj, int dcId, bool cdn, Action<T> callback, Action<TLRPCError> faultCallback = null)
        {
            Debug.WriteLine("Sending " + caption);

            var flags = RequestFlag.ForceDownload | RequestFlag.FailOnServerError;
            if (cdn)
            {
                flags |= RequestFlag.EnableUnauthorized;
            }

            var connectionManager = ConnectionManager.Instance;
            var messageToken = connectionManager.SendRequest(obj, (message, ex) =>
            {
                if (message.Object is TLRPCError error)
                {
                    faultCallback?.Invoke(error);
                }
                else if (message.Object is TLUnparsedObject unparsed)
                {
                    callback?.Invoke(TLFactory.Read<T>(unparsed.Reader, unparsed.Constructor));
                }
                else
                {
                    callback?.Invoke((T)(object)message.Object);
                }
            },
            null, dcId, ConnectionType.Download, flags | RequestFlag.Immediate);
        }
    }
}
