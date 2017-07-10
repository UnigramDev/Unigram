using System;
using System.Threading;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Help;
using Telegram.Api.TL.Help.Methods;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        /// <summary>
        /// Список доступных серверов, максимальный размер участников беседы и др.
        /// </summary>
	    private TLConfig _config;

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

            SendInformativeMessage<TLHelpSupport>("help.getSupport", obj, 
                result =>
                {
                    _cacheService.SyncUser(result.User, _ => { });
                    callback(result);
                }, 
                faultCallback);
        }

        public void GetAppChangelogAsync(string prevAppVersion, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetAppChangelog();

            SendInformativeMessage("help.getAppChangelog", obj, callback, faultCallback);
        }
	}
}
