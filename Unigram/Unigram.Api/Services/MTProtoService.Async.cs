using System.Diagnostics;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsPaymentResultBase>> SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsPaymentResultBase>>();
            SendPaymentFormAsync(msgId, infoId, optionId, credentials, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentResultBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentResultBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsValidatedRequestedInfo>> ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsValidatedRequestedInfo>>();
            ValidateRequestedInfoAsync(msgId, info, save, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsValidatedRequestedInfo>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsValidatedRequestedInfo>(faultCallback));
            });
            return tsc.Task;
        }
    }
}
