using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Services;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace Unigram.Tasks
{
    public sealed class NotificationTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
        }
    }
}
