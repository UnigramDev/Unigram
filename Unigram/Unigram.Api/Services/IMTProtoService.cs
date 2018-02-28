using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Payments;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
    {
        string Country { get; }
        event EventHandler<CountryEventArgs> GotUserCountry;

        // auth
        void ResendCodeAsync(string phoneNumber, string phoneCodeHash, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void CancelCodeAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Deprecated void SendCallAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);

        // account
        void GetTmpPasswordAsync(byte[] hash, int period, Action<TLAccountTmpPassword> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordAsync(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordSettingsAsync(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null);
        void CheckPasswordAsync(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void RequestPasswordRecoveryAsync(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null);
        void SendConfirmPhoneCodeAsync(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);

        // payments
        void ClearSavedInfoAsync(bool info, bool credentials, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials, Action<TLPaymentsPaymentResultBase> callback, Action<TLRPCError> faultCallback = null);
        void ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null);
    }
}
