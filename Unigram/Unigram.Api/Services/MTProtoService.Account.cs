using System;
using System.Threading;
#if WIN_RT
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Functions.Account;
using TLUpdateUserName = Telegram.Api.TL.Account.TLUpdateUserName;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
	    public event EventHandler CheckDeviceLocked;

	    protected virtual void RaiseCheckDeviceLocked()
	    {
	        var handler = CheckDeviceLocked;
	        if (handler != null) handler(this, EventArgs.Empty);
	    }

	    private void CheckDeviceLockedInternal(object state)
        {
            RaiseCheckDeviceLocked();
        }

        public void ReportPeerAsync(TLInputPeerBase peer, TLInputReportReasonBase reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReportPeer { Peer = peer, Reason = reason };

            SendInformativeMessage("account.reportPeer", obj, callback, faultCallback);
        }

	    public void DeleteAccountAsync(TLString reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLDeleteAccount { Reason = reason };

            SendInformativeMessage("account.deleteAccount", obj, callback, faultCallback);
	    }

        public void UpdateDeviceLockedAsync(TLInt period, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdateDeviceLocked{ Period = period };

            SendInformativeMessage("account.updateDeviceLocked", obj, callback, faultCallback);
        }

	    public void GetWallpapersAsync(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLGetWallPapers();

            SendInformativeMessage("account.getWallpapers", obj, callback, faultCallback);
	    }

        public void SendChangePhoneCodeAsync(TLString phoneNumber, TLString currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSendChangePhoneCode { Flags = new TLInt(0), PhoneNumber = phoneNumber, CurrentNumber = currentNumber };

            SendInformativeMessage("account.sendChangePhoneCode", obj, callback, faultCallback);
        }

        public void ChangePhoneAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChangePhone { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            SendInformativeMessage<TLUserBase>("account.changePhone", obj, user => _cacheService.SyncUser(user, callback.SafeInvoke), faultCallback);
        }

        public void RegisterDeviceAsync(TLInt tokenType, TLString token, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            if (_activeTransport.AuthKey == null)
            {
                faultCallback.SafeInvoke(new TLRPCError
                {
                    Code = new TLInt(404),
                    Message = new TLString("Service is not initialized to register device")
                });

                return;
            }

            var obj = new TLRegisterDevice
            {
                //TokenType = new TLInt(3),   // MPNS
                //TokenType = new TLInt(8),   // WNS
                TokenType = tokenType,
                Token = token
            };

            const string methodName = "account.registerDevice";
            Logs.Log.Write(string.Format("{0} {1}", methodName, obj));
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

        public void UnregisterDeviceAsync(TLInt tokenType, TLString token, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUnregisterDevice
            {
                //TokenType = new TLInt(3),   // MPNS
                //TokenType = new TLInt(8),   // WNS
                TokenType = tokenType,
                Token = token
            };

            const string methodName = "account.unregisterDevice";
            Logs.Log.Write(string.Format("{0} {1}", methodName, obj));
            SendInformativeMessage<TLBool>("account.unregisterDevice", obj,
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

        public void GetNotifySettingsAsync(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetNotifySettings{ Peer = peer };

            SendInformativeMessage("account.getNotifySettings", obj, callback, faultCallback);
        }

        public void ResetNotifySettingsAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            Execute.ShowDebugMessage(string.Format("account.resetNotifySettings"));

            var obj = new TLResetNotifySettings();

            SendInformativeMessage("account.resetNotifySettings", obj, callback, faultCallback);
        }

	    public void UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            //Execute.ShowDebugMessage(string.Format("account.updateNotifySettings peer=[{0}] settings=[{1}]", peer, settings));

            var obj = new TL.Functions.Account.TLUpdateNotifySettings { Peer = peer, Settings = settings };

            SendInformativeMessage("account.updateNotifySettings", obj, callback, faultCallback);
        }

        public void UpdateProfileAsync(TLString firstName, TLString lastName, TLString about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdateProfile { FirstName = firstName, LastName = lastName, About = about };

            SendInformativeMessage<TLUserBase>("account.updateProfile", obj, result => _cacheService.SyncUser(result, callback), faultCallback);
        }

        public void UpdateStatusAsync(TLBool offline, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            if (_activeTransport.AuthKey == null) return;

#if WIN_RT
            if (_deviceInfo != null && _deviceInfo.IsBackground)
            {
                var message = string.Format("::{0} {1} account.updateStatus {2}", _deviceInfo.BackgroundTaskName, _deviceInfo.BackgroundTaskId, offline);
                Logs.Log.Write(message);
#if DEBUG
                AddToast("task", message);
#endif
            }
#endif
            var obj = new TLUpdateStatus { Offline = offline };
            System.Diagnostics.Debug.WriteLine("account.updateStatus offline=" + offline.Value);
            SendInformativeMessage("account.updateStatus", obj, callback, faultCallback);
        }
#if WIN_RT
        public static void AddToast(string caption, string message)
        {
            var toastNotifier = ToastNotificationManager.CreateToastNotifier();

            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            SetText(toastXml, caption, message);

            try
            {
                var toast = new ToastNotification(toastXml);
                //RemoveToastGroup(group);
                toastNotifier.Show(toast);
            }
            catch (Exception ex)
            {
                Logs.Log.Write(ex.ToString());
            }
        }

        private static void SetText(XmlDocument document, string caption, string message)
        {
            var toastTextElements = document.GetElementsByTagName("text");
            toastTextElements[0].InnerText = caption ?? string.Empty;
            toastTextElements[1].InnerText = message ?? string.Empty;
        }
#endif

	    public void CheckUsernameAsync(TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLCheckUsername { Username = username };

            SendInformativeMessage("account.checkUsername", obj, callback, faultCallback);
	    }

	    public void UpdateUsernameAsync(TLString username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLUpdateUserName { Username = username };

            SendInformativeMessage("account.updateUsername", obj, callback, faultCallback);
	    }

	    public void GetAccountTTLAsync(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLGetAccountTTL();

            SendInformativeMessage("account.getAccountTTL", obj, callback, faultCallback);
	    }

        public void SetAccountTTLAsync(TLAccountDaysTTL ttl, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSetAccountTTL{TTL = ttl};

            SendInformativeMessage("account.setAccountTTL", obj, callback, faultCallback);
        }

        public void DeleteAccountTTLAsync(TLString reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteAccount { Reason = reason };

            SendInformativeMessage("account.deleteAccount", obj, callback, faultCallback);
        }

        public void GetPrivacyAsync(TLInputPrivacyKeyBase key, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetPrivacy { Key = key };

            SendInformativeMessage("account.getPrivacy", obj, callback, faultCallback);
        }

        public void SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSetPrivacy { Key = key, Rules = rules };

            SendInformativeMessage("account.setPrivacy", obj, callback, faultCallback);
        }

        public void GetAuthorizationsAsync(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetAuthorizations();

            SendInformativeMessage("account.getAuthorizations", obj, callback, faultCallback);
        }

        public void ResetAuthorizationAsync(TLLong hash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLResetAuthorization { Hash = hash };

            SendInformativeMessage("account.resetAuthorization", obj, callback, faultCallback);
        }

	    public void GetPasswordAsync(Action<TLPasswordBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLGetPassword();

            SendInformativeMessage("account.getPassword", obj, callback, faultCallback);
	    }

	    public void GetPasswordSettingsAsync(TLString currentPasswordHash, Action<TLPasswordSettings> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLGetPasswordSettings{ CurrentPasswordHash = currentPasswordHash };

            SendInformativeMessage("account.getPasswordSettings", obj, callback, faultCallback);
	    }

	    public void UpdatePasswordSettingsAsync(TLString currentPasswordHash, TLPasswordInputSettings newSettings, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLUpdatePasswordSettings { CurrentPasswordHash = currentPasswordHash, NewSettings = newSettings };

            SendInformativeMessage("account.updatePasswordSettings", obj, callback, faultCallback);
	    }

	    public void CheckPasswordAsync(TLString passwordHash, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLCheckPassword { PasswordHash = passwordHash };

            SendInformativeMessage("account.checkPassword", obj, callback, faultCallback);
	    }

	    public void RequestPasswordRecoveryAsync(Action<TLPasswordRecovery> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLRequestPasswordRecovery();

            SendInformativeMessage("account.requestPasswordRecovery", obj, callback, faultCallback);
	    }

	    public void RecoverPasswordAsync(TLString code, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var obj = new TLRecoverPassword {Code = code};

            SendInformativeMessage("account.recoverPassword", obj, callback, faultCallback);
	    }


	    public void ConfirmPhoneAsync(TLString phoneCodeHash, TLString phoneCode, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLConfirmPhone { PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            SendInformativeMessage("account.confirmPhone", obj, callback, faultCallback);
	    }

	    public void SendConfirmPhoneCodeAsync(TLString hash, TLBool currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSendConfirmPhoneCode { Flags = new TLInt(0), Hash = hash, CurrentNumber = currentNumber };

            SendInformativeMessage("account.sendConfirmPhoneCode", obj, callback, faultCallback);
	    }
	}
}
