using System;
using System.Threading;
#if WIN_RT
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Account;



namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
	    public event EventHandler CheckDeviceLocked;

	    protected virtual void RaiseCheckDeviceLocked()
	    {
            CheckDeviceLocked?.Invoke(this, EventArgs.Empty);
        }

	    private void CheckDeviceLockedInternal(object state)
        {
            RaiseCheckDeviceLocked();
        }

        public void GetTmpPasswordAsync(byte[] hash, int period, Action<TLAccountTmpPassword> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

	    public void DeleteAccountAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

        public void SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

        public void ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            Logs.Logger.Critical("Invoking legacy method");
        }

	    public void GetPasswordAsync(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void GetPasswordSettingsAsync(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void CheckPasswordAsync(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void RequestPasswordRecoveryAsync(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void RecoverPasswordAsync(string code, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
	        Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void ConfirmPhoneAsync(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }

	    public void SendConfirmPhoneCodeAsync(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
	    {
            Logs.Logger.Critical("Invoking legacy method");
	    }
	}
}
