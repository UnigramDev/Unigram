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

        public void SendInformativeMessageInternal<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
            int? maxAttempt = null, // to send delayed items
            Action<int> attemptFailed = null,
            Action fastCallback = null) // to send delayed items
        {
            var connectionManager = ConnectionManager.Instance;
            var messageToken = connectionManager.SendRequest(obj, (message, ex) =>
            {
                if (message.Object is TLRPCError error)
                {
                    faultCallback?.Invoke(error);
                }
                else
                {
                    callback?.Invoke((T)(object)message.Object);
                }
            },
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.FailOnServerError | RequestFlag.WithoutLogin | RequestFlag.TryDifferentDc | RequestFlag.EnableUnauthorized);
        }

        public void SendRequestAsync<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null)
        {
            SendInformativeMessage<T>(caption, obj, callback, faultCallback);
        }

        private void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items
        {
            SendInformativeMessageInternal(caption, obj, callback, faultCallback, maxAttempt, attemptFailed);
        }
    }
}
