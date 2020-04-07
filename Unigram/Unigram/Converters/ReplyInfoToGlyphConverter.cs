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
        public const string ForwardGlyph = "\uE72D";
        public const string LoadingGlyph = "\uE1CD";
        public const string SendGlyph = "\uE725";
        public const string ConfirmGlyph = "\uE10B";

        public const string AttachGlyph = "\uE917";
        public const string AttachEditGlyph = "\uE918";
    }

    public class Icons
    {
        public const string Download = "\uE118";
        public const string Cancel = "\uE10A";
        public const string Play = "\uE102";
        public const string Pause = "\uE103";
        public const string Confirm = "\uE10B";
        public const string Ttl = "\uE60E";
        public const string Document = "\uE160";
        public const string Animation = "\uE906";
        public const string Theme = "\uE2B1";

        public const string Retry = "\uE72C";

        public const string Undo = "\uE7A7";
        public const string Redo = "\uE7A6";
        public const string Cut = "\uE8C6";
        public const string Copy = "\uE8C8";
        public const string Paste = "\uE77F";

        public const string Bold = "\uE8DD";
        public const string Italic = "\uE8DB";
        public const string Underline = "\uE8DC";
        public const string Strikethrough = "\uE8DE";
        public const string Monospace = "\uE943";
        public const string Link = "\uE71B";

        public const string BasicGroup = "\uE125";
        public const string Group = "\uE902";
        public const string Channel = "\uE789";
        public const string Secret = "\uE1F6";
        public const string Bot = "\uE99A";

        public const string Help = "\uE897";

        public const string Reply = "\uE248";
        public const string Edit = "\uE104";
        public const string Timer = "\uE916";
        public const string Report = "\uE730";
        public const string Clear = "\uEA99";
        public const string Schedule = "\uE81C";

        public const string Mute = "\uE7ED";
        public const string Unmute = "\uEA8F";

        public const string Pin = "\uE840";
        public const string Unpin = "\uE77A";

        public const string MarkAsRead = "\uE91D";
        public const string MarkAsUnread = "\uE91C";

        public const string CopyLink = "\uE71B";
        public const string CopyImage = "\uEB9F";

        public const string Stickers = "\uF4AA";
        public const string Animations = "\uF4A9";

        public const string Favorite = "\uE734";
        public const string Unfavorite = "\uE8D9";

        public const string Message = "\uE8BD";

        public const string Archive = "\uE7B8";

        public const string Send = "\uE919";

        public const string Delete = "\uE74D";
        public const string Forward = "\uE72D";
        public const string Select = "\uE762";
        public const string SaveAs = "\uE792";
        public const string Folder = "\uE838";
        public const string OpenIn = "\uE7AC";
        public const string OpenInNewWindow = "\uE8A7";

        public const string Contact = "\uE77B";
        public const string AddUser = "\uE8FA";

        public const string Admin = "\uE734";
        public const string Restricted = "\uE72E";
        public const string Banned = "\uF140";

        public const string Share = "\uE72D";
        public const string Search = "\uE721";
        public const string Settings = "\uE713";

        public const string Camera = "\uE722";
        public const string Photo = "\uEB9F";

        public const string Statistics = "\uE9D9";

        public const string Add = "\uE710";
    }
}
