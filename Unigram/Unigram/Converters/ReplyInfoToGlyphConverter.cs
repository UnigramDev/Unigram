using System;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.ViewModels;

namespace Unigram.Converters
{
    public class Icons
    {
        public const string Globe = "\uE774"; // Globe
        public const string Loading = "\uE1CD";

        public const string Attach = "\uE917"; // Attach
        public const string AttachArrowRight = "\uE918"; // AttachArrowRight

        public const string Expand = "\uF164";
        public const string Collapse = "\uF166";

        public const string ArrowDownload = "\uE118"; // ArrowDownload
        public const string DownloadSmall = "\uE92A";
        public const string Dismiss = "\uE711"; // Dismiss
        public const string CancelSmall = "\uE928";
        public const string Play = "\uE768"; // Play
        public const string Pause = "\uE769"; // Pause
        public const string Checkmark = "\uE10B"; // Checkmark
        public const string Ttl = "\uE60E";
        public const string Document = "\uE7C3"; // Document
        public const string Animation = "\uE906";
        public const string Color = "\uE2B1"; // Color

        public const string Retry = "\uE72C";

        public const string ArrowUndo = "\uE7A7"; // ArrowUndo
        public const string ArrowRedo = "\uE7A6"; // ArrowRedo
        public const string Cut = "\uE8C6"; // Cut
        public const string DocumentCopy = "\uE8C8"; // DocumentCopy
        public const string ClipboardPaste = "\uE77F"; // ClipboardPaste

        public const string TextBold = "\uE8DD"; // TextBold
        public const string TextItalic = "\uE8DB"; // TextItalic
        public const string TextUnderline = "\uE8DC"; // TextUnderline
        public const string TextStrikethrough = "\uE8DE"; // TextStrikethrough
        public const string Code = "\uE943"; // Code
        public const string Link = "\uE71B"; // Link

        public const string People = "\uE716"; // People
        public const string Megaphone = "\uE789"; // Megaphone
        public const string Lock = "\uE72E"; // Lock
        public const string Bot = "\uE99A";

        public const string PersonQuestionMark = "\uE897";
        public const string QuestionCircle = "\uE9CE"; // QuestionCircle

        public const string ArrowReply = "\uE248"; // ArrowReply
        public const string Thread = "\uE93D";
        public const string Edit = "\uE104"; // Edit
        public const string Compose = "\uE932"; // Compose
        public const string Signature = "\uEE56"; // Signature
        public const string Timer = "\uE916"; // Timer
        public const string ShieldError = "\uE730"; // ShieldError
        public const string Broom = "\uEA99"; // Broom
        public const string CalendarClock = "\uE81C"; // CalendarClock

        public const string AlertOff = "\uE7ED"; // AlertOff
        public const string Alert = "\uEA8F"; // Alert

        public const string Pin = "\uE840"; // Pin
        public const string PinOff = "\uE77A"; // PinOff

        public const string MarkAsRead = "\uE91D";
        public const string MarkAsUnread = "\uE91C"; // CommentArrowRight

        public const string Image = "\uEB9F"; // Image

        public const string Sticker = "\uF4AA"; // Sticker
        public const string Gif = "\uF4A9"; // Gif

        public const string Star = "\uE734"; // Star
        public const string StarOff = "\uE8D9"; // StarOff

        public const string Comment = "\uE8BD"; // Comment

        public const string Archive = "\uE7B8"; // Archive

        public const string Send = "\uE919";

        public const string Delete = "\uE74D"; // Delete
        public const string Share = "\uE72D"; // Share
        public const string Multiselect = "\uE762"; // Multiselect
        public const string SaveAs = "\uE792"; // SaveAs
        public const string FolderOpen = "\uE838"; // FolderOpen
        public const string OpenIn = "\uE7AC";

        public const string Person = "\uE77B"; // Person
        public const string PersonAdd = "\uE8FA"; // PersonAdd

        public const string Block = "\uF140"; // Block
        public const string Search = "\uE721"; // Search
        public const string Settings = "\uE713"; // Settings
        public const string Phone = "\uE717"; // Phone
        public const string Video = "\uE714"; // Video
        public const string Camera = "\uE722"; // Camera
        public const string MusicNote = "\uE8D6"; // MusicNote
        public const string MicOn = "\uE720"; // MicOn
        public const string MicOnFilled = "\uF12E"; // MicOn
        public const string DataUsage = "\uE9D9"; // DataUsage
        public const string Add = "\uE710"; // Add

        public const string EmojiRecents = "\uE911";
        public const string Emoji1 = "\uE920";
        public const string Emoji2 = "\uE921";
        public const string Emoji3 = "\uE922";
        public const string Emoji4 = "\uE923";
        public const string Emoji5 = "\uE924";
        public const string Emoji6 = "\uE925";
        public const string Emoji7 = "\uE926";
        public const string Emoji8 = "\uE927";

        public const string Calendar = "\uE787"; // Calendar
        public const string TextFont = "\uE8D2"; // TextFont
        public const string StarFilled = "\uE735"; // StarFilled
        public const string Folder = "\uF12B";
        public const string DataPie = "\uEB05"; // DataPie
        public const string FolderMove = "\uE92B"; // FolderMove
        public const string FolderAdd = "\uE929"; // FolderAdd
        public const string Emoji = "\uE76E"; // Emoji
        public const string ChevronUp = "\uE0E4"; // ChevronUp
        public const string ChevronDown = "\uE0E5"; // ChevronDown
        public const string ShieldCheckmark = "\uEA1A"; // ShieldCheckmark
        public const string Bug = "\uE825"; // Bug
        public const string Bookmark = "\uE907"; // Bookmark
        public const string ArrowRight = "\uE72A";
        public const string Channel = "\uEC42";
        public const string Speaker = "\uE995";
        public const string Speaker1 = "\uE993";
        public const string SpeakerNone = "\uE74F";
        public const string SendFilled = "\uE919";

        public const string AppFolder = "\uF122";

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
    }
}
