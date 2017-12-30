using System;
using System.Threading;
#if WIN_RT
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Account.Methods;
using Telegram.Api.TL.Auth.Methods;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Account;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;

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
            var obj = new TLAccountGetTmpPassword { PasswordHash = hash, Period = period };

            const string caption = "account.getTmpPassword";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountReportPeer { Peer = peer, Reason = reason };

            const string caption = "account.reportPeer";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

	    public void DeleteAccountAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            const string caption = "account.deleteAccount";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

        public void UpdateDeviceLockedAsync(int period, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountUpdateDeviceLocked { Period = period };

            const string caption = "account.updateDeviceLocked";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

	    public void GetWallpapersAsync(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetWallPapers();

            const string caption = "account.getWallpapers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
	    }

        public void SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSendChangePhoneCode { Flags = 0, PhoneNumber = phoneNumber, CurrentNumber = currentNumber };

            const string caption = "account.sendChangePhoneCode";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountChangePhone { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            const string caption = "account.changePhone";
            SendInformativeMessage<TLUserBase>(caption, obj,
                user =>
                {
                    _cacheService.SyncUser(user, callback);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void RegisterDeviceAsync(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            //if (_activeTransport.AuthKey == null)
            //{
            //    faultCallback?.Invoke(new TLRPCError
            //    {
            //        ErrorCode = 404,
            //        ErrorMessage = "Service is not initialized to register device"
            //    });

            //    return;
            //}

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

        public void UnregisterDeviceAsync(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null)
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
            const string caption = "account.unregisterDevice";
            SendInformativeMessage<bool>(caption, obj,
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

        public void GetNotifySettingsAsync(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetNotifySettings { Peer = peer };

            const string caption = "account.getNotifySettings";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ResetNotifySettingsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            Execute.ShowDebugMessage(string.Format("account.resetNotifySettings"));

            var obj = new TLAccountResetNotifySettings();

            const string caption = "account.resetNotifySettings";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

	    public void UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            //Execute.ShowDebugMessage(string.Format("account.updateNotifySettings peer=[{0}] settings=[{1}]", peer, settings));

            var obj = new TLAccountUpdateNotifySettings { Peer = peer, Settings = settings };

            const string caption = "account.updateNotifySettings";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UpdateProfileAsync(string firstName, string lastName, string about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountUpdateProfile { FirstName = firstName, LastName = lastName, About = about };

            const string caption = "account.updateProfile";
            SendInformativeMessage<TLUserBase>(caption, obj, result => _cacheService.SyncUser(result, callback), faultCallback);
        }

        public void UpdateStatusAsync(bool offline, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            //if (_activeTransport.AuthKey == null) return;
            //if (!SettingsHelper.IsAuthorized) return;

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
            const string caption = "account.updateStatus";
            SendInformativeMessage(caption, obj, callback, faultCallback);
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

	    public void CheckUsernameAsync(string username, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountCheckUsername { Username = username };

            const string caption = "account.checkUsername";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
	    }

	    public void UpdateUsernameAsync(string username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountUpdateUsername { Username = username };

            const string caption = "account.updateUsername";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
	    }

	    public void GetAccountTTLAsync(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetAccountTTL();

            const string caption = "account.getAccountTTL";
            SendInformativeMessage(caption, obj, callback, faultCallback);
	    }

        public void SetAccountTTLAsync(TLAccountDaysTTL ttl, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSetAccountTTL { TTL = ttl};

            const string caption = "account.setAccountTTL";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void DeleteAccountTTLAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            const string caption = "account.deleteAccount";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
        }

        public void GetPrivacyAsync(TLInputPrivacyKeyBase key, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetPrivacy { Key = key };

            const string caption = "account.getPrivacy";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountSetPrivacy { Key = key, Rules = rules };

            const string caption = "account.setPrivacy";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetAuthorizationsAsync(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountGetAuthorizations();

            const string caption = "account.getAuthorizations";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ResetAuthorizationAsync(long hash, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAccountResetAuthorization { Hash = hash };

            const string caption = "account.resetAuthorization";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

	    public void GetPasswordAsync(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetPassword();

            const string caption = "account.getPassword";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

	    public void GetPasswordSettingsAsync(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountGetPasswordSettings { CurrentPasswordHash = currentPasswordHash };

            const string caption = "account.getPasswordSettings";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

	    public void UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountUpdatePasswordSettings { CurrentPasswordHash = currentPasswordHash, NewSettings = newSettings };

            const string caption = "account.updatePasswordSettings";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

	    public void CheckPasswordAsync(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthCheckPassword { PasswordHash = passwordHash };

            const string caption = "auth.checkPassword";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

	    public void RequestPasswordRecoveryAsync(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAuthRequestPasswordRecovery();

            const string caption = "auth.requestPasswordRecovery";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }

	    public void RecoverPasswordAsync(string code, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var obj = new TLAuthRecoverPassword {Code = code};

            const string caption = "auth.recoverPassword";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
	    }


	    public void ConfirmPhoneAsync(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountConfirmPhone { PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            const string caption = "account.confirmPhone";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
	    }

	    public void SendConfirmPhoneCodeAsync(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLAccountSendConfirmPhoneCode { Flags = 0, Hash = hash, CurrentNumber = currentNumber };

            const string caption = "account.sendConfirmPhoneCode";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
	    }
	}
}
