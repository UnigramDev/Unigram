using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram
{
    public static class Constants
    {
        public const int TypingTimeout = 300;

        public static readonly string[] MediaTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4" };
        public static readonly string[] PhotoTypes = new[] { ".jpg", ".jpeg", ".png", ".gif" };

        public const string WallpaperFileName = "wallpaper.jpg";

        public static readonly string[] TelegramHosts = new string[]
        {
            "telegram.me",
            "telegram.dog",
            "t.me",
            /*"telesco.pe"*/
        };
    }
}
