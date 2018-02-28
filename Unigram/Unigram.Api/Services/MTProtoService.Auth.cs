using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Auth;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
	{
	    public void LogOutAsync(Action callback)
	    {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void SendCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void ResendCodeAsync(string phoneNumber, string phoneCodeHash, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void CancelCodeAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

        public void SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void LogOutAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void SendInvitesAsync(TLVector<string> phoneNumbers, string message, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void ResetAuthorizationsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }
	}
}
