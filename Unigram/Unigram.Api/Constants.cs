using System.Collections.Generic;
using Telegram.Api.Helpers;

namespace Telegram.Api
{
    public static partial class Constants
    {
        public static readonly int ApiId;
        public static readonly string ApiHash;

        public const int CallsMaxLayer = 65;
        public const int CallsMinLayer = 65;

        public const int AccountsMaxCount = 3;

        public const string BackgroundTaskSettingsFileName = "background_task_settings.dat";
        public const string DifferenceFileName = "difference.dat";
        public const string DifferenceTimeFileName = "difference_time.dat";
        public const string TempDifferenceFileName = "temp_difference.dat";

        public const string TelegramMessengerMutexName = "TelegramMessenger";
        public const double DifferenceMinInterval = 10.0;           //seconds

        public const string InitConnectionFileName = "init_connection.dat";

        public const string ProxyConfigFileName = "proxy_config.dat";
    }
}