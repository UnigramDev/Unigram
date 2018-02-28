using System.Diagnostics;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
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
        public Task<MTProtoResponse<TLAccountTmpPassword>> GetTmpPasswordAsync(byte[] hash, int period)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountTmpPassword>>();
            GetTmpPasswordAsync(hash, period, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountTmpPassword>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountTmpPassword>(faultCallback));
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

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            CheckPasswordAsync(passwordHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> SendConfirmPhoneCodeAsync(string hash, bool currentNumber)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            SendConfirmPhoneCodeAsync(hash, currentNumber, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            SendChangePhoneCodeAsync(phoneNumber, currentNumber, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthPasswordRecovery>>();
            RequestPasswordRecoveryAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPasswordBase>>();
            GetPasswordAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            DeleteAccountAsync(reason, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        public Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            SignUpAsync(phoneNumber, phoneCodeHash, phoneCode, firstName, lastName, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        public Task<MTProtoResponse<TLAuthSentCode>> ResendCodeAsync(string phoneNumber, string phoneCodeHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            ResendCodeAsync(phoneNumber, phoneCodeHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            ChangePhoneAsync(phoneNumber, phoneCodeHash, phoneCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
            });
            return tsc.Task;
        }
    }
}
