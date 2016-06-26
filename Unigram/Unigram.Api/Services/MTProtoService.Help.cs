using System;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<MTProtoResponse<TLConfig>> GetConfigAsync()
        {
            Logs.Log.Write("help.getConfig");

            return SendInformativeMessage<TLConfig>("help.getConfig", new TLHelpGetConfig());
        }

        private Timer _getConfigTimer;

        private volatile bool _isGettingConfig;

        private async void CheckGetConfig(object state)
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
            if (config23 != null && (config23.Expires > currentTime))
            {
                return;
            }

            //if (_config != null && _config.Date != null && Math.Abs(_config.Date.Value - currentTime.Value) < Constants.GetConfigInterval)
            //{
            //    return;
            //}

            //Execute.ShowDebugMessage("MTProtoService.CheckGetConfig GetConfig");

            _isGettingConfig = true;
            var result = await GetConfigAsync();
            if (result.Error == null)
            {

                //TLUtils.WriteLine(DateTime.Now.ToLongTimeString() + ": help.getConfig", LogSeverity.Error);
                _config = TLExtensions.Merge(_config, result.Value);
                SaveConfig();
                _isGettingConfig = false;
            }
            else
            {
                _isGettingConfig = false;
                //Execute.ShowDebugMessage("help.getConfig error: " + error);
            }
        }

        public Task<MTProtoResponse<TLNearestDC>> GetNearestDCAsync()
        {
            return SendInformativeMessage<TLNearestDC>("help.getNearestDc", new TLHelpGetNearestDC());
        }

        public Task<MTProtoResponse<TLHelpInviteText>> GetInviteTextAsync(string langCode)
        {
            return SendInformativeMessage<TLHelpInviteText>("help.getInviteText", new TLHelpGetInviteText { /*LangCode = langCode*/ });
        }

        public Task<MTProtoResponse<TLHelpSupport>> GetSupportAsync()
        {
            return SendInformativeMessage<TLHelpSupport>("help.getSupport", new TLHelpGetSupport());
        }

        public Task<MTProtoResponse<TLHelpAppChangelogBase>> GetAppChangelogAsync(string deviceModel, string systemVersion, string appVersion, string langCode)
        {
            var obj = new TLHelpGetAppChangelog { /*DeviceModel = deviceModel, SystemVersion = systemVersion, AppVersion = appVersion, LangCode = langCode*/ };
            return SendInformativeMessage<TLHelpAppChangelogBase>("help.getAppChangelog", obj);
        }
    }
}
