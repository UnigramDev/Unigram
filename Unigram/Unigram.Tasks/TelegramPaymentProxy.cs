using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation.Metadata;

namespace Unigram.Tasks
{
    public delegate void TelegramPaymentProxyDelegate(string title, string credentials);

    [AllowForWeb]
    public sealed class TelegramPaymentProxy
    {
        private TelegramPaymentProxyDelegate _callback;

        public TelegramPaymentProxy(TelegramPaymentProxyDelegate callback)
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
}
