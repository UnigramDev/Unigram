using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace Unigram.Tasks
{
    public delegate void TelegramGameProxyDelegate(bool withMyScore);

    [AllowForWeb]
    public sealed class TelegramGameProxy
    {
        private TelegramGameProxyDelegate _callback;

        public TelegramGameProxy(TelegramGameProxyDelegate callback)
        {
            _callback = callback;
        }

        public void PostEvent(string eventName, string eventData)
        {
            if (eventName.Equals("share_game"))
            {
                _callback.Invoke(false);
            }
            else if (eventName.Equals("share_score"))
            {
                _callback.Invoke(true);
            }
        }
    }
}
