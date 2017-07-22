using System;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Updates;
using Telegram.Api.TL.Updates.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetStateAsync(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdatesGetState();

            const string caption = "updates.getState";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetStateWithoutUpdatesAsync(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLInvokeWithoutUpdates { Query = new TLUpdatesGetState() };

            const string caption = "updates.getState";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetDifferenceAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdatesGetDifference { Date = date, Pts = pts, Qts = qts };

            const string caption = "updates.getDifference";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdatesGetDifference { Date = date, Pts = pts, Qts = qts };

            const string caption = "updates.getDifference";
            SendInformativeMessage(caption, new TLInvokeWithoutUpdates { Query = obj }, callback, faultCallback);
        }
    }
}
