using System;
using System.Threading;
using Telegram.Api.Helpers;
using Telegram.Api.Native;
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

            const string caption = "help.getConfig";
            SendInformativeMessage<TLConfig>(caption, obj,
                result =>
                {
                    callback(result);
                },
                faultCallback);
        }

        public void GetTermsOfServiceAsync(string langCode, Action<TLHelpTermsOfService> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetTermsOfService();

            const string caption = "help.getTermsOfService";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetNearestDCAsync(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetNearestDC();

            const string caption = "help.getNearestDc";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetInviteTextAsync(string langCode, Action<TLHelpInviteText> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetInviteText();

            const string caption = "help.getInviteText";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void GetSupportAsync(Action<TLHelpSupport> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLHelpGetSupport();

            const string caption = "help.getSupport";
            SendInformativeMessage<TLHelpSupport>(caption, obj,
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

            const string caption = "help.getAppChangelog";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
