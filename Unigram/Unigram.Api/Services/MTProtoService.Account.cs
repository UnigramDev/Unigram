using System;
using System.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.TL.Methods.Account;

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

        public Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason)
        {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            return SendInformativeMessage<bool>("account.deleteAccount", obj);
        }

        public Task<MTProtoResponse<bool>> UpdateDeviceLockedAsync(int period)
        {
            var obj = new TLAccountUpdateDeviceLocked { Period = period };

            return SendInformativeMessage<bool>("account.updateDeviceLocked", obj);
        }

        public Task<MTProtoResponse<TLVector<TLWallPaperBase>>> GetWallpapersAsync()
        {
            var obj = new TLAccountGetWallPapers();

            return SendInformativeMessage<TLVector<TLWallPaperBase>>("account.getWallpapers", obj);
        }

        // TODO: VERIFY RETURN TYPE
        public Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber)
        {
            var obj = new TLAccountSendChangePhoneCode { PhoneNumber = phoneNumber };

            return SendInformativeMessage<TLAuthSentCode>("account.sendChangePhoneCode", obj);
        }

        public async Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        {
            var obj = new TLAccountChangePhone { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            var result = await SendInformativeMessage<TLUserBase>("account.changePhone", obj);
            var task = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            _cacheService.SyncUser(result.Value, (callback) =>
            {
                task.SetResult(new MTProtoResponse<TLUserBase>(callback));
            });
            return await task.Task;
        }

        public async Task<MTProtoResponse<bool>> RegisterDeviceAsync(int tokenType, string token)
        {
            if (_activeTransport.AuthKey == null)
            {
                return new MTProtoResponse<bool>(new TLRPCError { ErrorCode = 404, ErrorMessage = "Service is not initialized to register device" });
            }

            var systemVersion = _deviceInfo.SystemVersion;
#if !WP8
            if (!systemVersion.StartsWith("7."))
            {
                systemVersion = "7.0.0.0";
            }
#endif

            var obj = new TLAccountRegisterDevice
            {
                //TokenType = new int?(3),   // MPNS
                //TokenType = new int?(8),   // WNS
                TokenType = tokenType,
                Token = token,
                //TODO: where should we put these info?
                //DeviceModel = _deviceInfo.Model,
                //SystemVersion = systemVersion,
                //AppVersion = _deviceInfo.AppVersion,
                //AppSandbox = false,
                //LangCode = Utils.CurrentUICulture()
            };

            //Execute.ShowDebugMessage("account.registerDevice " + obj);

            return await SendInformativeMessage<bool>("account.registerDevice", obj);
        }

        public Task<MTProtoResponse<bool>> UnregisterDeviceAsync(string token)
        {
            var obj = new TLAccountUnregisterDevice
            {
                TokenType = 3,
                Token = token
            };

            return SendInformativeMessage<bool>("account.unregisterDevice", obj);
        }

        public Task<MTProtoResponse<TLPeerNotifySettingsBase>> GetNotifySettingsAsync(TLInputNotifyPeerBase peer)
        {
            var obj = new TLAccountGetNotifySettings { Peer = peer };

            return SendInformativeMessage<TLPeerNotifySettingsBase>("account.getNotifySettings", obj);
        }

        public Task<MTProtoResponse<bool>> ResetNotifySettingsAsync()
        {
            Execute.ShowDebugMessage(string.Format("account.resetNotifySettings"));

            var obj = new TLAccountResetNotifySettings();

            return SendInformativeMessage<bool>("account.resetNotifySettings", obj);
        }

        public Task<MTProtoResponse<bool>> UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings)
        {
            //Execute.ShowDebugMessage(string.Format("account.updateNotifySettings peer=[{0}] settings=[{1}]", peer, settings));

            var obj = new TLAccountUpdateNotifySettings { Peer = peer, Settings = settings };

            return SendInformativeMessage<bool>("account.updateNotifySettings", obj);
        }

        public async Task<MTProtoResponse<TLUserBase>> UpdateProfileAsync(string firstName, string lastName)
        {
            var obj = new TLAccountUpdateProfile { FirstName = firstName, LastName = lastName };

            var result = await SendInformativeMessage<TLUserBase>("account.updateProfile", obj);
            var task = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            _cacheService.SyncUser(result.Value, (callback) =>
            {
                task.SetResult(new MTProtoResponse<TLUserBase>(callback));
            });
            return await task.Task;
        }

        public Task<MTProtoResponse<bool>> UpdateStatusAsync(bool offline)
        {
            if (_activeTransport.AuthKey == null)
            {
                return null;
            }

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

            return SendInformativeMessage<bool>("account.updateStatus", obj);
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

        public Task<MTProtoResponse<bool>> CheckUsernameAsync(string username)
        {
            var obj = new TLAccountCheckUsername { Username = username };

            return SendInformativeMessage<bool>("account.checkUsername", obj);
        }

        public Task<MTProtoResponse<TLUserBase>> UpdateUsernameAsync(string username)
        {
            var obj = new TLUpdateUserName { Username = username };

            return SendInformativeMessage<TLUserBase>("account.updateUsername", obj);
        }

        public Task<MTProtoResponse<TLAccountDaysTTL>> GetAccountTTLAsync()
        {
            var obj = new TLAccountGetAccountTTL();

            return SendInformativeMessage<TLAccountDaysTTL>("account.getAccountTTL", obj);
        }

        public Task<MTProtoResponse<bool>> SetAccountTTLAsync(TLAccountDaysTTL ttl)
        {
            var obj = new TLAccountSetAccountTTL { TTL = ttl };

            return SendInformativeMessage<bool>("account.setAccountTTL", obj);
        }

        public Task<MTProtoResponse<bool>> DeleteAccountTTLAsync(string reason)
        {
            var obj = new TLAccountDeleteAccount { Reason = reason };

            return SendInformativeMessage<bool>("account.deleteAccount", obj);
        }

        public Task<MTProtoResponse<TLAccountPrivacyRules>> GetPrivacyAsync(TLInputPrivacyKeyBase key)
        {
            var obj = new TLAccountGetPrivacy { Key = key };

            return SendInformativeMessage<TLAccountPrivacyRules>("account.getPrivacy", obj);
        }

        public Task<MTProtoResponse<TLInputPrivacyRuleBase>> SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules)
        {
            var obj = new TLAccountSetPrivacy { Key = key, Rules = rules };

            return SendInformativeMessage<TLInputPrivacyRuleBase>("account.setPrivacy", obj);
        }

        public Task<MTProtoResponse<TLAccountAuthorizations>> GetAuthorizationsAsync()
        {
            var obj = new TLAccountGetAuthorizations();

            return SendInformativeMessage<TLAccountAuthorizations>("account.getAuthorizations", obj);
        }

        public Task<MTProtoResponse<bool>> ResetAuthorizationAsync(long hash)
        {
            var obj = new TLAccountResetAuthorization { Hash = hash };

            return SendInformativeMessage<bool>("account.resetAuthorization", obj);
        }

        public Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync()
        {
            var obj = new TLAccountGetPassword();

            return SendInformativeMessage<TLAccountPasswordBase>("account.getPassword", obj);
        }

        public Task<MTProtoResponse<TLAccountPasswordSettings>> GetPasswordSettingsAsync(byte[] currentPasswordHash)
        {
            var obj = new TLAccountGetPasswordSettings { CurrentPasswordHash = currentPasswordHash };

            return SendInformativeMessage<TLAccountPasswordSettings>("account.getPasswordSettings", obj);
        }

        public Task<MTProtoResponse<bool>> UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings)
        {
            var obj = new TLAccountUpdatePasswordSettings { CurrentPasswordHash = currentPasswordHash, NewSettings = newSettings };

            return SendInformativeMessage<bool>("account.updatePasswordSettings", obj);
        }

        public Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash)
        {
            var obj = new TLAuthCheckPassword { PasswordHash = passwordHash };

            return SendInformativeMessage<TLAuthAuthorization>("account.checkPassword", obj);
        }

        public Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync()
        {
            var obj = new TLAuthRequestPasswordRecovery();

            return SendInformativeMessage<TLAuthPasswordRecovery>("account.requestPasswordRecovery", obj);
        }

        public Task<MTProtoResponse<TLAuthAuthorization>> RecoverPasswordAsync(string code)
        {
            var obj = new TLAuthRecoverPassword { Code = code };

            return SendInformativeMessage<TLAuthAuthorization>("account.recoverPassword", obj);
        }
    }
}
