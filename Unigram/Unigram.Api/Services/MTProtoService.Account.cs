using System;
using System.Threading;
#if WIN_RT
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Account;
using Telegram.Api.TL.Methods.Auth;

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

        public void ReportPeerCallback(TLInputPeerBase peer, TLReportReasonBase reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountReportPeer { Peer = peer, Reason = reason };

            SendInformativeMessage("account.reportPeer", obj, callback, faultCallback);
        }

	    public void DeleteAccountCallback(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            SendInformativeMessage("account.deleteAccount", obj, callback, faultCallback);
	    }

        public void UpdateDeviceLockedCallback(int period, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountUpdateDeviceLocked { Period = period };

            SendInformativeMessage("account.updateDeviceLocked", obj, callback, faultCallback);
        }

	    public void GetWallpapersCallback(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetWallPapers();

            SendInformativeMessage("account.getWallpapers", obj, callback, faultCallback);
	    }

        public void SendChangePhoneCodeCallback(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSendChangePhoneCode { Flags = 0, PhoneNumber = phoneNumber, CurrentNumber = currentNumber };

            SendInformativeMessage("account.sendChangePhoneCode", obj, callback, faultCallback);
        }

        public void ChangePhoneCallback(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountChangePhone { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            SendInformativeMessage<TLUserBase>("account.changePhone", obj, user => _cacheService.SyncUser(user, callback), faultCallback);
        }

        public void RegisterDeviceCallback(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            if (_activeTransport.AuthKey == null)
            {
                faultCallback?.Invoke(new TLRPCError
                {
                    ErrorCode = 404,
                    ErrorMessage = "Service is not initialized to register device"
                });

                return;
            }

            var obj = new TLAccountRegisterDevice
            {
                //TokenType = 3,   // MPNS
                //TokenType = 8,   // WNS
                TokenType = tokenType,
                Token = token
            };

            const string methodName = "account.registerDevice";
            Logs.Log.Write(string.Format("{0} {1}", methodName, obj));
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

        public void UnregisterDeviceCallback(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountUnregisterDevice
            {
                //TokenType = 3,   // MPNS
                //TokenType = 8,   // WNS
                TokenType = tokenType,
                Token = token
            };

            const string methodName = "account.unregisterDevice";
            Logs.Log.Write(string.Format("{0} {1}", methodName, obj));
            SendInformativeMessage<bool>("account.unregisterDevice", obj,
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

        public void GetNotifySettingsCallback(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetNotifySettings { Peer = peer };

            SendInformativeMessage("account.getNotifySettings", obj, callback, faultCallback);
        }

        public void ResetNotifySettingsCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Execute.ShowDebugMessage(string.Format("account.resetNotifySettings"));

            var obj = new TLAccountResetNotifySettings();

            SendInformativeMessage("account.resetNotifySettings", obj, callback, faultCallback);
        }

	    public void UpdateNotifySettingsCallback(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            //Execute.ShowDebugMessage(string.Format("account.updateNotifySettings peer=[{0}] settings=[{1}]", peer, settings));

            var obj = new TLAccountUpdateNotifySettings { Peer = peer, Settings = settings };

            SendInformativeMessage("account.updateNotifySettings", obj, callback, faultCallback);
        }

        public void UpdateProfileCallback(string firstName, string lastName, string about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountUpdateProfile { FirstName = firstName, LastName = lastName, About = about };

            SendInformativeMessage<TLUserBase>("account.updateProfile", obj, result => _cacheService.SyncUser(result, callback), faultCallback);
        }

        public void UpdateStatusCallback(bool offline, Action<bool> callback, Action<TLRPCError> faultCallback = null)
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
            var obj = new TLAccountUpdateStatus { Offline = offline };
            System.Diagnostics.Debug.WriteLine("account.updateStatus offline=" + offline);
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

	    public void CheckUsernameCallback(string username, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountCheckUsername { Username = username };

            SendInformativeMessage("account.checkUsername", obj, callback, faultCallback);
	    }

	    public void UpdateUsernameCallback(string username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountUpdateUsername { Username = username };

            SendInformativeMessage("account.updateUsername", obj, callback, faultCallback);
	    }

	    public void GetAccountTTLCallback(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetAccountTTL();

            SendInformativeMessage("account.getAccountTTL", obj, callback, faultCallback);
	    }

        public void SetAccountTTLCallback(TLAccountDaysTTL ttl, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSetAccountTTL { TTL = ttl};

            SendInformativeMessage("account.setAccountTTL", obj, callback, faultCallback);
        }

        public void DeleteAccountTTLCallback(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            SendInformativeMessage("account.deleteAccount", obj, callback, faultCallback);
        }

        public void GetPrivacyCallback(TLInputPrivacyKeyBase key, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetPrivacy { Key = key };

            SendInformativeMessage("account.getPrivacy", obj, callback, faultCallback);
        }

        public void SetPrivacyCallback(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSetPrivacy { Key = key, Rules = rules };

            SendInformativeMessage("account.setPrivacy", obj, callback, faultCallback);
        }

        public void GetAuthorizationsCallback(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetAuthorizations();

            SendInformativeMessage("account.getAuthorizations", obj, callback, faultCallback);
        }

        public void ResetAuthorizationCallback(long hash, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountResetAuthorization { Hash = hash };

            SendInformativeMessage("account.resetAuthorization", obj, callback, faultCallback);
        }

	    public void GetPasswordCallback(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetPassword();

            SendInformativeMessage("account.getPassword", obj, callback, faultCallback);
	    }

	    public void GetPasswordSettingsCallback(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetPasswordSettings { CurrentPasswordHash = currentPasswordHash };

            SendInformativeMessage("account.getPasswordSettings", obj, callback, faultCallback);
	    }

	    public void UpdatePasswordSettingsCallback(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountUpdatePasswordSettings { CurrentPasswordHash = currentPasswordHash, NewSettings = newSettings };

            SendInformativeMessage("account.updatePasswordSettings", obj, callback, faultCallback);
	    }

	    public void CheckPasswordCallback(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthCheckPassword { PasswordHash = passwordHash };

            SendInformativeMessage("auth.checkPassword", obj, callback, faultCallback);
	    }

	    public void RequestPasswordRecoveryCallback(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthRequestPasswordRecovery();

            SendInformativeMessage("auth.requestPasswordRecovery", obj, callback, faultCallback);
	    }

	    public void RecoverPasswordCallback(string code, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var obj = new TLAuthRecoverPassword {Code = code};

            SendInformativeMessage("auth.recoverPassword", obj, callback, faultCallback);
	    }


	    public void ConfirmPhoneCallback(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountConfirmPhone { PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            SendInformativeMessage("account.confirmPhone", obj, callback, faultCallback);
	    }

	    public void SendConfirmPhoneCodeCallback(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountSendConfirmPhoneCode { Flags = 0, Hash = hash, CurrentNumber = currentNumber };

            SendInformativeMessage("account.sendConfirmPhoneCode", obj, callback, faultCallback);
	    }
	}
}
