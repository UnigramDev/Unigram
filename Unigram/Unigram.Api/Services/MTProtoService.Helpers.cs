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

        //public void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null, RequestFlag flags = RequestFlag.None, bool immediate = true)
        //{
        //    SendInformativeMessage(caption, obj, callback, faultCallback, null, flags, immediate);
        //}

        public void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null, Action fastCallback = null, RequestFlag flags = RequestFlag.None, bool immediate = true)
        {
            SendInformativeMessage(caption, obj, callback, faultCallback, fastCallback, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, flags, immediate);
        }

        public int SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback, Action fastCallback, int datacenterId, ConnectionType connectionType, RequestFlag flags, bool immediate)
        {
            RequestQuickAckReceivedCallback quick = null;
            if (fastCallback != null)
            {
                quick = () => fastCallback?.Invoke();
            }

            Debug.WriteLine("Sending " + caption);

            if (immediate)
            {
                flags |= RequestFlag.Immediate;
            }

            try
            {
                return _connectionManager.SendRequest(obj, (message, ex) =>
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
                quick, datacenterId, connectionType, flags);
            }
            catch
            {
                return 0;
            }
        }

        public void SendRequestAsync<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null)
        {
            SendInformativeMessage<T>(caption, obj, callback, faultCallback);
        }
    }
}
