using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Unigram.ViewModels;
using Telegram.Td.Api;

namespace Unigram.Converters
{
    public class ReplyInfoToGlyphConverter
    {
        public const string EditGlyph = "\uE104";
        public const string ReplyGlyph = "\uE248";
        public const string GlobeGlyph = "\uE12B";
        public const string ForwardGlyph = "\uEE35";
        public const string LoadingGlyph = "\uE1CD";
        public const string SendGlyph = "\uE725";
        public const string ConfirmGlyph = "\uE10B";

        public const string AttachGlyph = "\uE917";
        public const string AttachEditGlyph = "\uE918";
    }

    public class Icons
    {
        public const string BasicGroup = "\uE125";
        public const string Group = "\uE902";
        public const string Channel = "\uE789";
        public const string Secret = "\uE1F6";
        public const string Bot = "\uE99A";

        public const string Reply = "\uE248";
        public const string Edit = "\uE104";
        public const string Timer = "\uE916";
        public const string Report = "\uE730";
        public const string Clear = "\uEA99";

        public const string Mute = "\uE7ED";
        public const string Unmute = "\uEA8F";

        public const string Pin = "\uE840";
        public const string Unpin = "\uE77A";

        public const string MarkAsRead = "\uE91B";
        public const string MarkAsUnread = "\uE91C";

        public const string Copy = "\uE8C8";
        public const string CopyLink = "\uE71B";
        public const string CopyImage = "\uEB9F";

        public const string Stickers = "\uF4AA";
        public const string Animations = "\uF4A9";

        public const string Favorite = "\uE734";
        public const string Unfavorite = "\uE8D9";

        public const string Message = "\uE8BD";

        public const string Delete = "\uE74D";
        public const string Forward = "\uEE35";
        public const string Select = "\uE762";
        public const string SaveAs = "\uE792";
        public const string Folder = "\uE838";
        public const string OpenIn = "\uE7AC";

        public const string Contact = "\uE8D4";
        public const string AddUser = "\uE8FA";

        public const string Admin = "\uE734";
        public const string Restricted = "\uE72E";
        public const string Banned = "\uF140";

        public const string Share = "\uE72D";
        public const string Search = "\uE721";
        public const string Settings = "\uE713";
    }
}
