using System;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
	    public void LogOutAsync(Action callback)
	    {
	        _cacheService.ClearAsync(callback);

            //try to close session
            LogOutCallback(null, null);
	    }

        public void CheckPhoneAsync(string phoneNumber, Action<TLAuthCheckedPhone> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthCheckPhone { PhoneNumber = phoneNumber };

            SendInformativeMessage("auth.checkPhone", obj, callback, faultCallback);
	    }

        public void SendCodeCallback(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthSendCode
            {
                Flags = 0,
                PhoneNumber = phoneNumber,
                CurrentNumber = currentNumber,
                ApiId = Constants.ApiId,
                ApiHash = Constants.ApiHash
            };

            SendInformativeMessage("auth.sendCode", obj, callback, faultCallback, 3);
        }

        public void ResendCodeCallback(string phoneNumber, string phoneCodeHash, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthResendCode { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

            SendInformativeMessage("auth.resendCode", obj, callback, faultCallback);
        }

        public void CancelCodeCallback(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthCancelCode { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

            SendInformativeMessage("auth.cancelCode", obj, callback, faultCallback);
        }

        // Fela: DEPRECATED
        //public void SendCallAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        //{
        //    var obj = new TLSendCall { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

        //    SendInformativeMessage("auth.sendCall", obj, callback, faultCallback);
        //}

        public void SignUpCallback(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthSignUp { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode, FirstName = firstName, LastName = lastName };

            SendInformativeMessage<TLAuthAuthorization>("auth.signUp", obj,
                auth =>
                {
                    _cacheService.SyncUser(auth.User, result => { });
                    callback(auth);
                },
                faultCallback);
	    }

        public void SignInCallback(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthSignIn { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode};

            SendInformativeMessage<TLAuthAuthorization>("auth.signIn", obj,
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

        public void LogOutCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthLogOut();

            const string methodName = "auth.logOut";
            Logs.Log.Write(methodName);
            SendInformativeMessage<bool>(methodName, obj,
                result =>
                {
                    Logs.Log.Write(string.Format("{0} result={1}", methodName, result));
                    callback?.Invoke(result);
                }, 
                error =>
                {
                    Logs.Log.Write(string.Format("{0} error={1}", methodName, error));
                    faultCallback?.Invoke(error);
                });
        }

        public void SendInvitesAsync(TLVector<string> phoneNumbers, string message, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthSendInvites { PhoneNumbers = phoneNumbers, Message = message };

            SendInformativeMessage("auth.sendInvites", obj, callback, faultCallback);
	    }

	    public void ExportAuthorizationAsync(int dcId, Action<TLAuthExportedAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthExportAuthorization { DCId = dcId };

            SendInformativeMessage("auth.exportAuthorization dc_id=" + dcId, obj, callback, faultCallback);
	    }

	    public void ImportAuthorizationAsync(int id, byte[] bytes, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthImportAuthorization { Id = id, Bytes = bytes };

            SendInformativeMessage("auth.importAuthorization id=" + id, obj, callback, faultCallback);
	    }

        public void ImportAuthorizationByTransportAsync(ITransport transport, int id, byte[] bytes, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthImportAuthorization { Id = id, Bytes = bytes };

            SendInformativeMessageByTransport(transport, "auth.importAuthorization dc_id=" + transport.DCId, obj, callback, faultCallback);
        }

        public void ResetAuthorizationsCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAuthResetAuthorizations();

            SendInformativeMessage("auth.resetAuthorizations", obj, callback, faultCallback);
        }
	}
}
