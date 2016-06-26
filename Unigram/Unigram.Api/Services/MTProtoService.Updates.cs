using System;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Methods.Updates;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public Task<MTProtoResponse<TLUpdatesState>> GetStateAsync()
        {
            return SendInformativeMessage<TLUpdatesState>("updates.getState", new TLUpdatesGetState());
        }
        public async void GetStateCallbackAsync(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null)
        {
            var result = await SendInformativeMessage<TLUpdatesState>("updates.getState", new TLUpdatesGetState());
            if (result?.IsSucceeded == true)
            {
                callback?.Invoke(result.Value);
            }
            else
            {
                faultCallback?.Invoke(result?.Error);
            }
        }

        public Task<MTProtoResponse<TLUpdatesState>> GetStateWithoutUpdatesAsync()
        {
            return SendInformativeMessage<TLUpdatesState>("updates.getState", new TLInvokeWithoutUpdates { Query = new TLUpdatesGetState() });
        }

        public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceAsync(int pts, int date, int qts)
        {
            return SendInformativeMessage<TLUpdatesDifferenceBase>("updates.getDifference", new TLUpdatesGetDifference { Date = date, Pts = pts, Qts = qts });
        }
        public async void GetDifferenceCallbackAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var result = await SendInformativeMessage<TLUpdatesDifferenceBase>("updates.getDifference", new TLUpdatesGetDifference { Date = date, Pts = pts, Qts = qts });
            if (result?.IsSucceeded == true)
            {
                callback?.Invoke(result.Value);
            }
            else
            {
                faultCallback?.Invoke(result?.Error);
            }
        }

        public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts)
        {
            return SendInformativeMessage<TLUpdatesDifferenceBase>("updates.getDifference", new TLInvokeWithoutUpdates { Query = new TLUpdatesGetDifference { Date = date, Pts = pts, Qts = qts } });
        }
    }
}
