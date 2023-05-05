using Microsoft.AppCenter.Crashes;
using System;
using System.IO;
using Telegram.Controls;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using File = System.IO.File;

namespace Telegram.Common
{
    public class UnexpectedException : Exception
    {
        public UnexpectedException()
            : base("The process terminated unexpectedly.")
        {
        }
    }

    public class CrashedException : Exception
    {
        public CrashedException()
            : base("The process has crashed.")
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
            // NotRunning: An app could be in this state because it hasn't been launched
            // since the last time the user rebooted or logged in. It can also be in this
            // state if it was running but then crashed, or because the user closed it earlier.

            var version = VersionLabel.GetVersion();

            if (previousExecutionState == ApplicationExecutionState.NotRunning && File.Exists(_crashLock))
            {
                var text = File.ReadAllText(_crashLock);
                var exception = new UnexpectedException();

                // Temporary, as for now this happens even after updates
                if (text == version)
                {
                    Crashes.TrackError(exception, attachments: ErrorAttachmentLog.AttachmentWithText(text, "crash.txt"));
                }
            }

            File.WriteAllText(_crashLock, version);
        }

        public static void Update(string data)
        {
            File.WriteAllText(_crashLock, data);
            Client.Execute(new AddLogMessage(1, data));
        }

        public static void Stop()
        {
            if (File.Exists(_crashLock))
            {
                File.Delete(_crashLock);
            }
        }
    }
}
