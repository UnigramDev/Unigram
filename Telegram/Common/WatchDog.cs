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

    public class UnhandledException : Exception
    {
        public UnhandledException()
            : base("The process crashed.")
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

            if (previousExecutionState == ApplicationExecutionState.NotRunning && File.Exists(_crashLock))
            {
                var text = File.ReadAllText(_crashLock);
                // TODO: REMOVE ME!!!
                if (text.Contains("(8374)") || text.Contains("(8376)"))
                {

                }
                else if (text.StartsWith("Unhandled"))
                {
                    Crashes.TrackError(new UnhandledException(), attachments: ErrorAttachmentLog.AttachmentWithText(text, "crash.txt"));
                }
                else
                {
                    Crashes.TrackError(new UnexpectedException(), attachments: ErrorAttachmentLog.AttachmentWithText(text, "crash.txt"));
                }
            }

            File.WriteAllText(_crashLock, VersionLabel.GetVersion());
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
