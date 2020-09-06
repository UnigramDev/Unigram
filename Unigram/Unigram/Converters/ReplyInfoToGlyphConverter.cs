using System;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.ViewModels;

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
        public const string DownloadSmall = "\uE92A";
        public const string Cancel = "\uE10A";
        public const string CancelSmall = "\uE928";
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
        public const string Help2 = "\uE9CE";

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

        public const string Call = "\uE13A";
        public const string VideoCall = "\uE714";

        public const string Camera = "\uE722";
        public const string Photo = "\uEB9F";

        public const string Statistics = "\uE9D9";

        public const string Add = "\uE710";

        public const string EmojiRecents = "\uE911";
        public const string Emoji1 = "\uE920";
        public const string Emoji2 = "\uE921";
        public const string Emoji3 = "\uE922";
        public const string Emoji4 = "\uE923";
        public const string Emoji5 = "\uE924";
        public const string Emoji6 = "\uE925";
        public const string Emoji7 = "\uE926";
        public const string Emoji8 = "\uE927";

        public static readonly ChatFilterIcon[] Filters = new ChatFilterIcon[]
        {
            ChatFilterIcon.Cat,
            ChatFilterIcon.Crown,
            ChatFilterIcon.Favorite,
            ChatFilterIcon.Flower,
            ChatFilterIcon.Game,
            ChatFilterIcon.Home,
            ChatFilterIcon.Love,
            ChatFilterIcon.Mask,
            ChatFilterIcon.Party,
            ChatFilterIcon.Sport,
            ChatFilterIcon.Study,
            ChatFilterIcon.Trade,
            ChatFilterIcon.Travel,
            ChatFilterIcon.Work,
            ChatFilterIcon.All,
            ChatFilterIcon.Unread,
            ChatFilterIcon.Unmuted,
            ChatFilterIcon.Bots,
            ChatFilterIcon.Channels,
            ChatFilterIcon.Groups,
            ChatFilterIcon.Private,
            ChatFilterIcon.Custom,
            ChatFilterIcon.Setup
        };

        public static ChatFilterIcon ParseFilter(ChatFilter filter)
        {
            var iconName = filter.IconName;
            if (string.IsNullOrEmpty(iconName))
            {
                var text = Client.Execute(new GetChatFilterDefaultIconName(filter)) as Text;
                if (text != null)
                {
                    iconName = text.TextValue;
                }
            }

            return ParseFilter(iconName);
        }

        public static ChatFilterIcon ParseFilter(string iconName)
        {
            if (Enum.TryParse(iconName, out ChatFilterIcon result))
            {
                return result;
            }

            return ChatFilterIcon.Custom;
        }

        public static string FromFilter(ChatFilterIcon icon)
        {
            switch (icon)
            {
                case ChatFilterIcon.All:
                    return "\uE92D";
                case ChatFilterIcon.Unread:
                    return "\uE91C";
                case ChatFilterIcon.Unmuted:
                    return "\uE93B";
                case ChatFilterIcon.Bots:
                    return "\uE92E";
                case ChatFilterIcon.Channels:
                    return "\uE930";
                case ChatFilterIcon.Groups:
                    return "\uE935";
                case ChatFilterIcon.Private:
                    return "\uE931";
                case ChatFilterIcon.Custom:
                default:
                    return "\uE932";
                case ChatFilterIcon.Setup:
                    return "\uE938";
                case ChatFilterIcon.Cat:
                    return "\uE92F";
                case ChatFilterIcon.Crown:
                    return "\uE932"; // <-- todo
                case ChatFilterIcon.Favorite:
                    return "\uE933";
                case ChatFilterIcon.Flower:
                    return "\uE932"; // <-- todo
                case ChatFilterIcon.Game:
                    return "\uE934";
                case ChatFilterIcon.Home:
                    return "\uE936";
                case ChatFilterIcon.Love:
                    return "\uE937";
                case ChatFilterIcon.Mask:
                    return "\uE932"; // <-- todo
                case ChatFilterIcon.Party:
                    return "\uE932"; // <-- todo
                case ChatFilterIcon.Sport:
                    return "\uE923";
                case ChatFilterIcon.Study:
                    return "\uE939";
                case ChatFilterIcon.Trade:
                    return "\uE932"; // <-- todo
                case ChatFilterIcon.Travel:
                    return "\uE93A";
                case ChatFilterIcon.Work:
                    return "\uE93C";
            }
        }
    }
}
