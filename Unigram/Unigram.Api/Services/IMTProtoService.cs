using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
    {
        string Country { get; }
        event EventHandler<CountryEventArgs> GotUserCountry;

        // payments
        void ClearSavedInfoAsync(bool info, bool credentials, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials, Action<TLPaymentsPaymentResultBase> callback, Action<TLRPCError> faultCallback = null);
        void ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null);
    }
}
