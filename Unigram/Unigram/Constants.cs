using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram
{
    public static partial class Constants
    {
        public static readonly int ApiId;
        public static readonly string ApiHash;

        public static readonly string AppCenterId;


        public const int TypingTimeout = 300;

        public static readonly string[] MediaTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4" };
        public static readonly string[] PhotoTypes = new[] { ".jpg", ".jpeg", ".png", ".gif" };

        public const string WallpaperFileName = "wallpaper.jpg";
        public const string WallpaperLocalFileName = "wallpaper.local.jpg";
        public const string WallpaperColorFileName = "wallpaper.color.jpg";
        public const int WallpaperLocalId = -1;

        public static readonly string[] TelegramHosts = new string[]
        {
            "telegram.me",
            "telegram.dog",
            "t.me",
            "telegra.ph"
            /*"telesco.pe"*/
        };
    }
}
