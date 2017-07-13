using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
#if WINDOWS_PHONE
using System.Globalization;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Help.Methods;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private void PrintCaption(string caption)
        {
            TLUtils.WriteLine(" ");
            //TLUtils.WriteLine("------------------------");
            TLUtils.WriteLine(String.Format("-->>{0}", caption));
            TLUtils.WriteLine("------------------------");
        }

        private readonly object _historyRoot = new object();

        public void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null, RequestFlag flags = 0, Action fastCallback = null)
        {
            RequestQuickAckReceivedCallback quick = null;
            if (fastCallback != null)
            {
                quick = () => fastCallback?.Invoke();
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
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, flags);
        }

        public void SendRequestAsync<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null)
        {
            SendInformativeMessage<T>(caption, obj, callback, faultCallback);
        }
    }
}
