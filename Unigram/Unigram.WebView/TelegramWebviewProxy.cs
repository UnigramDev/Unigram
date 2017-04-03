using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation.Metadata;

namespace Unigram.Webview
{
    [AllowForWeb]
    public sealed class TelegramWebviewProxy
    {
        private TelegramWebviewProxyDelegate _callback;

        public TelegramWebviewProxy(TelegramWebviewProxyDelegate callback)
        {
            _callback = callback;
        }

        public void PostEvent(string eventName, string eventData)
        {
            if (eventName.Equals("payment_form_submit"))
            {
                try
                {
                    var json = JsonObject.Parse(eventData);
                    var response = json.GetNamedValue("credentials");
                    var title = json.GetNamedString("title", string.Empty);

                    _callback.Invoke(title, response.Stringify());
                }
                catch
                {
                    _callback.Invoke(null, eventData);
                }
            }
        }
    }

    public delegate void TelegramWebviewProxyDelegate(string title, string credentials);
}
