using Microsoft.AppCenter.Crashes;
using System;
using System.IO;
using Telegram.Controls;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace Telegram.Common
{
    public class UnexpectedException : Exception
    {
        public UnexpectedException()
            : base("The process was quit unexpectedly.")
        {
        }
    }

    public class WatchDog
    {
        private static readonly string _crashLock;

        static WatchDog()
        {
            _crashLock = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.lock");
        }

        public static void Start(ApplicationExecutionState previousExecutionState)
        {
#if !DEBUG
            // NotRunning: An app could be in this state because it hasn't been launched
            // since the last time the user rebooted or logged in. It can also be in this
            // state if it was running but then crashed, or because the user closed it earlier.

            if (previousExecutionState == ApplicationExecutionState.NotRunning && File.Exists(_crashLock))
            {
                var text = File.ReadAllText(_crashLock);
                var exception = new UnexpectedException();

                Crashes.TrackError(exception, attachments: ErrorAttachmentLog.AttachmentWithText(text, "crash.txt"));
            }

            File.WriteAllText(_crashLock, VersionLabel.GetVersion());
#endif
        }

        public static void Stop()
        {
#if !DEBUG
            if (File.Exists(_crashLock))
            {
                File.Delete(_crashLock);
            }
#endif
        }
    }
}
