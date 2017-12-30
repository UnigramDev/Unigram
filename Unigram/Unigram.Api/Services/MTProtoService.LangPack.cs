using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.LangPack.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetDifferenceAsync(int fromVersion, Action<TLLangPackDifference> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLangPackGetDifference { FromVersion = fromVersion };

            const string caption = "langpack.getDifference";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetLangPackAsync(string langCode, Action<TLLangPackDifference> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLangPackGetLangPack { LangCode = langCode };

            const string caption = "langpack.getLangPack";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetLanguagesAsync(Action<TLVector<TLLangPackLanguage>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLangPackGetLanguages();

            const string caption = "langpack.getLanguages";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetStringsAsync(string langCode, TLVector<string> keys, Action<TLVector<TLLangPackStringBase>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLangPackGetStrings { LangCode = langCode };

            const string caption = "langpack.getStrings";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
