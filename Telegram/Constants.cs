//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using Telegram.Td.Api;

namespace Telegram
{
    public static partial class Constants
    {
        public static readonly int ApiId;
        public static readonly string ApiHash;

        public static readonly string AppChannel;

        public static readonly string AppCenterId;
        public static readonly string BingMapsApiKey;

        public static readonly ushort BuildNumber;

        public const int TypingTimeout = 300;
        public const int HoldingThrottle = 500;
        public const int AnimatedThrottle = 200;

        public static readonly string[] MediaTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4" };
        public static readonly string[] PhotoTypes = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        public static readonly string[] VideoTypes = new[] { ".mp4" };

        public const string WallpaperFileName = "wallpaper.jpg";
        public const string WallpaperLocalFileName = "wallpaper.local.jpg";
        public const string WallpaperColorFileName = "wallpaper.color.jpg";
        public const string WallpaperDefaultFileName = "wallpaper.default.jpg";
        public const int WallpaperLocalId = -1;
        public const int WallpaperColorId = -2;

        public const int ChatListMain = 0;
        public const int ChatListArchive = 1;

        public const string DefaultDeviceId = "";

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

        public static MessageSendOptions PreviewOnly = new(false, false, false, false, null, 0, true);

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
    }
}
