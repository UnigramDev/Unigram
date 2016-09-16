using System;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Account;
using Telegram.Api.TL.Functions.Auth;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
	    public void LogOutAsync(Action callback)
	    {
	        _cacheService.ClearAsync(callback);

            //try to close session
            LogOutAsync(null, null);
	    }

        public void CheckPhoneAsync(TLString phoneNumber, Action<TLCheckedPhoneBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLCheckPhone { PhoneNumber = phoneNumber };

            SendInformativeMessage("auth.checkPhone", obj, callback, faultCallback);
	    }

        public void SendCodeAsync(TLString phoneNumber, TLString currentNumber, Action<TLSentCodeBase> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSendCode
            {
                Flags = new TLInt(0),
                PhoneNumber = phoneNumber,
                CurrentNumber = currentNumber,
                ApiId = new TLInt(Constants.ApiId),
                ApiHash = new TLString(Constants.ApiHash)
            };

            SendInformativeMessage("auth.sendCode", obj, callback, faultCallback, 3);
        }

        public void ResendCodeAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLResendCode { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

            SendInformativeMessage("auth.resendCode", obj, callback, faultCallback);
        }

        public void CancelCodeAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLCancelCode { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

            SendInformativeMessage("auth.cancelCode", obj, callback, faultCallback);
        }

        public void SendCallAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSendCall { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

            SendInformativeMessage("auth.sendCall", obj, callback, faultCallback);
        }

	    public void SignUpAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, TLString firstName, TLString lastName, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSignUp { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode, FirstName = firstName, LastName = lastName };

            SendInformativeMessage<TLAuthorization>("auth.signUp", obj,
                auth =>
                {
                    _cacheService.SyncUser(auth.User, result => { });
                    callback(auth);
                },
                faultCallback);
	    }

        public void SignInAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSignIn{ PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode};

            SendInformativeMessage<TLAuthorization>("auth.signIn", obj,
                auth =>
                {
                    _cacheService.SyncUser(auth.User, result => { }); 
                    callback(auth);
                }, 
                faultCallback);
        }

	    public void CancelSignInAsync()
	    {
	        CancelDelayedItemsAsync(true);
	    }

        public void LogOutAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLogOut();

            const string methodName = "auth.logOut";
            Logs.Log.Write(methodName);
            SendInformativeMessage<TLBool>(methodName, obj,
                result =>
                {
                    Logs.Log.Write(string.Format("{0} result={1}", methodName, result));
                    callback.SafeInvoke(result);
                }, 
                error =>
                {
                    Logs.Log.Write(string.Format("{0} error={1}", methodName, error));
                    faultCallback.SafeInvoke(error);
                });
        }

        public void SendInvitesAsync(TLVector<TLString> phoneNumbers, TLString message, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSendInvites{ PhoneNumbers = phoneNumbers, Message = message };

            SendInformativeMessage("auth.sendInvites", obj, callback, faultCallback);
	    }

	    public void ExportAuthorizationAsync(TLInt dcId, Action<TLExportedAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLExportAuthorization { DCId = dcId };

            SendInformativeMessage("auth.exportAuthorization dc_id=" + dcId, obj, callback, faultCallback);
	    }

	    public void ImportAuthorizationAsync(TLInt id, TLString bytes, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLImportAuthorization { Id = id, Bytes = bytes };

            SendInformativeMessage("auth.importAuthorization id=" + id, obj, callback, faultCallback);
	    }

        public void ImportAuthorizationByTransportAsync(ITransport transport, TLInt id, TLString bytes, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLImportAuthorization { Id = id, Bytes = bytes };

            SendInformativeMessageByTransport(transport, "auth.importAuthorization dc_id=" + transport.DCId, obj, callback, faultCallback);
        }

        public void ResetAuthorizationsAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLResetAuthorizations();

            SendInformativeMessage("auth.resetAuthorizations", obj, callback, faultCallback);
        }
	}
}
