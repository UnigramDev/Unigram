using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Help;
using Telegram.Api.TL.Functions.Updates;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void GetStateAsync(Action<TLState> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetState();

            SendInformativeMessage("updates.getState", obj, callback, faultCallback);
        }

        public void GetStateWithoutUpdatesAsync(Action<TLState> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLInvokeWithoutUpdates {Object = new TLGetState()};

            SendInformativeMessage("updates.getState", obj, callback, faultCallback);
        }

        public void GetDifferenceAsync(int? pts, int? date, int? qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDifference{ Date = date, Pts = pts, Qts = qts };

            SendInformativeMessage("updates.getDifference", obj, callback, faultCallback);
        }

        public void GetDifferenceWithoutUpdatesAsync(int? pts, int? date, int? qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDifference { Date = date, Pts = pts, Qts = qts };

            SendInformativeMessage("updates.getDifference", new TLInvokeWithoutUpdates{Object = obj}, callback, faultCallback);
        }
	}
}
