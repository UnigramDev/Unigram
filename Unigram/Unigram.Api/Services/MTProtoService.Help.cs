using System;
using System.Threading;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Help;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        /// <summary>
        /// Список доступных серверов, максимальный размер участников беседы и др.
        /// </summary>
	    private TLConfig _config = new TLConfig
	    {
            DCOptions = new TLVector<TLDCOption>
            {
                new TLDCOption 
                { 
                    Id = Constants.FirstServerDCId,
                    IpAddress = Constants.FirstServerIpAddress, 
                    Port = Constants.FirstServerPort 
                }
            }
	    };

        public void GetConfigAsync(Action<TLConfig> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetConfig();

            Logs.Log.Write("help.getConfig");

            SendInformativeMessage<TLConfig>("help.getConfig", obj,
                result =>
                {
                    callback(result);
                }, 
                faultCallback);
        }

        private Timer _getConfigTimer;

	    private volatile bool _isGettingConfig;

        private void CheckGetConfig(object state)
        {
            //TLUtils.WriteLine(DateTime.Now.ToLongTimeString() + ": Check Config on Thread " + Thread.CurrentThread.ManagedThreadId, LogSeverity.Error);

            if (_deviceInfo != null && _deviceInfo.IsBackground)
            {
                return;
            }

            if (_isGettingConfig)
            {
                return;
            }

            if (_activeTransport == null)
            {
                return;
            }

            if (_activeTransport.AuthKey == null)
            {
                return;
            }

            var isAuthorized = SettingsHelper.IsAuthorized;
            if (!isAuthorized)
            {
                return;
            }

            var currentTime = TLUtils.DateToUniversalTimeTLInt(ClientTicksDelta, DateTime.Now);


            var config23 = _config as TLConfig;
            if (config23 != null && config23.Expires != null && (config23.Expires > currentTime))
            {
                return;
            }

            //if (_config != null && _config.Date != null && Math.Abs(_config.Date.Value - currentTime.Value) < Constants.GetConfigInterval)
            //{
            //    return;
            //}

            //Execute.ShowDebugMessage("MTProtoService.CheckGetConfig GetConfig");

            _isGettingConfig = true;
            GetConfigAsync(
                result =>
                {
                    //TLUtils.WriteLine(DateTime.Now.ToLongTimeString() + ": help.getConfig", LogSeverity.Error);
                    _config = TLExtensions.Merge(_config, result);
                    SaveConfig();
                    _isGettingConfig = false;
                },
                error =>
                {
                    _isGettingConfig = false;
                    //Execute.ShowDebugMessage("help.getConfig error: " + error);
                });
        }

        public void GetTermsOfServiceAsync(string langCode, Action<TLHelpTermsOfService> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetTermsOfService();

            SendInformativeMessage("help.getTermsOfService", obj, callback, faultCallback);
        }

        public void GetNearestDCAsync(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetNearestDC();

            SendInformativeMessage("help.getNearestDc", obj, callback, faultCallback);
        }

        public void GetInviteTextAsync(string langCode, Action<TLHelpInviteText> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetInviteText();

            SendInformativeMessage("help.getInviteText", obj, callback, faultCallback);
        }

        public void GetSupportAsync( Action<TLHelpSupport> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetSupport();

            SendInformativeMessage("help.getSupport", obj, callback, faultCallback);
        }

        public void GetAppChangelogAsync(string prevAppVersion, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetAppChangelog();

            SendInformativeMessage("help.getAppChangelog", obj, callback, faultCallback);
        }
	}
}
