using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Native;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace Unigram.Tasks
{
    public sealed class ClearCacheTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            var mode = 0;

            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("ClearCacheMode"))
            {
                mode = (int)ApplicationData.Current.LocalSettings.Values["ClearCacheMode"];
            }

            if (mode == 0)
            {
                deferral.Complete();
                return;
            }

            NativeUtils.CleanDirectory(FileUtils.GetTempFileName(string.Empty), mode);
            deferral.Complete();
        }
    }
}
