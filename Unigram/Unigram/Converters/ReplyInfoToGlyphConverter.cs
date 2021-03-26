using System;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.ViewModels;

namespace Unigram.Converters
{
    public class Icons
    {
        public const string Globe = "\uE774"; 
        public const string Loading = "\uE1CD";

        public const string Attach = "\uE917"; 
        public const string AttachArrowRight = "\uE918"; 

        public const string Expand = "\uF164";
        public const string Collapse = "\uF166";

        public const string ArrowDownload = "\uE118"; 
        public const string DownloadSmall = "\uE92A";
        public const string Dismiss = "\uE711"; 
        public const string CancelSmall = "\uE928";
        public const string Play = "\uE768"; 
        public const string Pause = "\uE769"; 
        public const string Checkmark = "\uE10B"; 
        public const string Ttl = "\uE60E";
        public const string Document = "\uE7C3"; 
        public const string Animation = "\uE906";
        public const string Color = "\uE2B1"; 

        public const string Retry = "\uE72C";

        public const string ArrowUndo = "\uE7A7"; 
        public const string ArrowRedo = "\uE7A6"; 
        public const string Cut = "\uE8C6"; 
        public const string DocumentCopy = "\uE8C8"; 
        public const string ClipboardPaste = "\uE77F"; 

        public const string TextBold = "\uE8DD"; 
        public const string TextItalic = "\uE8DB"; 
        public const string TextUnderline = "\uE8DC"; 
        public const string TextStrikethrough = "\uE8DE"; 
        public const string Code = "\uE943"; 
        public const string Link = "\uE71B"; 

        public const string People = "\uE716"; 
        public const string Megaphone = "\uE789"; 
        public const string Lock = "\uE72E"; 
        public const string Bot = "\uE99A";

        public const string PersonQuestionMark = "\uE897";
        public const string QuestionCircle = "\uE9CE"; 

        public const string ArrowReply = "\uE248"; 
        public const string Thread = "\uE93D";
        public const string Edit = "\uE104"; 
        public const string Compose = "\uE932"; 
        public const string Signature = "\uEE56"; 
        public const string Timer = "\uE916"; 
        public const string ShieldError = "\uE730"; 
        public const string Broom = "\uEA99"; 
        public const string CalendarClock = "\uE81C"; 

        public const string AlertOff = "\uE7ED"; 
        public const string Alert = "\uEA8F"; 

        public const string Pin = "\uE840"; 
        public const string PinOff = "\uE77A"; 

        public const string MarkAsRead = "\uE91D";
        public const string MarkAsUnread = "\uE91C"; 

        public const string Image = "\uEB9F"; 

        public const string Sticker = "\uF4AA"; 
        public const string Gif = "\uF4A9"; 

        public const string Star = "\uE734"; 
        public const string StarOff = "\uE8D9"; 

        public const string Comment = "\uE8BD"; 

        public const string Archive = "\uE7B8"; 

        public const string Send = "\uE919";

        public const string Delete = "\uE74D"; 
        public const string Share = "\uE72D"; 
        public const string Multiselect = "\uE762"; 
        public const string SaveAs = "\uE792"; 
        public const string FolderOpen = "\uE838"; 
        public const string OpenIn = "\uE7AC";

        public const string Person = "\uE77B"; 
        public const string PersonAdd = "\uE8FA"; 

        public const string Block = "\uF140"; 
        public const string Search = "\uE721"; 
        public const string Settings = "\uE713"; 
        public const string Phone = "\uE717"; 
        public const string Video = "\uE714"; 
        public const string Camera = "\uE722"; 
        public const string MusicNote = "\uE8D6"; 
        public const string MicOn = "\uE720"; 
        public const string MicOff = "\uE610"; 
        public const string MicOnFilled = "\uF12E"; 
        public const string DataUsage = "\uE9D9"; 
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

        public const string Calendar = "\uE787"; 
        public const string TextFont = "\uE8D2"; 
        public const string StarFilled = "\uE735"; 
        public const string Folder = "\uF12B";
        public const string DataPie = "\uEB05"; 
        public const string FolderMove = "\uE92B"; 
        public const string FolderAdd = "\uE929"; 
        public const string Emoji = "\uE76E"; 
        public const string ChevronUp = "\uE0E4"; 
        public const string ChevronDown = "\uE0E5"; 
        public const string ShieldCheckmark = "\uEA1A"; 
        public const string Bug = "\uE825"; 
        public const string Bookmark = "\uE907"; 
        public const string ArrowRight = "\uE72A";
        public const string Channel = "\uEC42";
        public const string Speaker = "\uE995";
        public const string Speaker1 = "\uE993";
        public const string SpeakerNone = "\uE74F";
        public const string SendFilled = "\uE919";

        public const string VoiceChat = "\uE900";

        public const string EmojiHand = "\uE901";

        public const string AppFolder = "\uF122";

        public const string ChatBubblesQuestion = "\uE783";

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
