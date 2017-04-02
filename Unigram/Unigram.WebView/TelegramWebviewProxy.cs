using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace Unigram.Webview
{
    [AllowForWeb]
    public sealed class TelegramWebviewProxy
    {
#pragma warning disable IDE1006 // Naming Styles
        public void postEvent(string eventName, string eventData)
#pragma warning restore IDE1006 // Naming Styles
        {
            Debugger.Break();
        }
    }
}
