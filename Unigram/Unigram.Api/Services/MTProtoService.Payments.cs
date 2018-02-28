using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void ClearSavedInfoAsync(bool info, bool credentials, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials, Action<TLPaymentsPaymentResultBase> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }
        
        public void ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }
    }
}
