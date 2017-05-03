using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;

namespace Unigram.Tasks
{
    public sealed class NotificationTask : IBackgroundTask
    {
        private readonly Mutex _appOpenMutex = new Mutex(false, "TelegramMessenger");

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (_appOpenMutex.WaitOne(0) == false)
            {
                return;
            }

            _appOpenMutex.ReleaseMutex();

            var deferral = taskInstance.GetDeferral();
            var details = taskInstance.TriggerDetails as RawNotification;
            if (details != null && details.Content != null)
            {
                try
                {
                    TLPushUtils.UpdateToastAndTiles(details);
                }
                catch { }
            }

            deferral.Complete();
        }
    }
}
