using Microsoft.AppCenter.Crashes;
using System;
using System.IO;
using Telegram.Controls;
using Telegram.Services;
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
        public UnhandledException(string message)
            : base(message)
        {
        }
    }

    public class WatchDog
    {
        private static readonly string _crashLog;

        static WatchDog()
        {
            _crashLog = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
        }

        public static bool? HasCrashedInLastSession { get; private set; }

        public static void Start(ApplicationExecutionState previousExecutionState)
        {
            // NotRunning: An app could be in this state because it hasn't been launched
            // since the last time the user rebooted or logged in. It can also be in this
            // state if it was running but then crashed, or because the user closed it earlier.

            if (previousExecutionState == ApplicationExecutionState.NotRunning && File.Exists(_crashLog))
            {
                var text = File.ReadAllText(_crashLog);
                var split = text.Split("\n----------\n");

                if (split.Length == 2)
                {
                    HasCrashedInLastSession = true;

                    var exception = GetException(split[0], out bool report);
                    if (report)
                    {
                        Crashes.TrackError(exception, attachments: ErrorAttachmentLog.AttachmentWithText(split[1], "crash.txt"));
                    }
                }
                else
                {
                    HasCrashedInLastSession = null;
                    Crashes.TrackError(new UnexpectedException());
                }
            }
            else
            {
                HasCrashedInLastSession = false;
            }

            File.WriteAllText(_crashLog, VersionLabel.GetVersion());
        }

        private static Exception GetException(string message, out bool unhandled)
        {
            if (message.StartsWith("TDLib"))
            {
                var exception = TdException.FromMessage(message);
                unhandled = exception.IsUnhandled;

                return exception;
            }

            unhandled = true;
            return new UnhandledException(message);
        }

        public static void Update(string data)
        {
            var version = VersionLabel.GetVersion();

            var next = DateTime.Now.ToTimestamp();
            var prev = SettingsService.Current.Diagnostics.LastUpdateTime;

            var count = SettingsService.Current.Diagnostics.UpdateCount;

            var info =
                $"Current version: {version}\n" + 
                $"Time since last update: {next - prev}s\n" +
                $"Update count: {count}\n\n";

            var dump = Logger.Dump();
            var payload = data + "\n----------\n" + info + dump;

            // Sigh...
            File.WriteAllText(Path.ChangeExtension(_crashLog, ".appcenter"), payload);

            File.WriteAllText(_crashLog, payload);
            Client.Execute(new AddLogMessage(1, payload));
        }

        public static void Stop()
        {
            if (File.Exists(_crashLog))
            {
                File.Delete(_crashLog);
            }
        }
    }
}
