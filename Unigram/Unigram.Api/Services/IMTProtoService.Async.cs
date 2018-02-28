using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
    {
        Task<MTProtoResponse<TLPaymentsPaymentResultBase>> SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials);
        Task<MTProtoResponse<TLPaymentsValidatedRequestedInfo>> ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save);

        Task<MTProtoResponse<TLAccountTmpPassword>> GetTmpPasswordAsync(byte[] hash, int period);

        Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash);
        Task<MTProtoResponse<TLAuthSentCode>> SendConfirmPhoneCodeAsync(string hash, bool currentNumber);
        Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber);
        Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync();
        Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync();
        Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName);
        Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason);
        Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode);
        Task<MTProtoResponse<TLAuthSentCode>> ResendCodeAsync(string phoneNumber, string phoneCodeHash);
    }
}
