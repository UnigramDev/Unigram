using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
    {
        Task<MTProtoResponse<TLPaymentsPaymentResultBase>> SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials);
        Task<MTProtoResponse<TLPaymentsValidatedRequestedInfo>> ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save);
    }
}
