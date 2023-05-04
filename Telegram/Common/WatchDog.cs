using Microsoft.AppCenter.Crashes;
using System;
using System.IO;
using Telegram.Controls;
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

        public static void Start()
        {
            if (File.Exists(_crashLock))
            {
                var text = File.ReadAllText(_crashLock);
                var exception = new UnexpectedException();

                Crashes.TrackError(exception, attachments: ErrorAttachmentLog.AttachmentWithText(text, "crash.txt"));
            }

            File.WriteAllText(_crashLock, VersionLabel.GetVersion());
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
