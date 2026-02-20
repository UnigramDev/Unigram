//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using Telegram.Td.Api;
using Windows.Foundation;

namespace Telegram
{
    public static partial class Constants
    {
        public static readonly int ApiId;
        public static readonly string ApiHash;

        public static readonly string AppChannel;

        public static readonly string AppReportsId;

        public static readonly string AppCenterId;

        public static readonly ushort BuildNumber;

        public static readonly string TextRecognizerModelKey;

        public static readonly Size SecretSize = new(320, 200);

        public const int TypingTimeout = 300;
        public const int HoldingThrottle = 500;
        public const int AnimatedThrottle = 200;

        public static readonly string[] MediaTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".heic", ".heif", ".mp4", ".mov", ".m4v" };
        public static readonly string[] PhotoTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".heic", ".heif" };
        public static readonly string[] VideoTypes = new[] { ".mp4", ".mov", ".m4v" };

        public const int ImageStandardQuality = 1280;
        public const int ImageHighQuality = 2560;

        public const string WallpaperFileName = "wallpaper.jpg";
        public const string WallpaperLocalFileName = "wallpaper.local.jpg";
        public const string WallpaperColorFileName = "wallpaper.color.jpg";
        public const string WallpaperDefaultFileName = "wallpaper.default.jpg";
        public const int WallpaperLocalId = -1;
        public const int WallpaperColorId = -2;

        public const int ChatListMain = 0;
        public const int ChatListArchive = 1;

        public const string DefaultDeviceId = "";

        public const string WebAppHostName = "windows";

        public const string StakeDice = "\U0001F3B2";
        public const double ToncoinMin = 1000000000.0;

        public const int FontSize = 14;
        public const int CaptionFontSize = 12;

        public const float BubbleElevation = 8.0f;

        public const int HistoryLimit = 24;
        public const int HistoryOffset = -12;

        public static readonly string[] TelegramHosts = new string[]
        {
            "telegram.org",
            "telegram.me",
            "telegram.dog",
            "t.me",
            "telegra.ph"
            /*"telesco.pe"*/
        };

        public static DiffOptions DiffOptions = new()
        {
            AllowBatching = false,
            DetectMoves = true
        };

        public static MessageSendOptions PreviewOnly = new(null, false, false, 0, false, null, 0, 0, true);

#if DEBUG
        // We use properties in debug so that the duration is re-evaluated
        // on every access. This way we can easily debug animations without
        // having to restart the app.
        public static TimeSpan FastAnimation => TimeSpan.FromMilliseconds(167);
        public static TimeSpan SoftAnimation => TimeSpan.FromMilliseconds(333);
#else
        public static TimeSpan FastAnimation = TimeSpan.FromMilliseconds(167);
        public static TimeSpan SoftAnimation = TimeSpan.FromMilliseconds(333);
#endif

#if DEBUG
        public static bool DEBUG = true;
        public static bool RELEASE = false;
#else
        public static bool RELEASE = true;
        public static bool DEBUG = false;
#endif
    }
}
