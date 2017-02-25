using System.Collections.Generic;

namespace Telegram.Api
{
    public static partial class Constants
    {
        public static readonly string FirstServerIpAddress;

        public static readonly int ApiId;
        public static readonly string ApiHash;



        public const int FirstServerDCId = 2; // from 1, 2, 3, 4, 5
        public const int FirstServerPort = 443;

        public const int DatabaseVersion = 1;

        public const int SupportedLayer = 62;

        public const int SecretSupportedLayer = 23;

        public const int LongPollReattemptDelay = 5000;     //ms
        public const double MessageSendingInterval =
#if DEBUG
            300;   //seconds (5 minutes - 30 seconds(max delay: 25))
#else
            180;   //seconds (5 minutes - 30 seconds(max delay: 25))
#endif
        public const double ResendMessageInterval = 5.0;    //seconds
        public const int CommitDBInterval = 3;              //seconds
        public const int GetConfigInterval = 60 * 60;       //seconds
        public const int TimeoutInterval = 25;              //seconds 
        public const double DelayedTimeoutInterval = 45.0;      //seconds 
        public const double NonEncryptedTimeoutInterval = 15.0; //seconds   

        public const bool IsLongPollEnabled = false;
        public const int CachedDialogsCount = 20;
        public const int CachedMessagesCount = 15;
        public const int WorkersNumber = 4;

        public const string ConfigKey = "Config";
        public const string ConfigFileName = "config.xml";
        public static double CheckSendingMesagesInterval = 5.0;     //seconds

        public static double CheckGetConfigInterval =
#if DEBUG
            10.0;
#else
            1 * 60.0;     //seconds (1 min)
#endif
        public static double CheckPingInterval = 20.0;              //seconds
        public static double UpdateStatusInterval = 2.0;
        public static int BigFileDownloadersCount = 4;
        public static int VideoDownloadersCount = 3;
        public static int VideoUploadersCount = 3;
        public static int DocumentUploadersCount = 3;
        public static int AudioDownloadersCount = 3;
        public static int MaximumChunksCount = 3000;
        public static int DownloadChunkSize = 64 * 1024;    // 1MB % DownloadedChunkSize = 0 && DownloadedChunkSize % 1KB = 0
        public static int DocumentDownloadChunkSize = 128 * 1024;    // 1MB % DownloadedChunkSize = 0 && DownloadedChunkSize % 1KB = 0
        public static ulong MaximumUploadedFileSize = 512 * 1024 * 3000;    // 1,5GB

        public static string StateFileName = "state.dat";
        public static string TempStateFileName = "temp_state.dat";
        public static string ActionQueueFileName = "action_queue.dat";
        public static string SentQueueIdFileName = "sent_queue_id.dat";

        public const string IsAuthorizedKey = "IsAuthorized";
        public const int StickerMaxSize = 256 * 1024;         // 256 KB
        public const int SmallFileMaxSize = 10 * 1024 * 1024;   // 10 MB

        public const string BackgroundTaskSettingsFileName = "background_task_settings.dat";
        public const string DifferenceFileName = "difference.dat";
        public const string DifferenceTimeFileName = "difference_time.dat";
        public const string TempDifferenceFileName = "temp_difference.dat";

        public const string TelegramMessengerMutexName = "TelegramMessenger";
        public const double DifferenceMinInterval = 10.0;           //seconds

        public const string InitConnectionFileName = "init_connection.dat";
    }
}