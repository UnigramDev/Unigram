//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Media
{
    public partial class Icons
    {
        public const string BulletSpace = "\u2022 ";
        public const string RTL = "\u200F";
        public const string LTR = "\u200E";
        public const string ZWJ = "\u200D";
        public const string ZWNJ = "\u200C";
        public const string Spacing = "\u2004\u200A";

        public const string Boosters12 = "\uEA6A";
        public const string Boosters212 = "\uEA6B";

        public static string TagFilter = "\uEA63";
        public static string TagEdit = "\uEA64";
        public static string TagSearch = "\uEA65";
        public static string TagOff = "\uEA66";

        public const string PictureInPictureEnter = "\uE92C";
        public const string PictureInPictureExit = "\uE92D";

        public const string ArrowDownLeft16 = "\uE928";
        public const string ArrowUpRight16 = "\uE9B7";

        public const string ArrowSync = "\uE981";

        public const string TicketDiagonal = "\uEA9D";
        public const string LinkDiagonal = "\uE9F8";
        public const string LinkDiagonalBroken = "\uE9F7";

        public const string ArrowMinimize = "\uE965";
        public const string ArrowMaximize = "\uE966";
        public const string ArrowMinimizeFilled24 = "\uEA94";
        public const string ArrowMaximizeFilled24 = "\uEA95";

        public const string PersonFilled16 = "\uEA48";
        public const string MegaphoneFilled16 = "\uEA47";

        public const string CodeFilled16 = "\uEA5A";
        public const string CodeBlockFilled16 = "\uEA3D";
        public const string QuoteBlockFilled16 = "\uEA01";
        public const string QuoteBlock = "\uEA3E";

        public const string Globe = "\uE774";
        public const string Loading = "\uE1CD";

        public const string ArrowReply24 = "\uEA4A";
        public const string Link24 = "\uEA49";
        public const string Edit24 = "\uEA4B";

        public const string Attach = "\uE917";
        public const string Attach24 = "\uE9D8";

        public const string Replace = "\uEA3F";
        public const string Replace24 = "\uEA60";

        public const string MoveUp = "\uEA44";
        public const string MoveDown = "\uEA43";
        public const string Enlarge = "\uEA45";
        public const string Shrink = "\uEA46";

        // TODO: wrong name
        public const string Location = "\uE1C4";
        public const string Poll = "\uE91F";
        public const string Crop = "\uE7A8";

        public const string AddCircle = "\uF164";
        public const string SubtractCircle = "\uF166";

        public const string ArrowDownloadFilled24 = "\uEA5C";
        public const string ArrowDownload = "\uE118";
        public const string DownloadSmall = "\uE92A";
        public const string Dismiss = "\uE711";
        public const string DismissCircle = "\uEA4E";
        public const string DismissFilled24 = "\uEA1F";
        public const string Cancel = "\uE978";
        public const string CancelSmall = "\uE93F";
        public const string Play = "\uE768";
        public const string ArrowClockwiseFilled12 = "\uEA35";
        public const string PlayFilled12 = "\uEA36";
        public const string PlayFilled24 = "\uEA17";
        public const string Pause = "\uE769";
        public const string PauseFilled24 = "\uEA18";
        public const string Checkmark = "\uE10B";
        public const string CheckmarkFilled24 = "\uEA20";
        public const string TtlFilled24 = "\uEA21";
        public const string Document = "\uE7C3";
        public const string DocumentFilled24 = "\uEA1C";
        public const string Animation = "\uE906";
        public const string Color = "\uE2B1";
        public const string ColorFilled24 = "\uEA1B";

        public const string ArrowClockwise = "\uE72C";

        public const string Info = "\uEA8C";
        public const string Coin = "\uEA8D";

        public const string ArrowUndo = "\uE7A7";
        public const string ArrowRedo = "\uE7A6";
        public const string Cut = "\uE8C6";
        public const string DocumentCopy = "\uE8C8";
        public const string ClipboardPaste = "\uE77F";

        public const string Translate = "\uE97D";

        public const string Payment16 = "\uE9AB";
        public const string Window16 = "\uE9AA";
        public const string CopyFilled16 = "\uEA97";

        public const string NumberSymbolFilled16 = "\uE9FB";

        public const string TextBold = "\uE8DD";
        public const string TextItalic = "\uE8DB";
        public const string TextUnderline = "\uE8DC";
        public const string TextStrikethrough = "\uE8DE";
        public const string Code = "\uE943";
        public const string Link = "\uE71B";
        public const string TabInPrivate = "\uE97C";

        public const string People = "\uE716";
        public const string Megaphone = "\uE789";
        public const string LockClosed = "\uE72E";
        public const string LockClosedFilled24 = "\uEA1E";
        public const string Bot = "\uE99A";
        public const string Wallet = "\uEA38";

        public const string GiftPremium = "\uE9D4";

        public const string HandRight = "\uE98B";
        public const string PlayCircle = "\uE9FC";

        public const string Hamburger = "\uE700";

        public const string LockClosedFilled12 = "\uE9DD";
        public const string LockClosedFilled14 = "\uEA0C";

        public const string PersonQuestionMark = "\uE897";
        public const string QuestionCircle = "\uE9CE";

        public const string Boosts = "\uEA39";

        public const string ArrowReply = "\uE248";
        public const string Thread = "\uE93D";
        public const string Edit = "\uE104";
        public const string EditFilled = "\uEA55";
        public const string Compose = "\uE932";
        public const string Compose16 = "\uE996";
        public const string Signature = "\uEE56";
        public const string Timer = "\uE916";
        public const string ShieldError = "\uE730";
        public const string Broom = "\uEA99";
        public const string CalendarClock = "\uE81C";
        public const string CheckmarkStarburst = "\uEA8B";

        public const string ClockAlarmHour = "\uE9A3";

        public const string Live = "\uE98D";

        public const string Options = "\uE9E9";

        public const string AlertOff = "\uE7ED";
        public const string AlertOffFilled = "\uEA53";
        public const string AlertOn = "\uE9A4";
        public const string Alert = "\uEA8F";
        public const string AlertFilled = "\uEA54";
        public const string AlertSnooze = "\uE99C";

        public const string StealthLocked = "\uEA29";
        public const string Stealth = "\uEA28";

        public const string MusicNote2 = "\uE99D";
        public const string MusicNoteOff2 = "\uE99E";

        public const string ErrorCircle = "\uEA22";

        public const string Pin = "\uE840";
        public const string PinOff = "\uE77A";

        public const string MarkAsRead = "\uE91D";
        public const string MarkAsUnread = "\uE91C";

        public const string ChatUnreadFilled = "\uEA79";
        public const string MegaphoneFilled = "\uEA78";
        public const string BotFilled = "\uEA77";

        public const string Image = "\uEB9F";
        public const string ImageFilled24 = "\uEA1D";

        public const string ArrowUpload = "\uEA01";

        public const string Sticker = "\uF4AA";
        public const string Sticker24 = "\uE9E2";
        public const string Gif = "\uF4A9";
        public const string Gif24 = "\uE9E1";

        public const string Reactions = "\uE980";
        public const string Mention16 = "\uE986";
        public const string HeartFilled16 = "\uE985";
        public const string HeartFilled12 = "\uE998";
        public const string Heart = "\uE987";

        public const string AutoDeleteOff = "\uE99F";
        public const string AutoDeleteDay = "\uE9A0";
        public const string AutoDeleteWeek = "\uE9A1";
        public const string AutoDeleteMonth = "\uE9A2";

        public const string Star = "\uE734";
        public const string StarOff = "\uE8D9";

        public const string ChatEmpty = "\uE8BD";
        public const string ChatEmptyFilled = "\uE9B0";
        public const string ChatEmptyFilled16 = "\uEA4C";

        public const string Archive = "\uE7B8";
        public const string ArchiveFilled = "\uEA0E";

        public const string MyNotesFilled = "\uEA61";
        public const string AuthorHiddenFilled = "\uEA62";

        public const string StoriesOff = "\uEA2D";
        public const string StoriesPinned = "\uE9AC";
        public const string StoriesPinnedOff = "\uE9D2";

        public const string Unarchive = "\uEA24";

        public const string WindowNew = "\uE97F";

        public const string Send = "\uE919";

        public const string Key = "\uE192";
        public const string Record = "\uE903";

        public const string AppsListDetails = "\uEA67";
        public const string DeleteFilled24 = "\uEAA0";
        public const string Delete = "\uE74D";
        public const string Share = "\uE72D";
        public const string ShareFilled = "\uE914";
        public const string CheckmarkCircle = "\uE97E";
        public const string Multiselect = "\uE762";
        public const string SaveAs = "\uE792";
        public const string SaveAsLocked = "\uEA2B";
        public const string FolderOpen = "\uE838";
        public const string OpenIn = "\uE7AC";
        public const string PersonCircle = "\uE9A9";

        public const string ArrowExit = "\uE999";
        public const string ArrowExitFilled = "\uEA56";
        public const string ArrowEnter = "\uE99B";
        public const string ArrowEnterFilled = "\uEA57";

        public const string ShareScreenStart = "\uE92F";
        public const string ShareScreenStop = "\uE92E";

        public const string Person = "\uE77B";
        public const string PersonAdd = "\uE8FA";
        public const string PersonCircleOnline = "\uE982";

        public const string EmojiEdit = "\uE9EC";
        public const string EmojiAdd = "\uE9ED";

        public const string Stories = "\uE9D3";
        public const string ExpiredStory = "\uE9E5";

        public const string Rewind524 = "\uEA31";
        public const string Rewind2524 = "\uEA32";

        public const string Block = "\uF166";
        public const string Search = "\uE721";
        public const string SearchFilled = "\uEA58";
        public const string Settings = "\uE713";
        public const string Call = "\uE717";
        public const string CallFilled = "\uEA52";
        public const string CallFilled24 = "\uE91B";
        public const string Video = "\uE714";
        public const string SmallVideoFilled = "\uE915";
        public const string VideoFilled = "\uE964";
        public const string VideoFilled24 = "\uE91E";
        public const string VideoNoteFilled24 = "\uE9D6";
        public const string MicOnFilled24 = "\uE9D7";
        public const string VideoOffFilled = "\uE963";
        public const string SmallScreencastFilled = "\uE910";
        public const string ShareScreenFilled = "\uE956";
        public const string ShareScreenStopFilled = "\uE955";
        public const string Camera = "\uE722";
        public const string MusicNote = "\uE8D6";
        public const string MicOn = "\uE720";
        public const string MicOff = "\uE610";
        public const string MicOnFilled = "\uF12E";
        public const string DataUsage = "\uE9D9";
        public const string Add = "\uE710";

        public const string LastSeenFilled = "\uEA6D";
        public const string ChatLockedFilled = "\uEA6C";
        public const string ChatSparkeFilled = "\uEA90";
        public const string BuildingShopFilled = "\uEA86";

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
        public const string StarFilled16 = "\uEA23";
        public const string PeopleFilled16 = "\uEA27";
        public const string PersonCircleFilled16 = "\uEA26";
        public const string Folder = "\uF12B";
        public const string DataPie = "\uEB05";
        public const string FolderMove = "\uE92B";
        public const string FolderAdd = "\uE929";
        public const string Emoji = "\uE76E";
        public const string Emoji24 = "\uE9E3";
        public const string ChevronUp = "\uE0E4";
        public const string ChevronDown = "\uE0E5";
        public const string ChevronRight = "\uE0E3";
        public const string ShieldCheckmark = "\uEA1A";
        public const string ShieldStar = "\uE983";
        public const string Bug = "\uE825";
        public const string Bookmark = "\uE907";
        public const string BookmarkFilled = "\uEA0F";
        public const string ArrowRight = "\uE72A";
        public const string ArrowRightFilled16 = "\uEA4D";
        public const string ArrowLeft = "\uE72B";
        public const string Channel = "\uEC42";
        public const string Speaker3 = "\uEA12";
        public const string Speaker2 = "\uE995";
        public const string Speaker1 = "\uE993";
        public const string Speaker0 = "\uEA11";
        public const string Speaker2Filled = "\uEA2E";
        public const string Speaker1Filled = "\uEA2F";
        public const string SpeakerMuteFilled = "\uEA30";
        public const string SpeakerOff = "\uE74F";
        public const string SendFilled = "\uE919";

        public const string MoreHorizontal = "\uE712";
        public const string MoreHorizontalFilled = "\uEA59";

        public const string GhostFilled = "\uE91A";

        public const string VideoChat = "\uE900";
        public const string VideoChatFilled = "\uEA4F";

        public const string EmojiHand = "\uE901";

        public const string AppFolder = "\uF122";

        public const string BotMarkup24 = "\uE9D5";

        public const string ChatBubblesQuestion = "\uE783";
        public const string ChatMultiple = "\uE992";
        public const string ChatMultipleFilled = "\uEA0D";
        public const string CameraAddFilled = "\uEA92";

        public const string TopSpeed = "\uE90D";

        public const string PaintBrush = "\uE90E";
        public const string PaintBrushFilled = "\uEA51";
        public const string WallpaperFilled = "\uEA50";

        public const string HandWaveFilled = "\uEA7B";
        public const string ChatSnoozeFilled = "\uEA7D";
        public const string ArrowReplyFilled = "\uEA7C";
        public const string LocationFilled = "\uEA7A";

        public const string Seen = "\uE96B";

        public const string Clock = "\uEA34";
        public const string ClockFilled = "\uEA33";
        public const string ClockEdit = "\uEA9E";
        public const string ClockArrowForward = "\uEA9F";

        public const string Multiplier2xFilled = "\uE9BA";
        public const string PlayCircleFilled = "\uE9AE";
        public const string ChatFilled = "\uE9B0";
        public const string ChatInfoFilled = "\uEA82";
        public const string ChatLinkFilled = "\uEA83";
        public const string ChatSettingsFilled = "\uEA6E";
        public const string TagFilled = "\uEA68";
        public const string HandOpenHeartFilled = "\uEA69";
        public const string StickerFilled = "\uE9AD";
        public const string HeartFilled = "\uE9B6";
        public const string MegaphoneOffFilled = "\uE9AF";
        public const string TopSpeedFilled = "\uE9B3";
        public const string DocumentFilled = "\uE9B4";
        public const string EmojiFilled = "\uE9B1";
        public const string TranslateFilled = "\uE9B2";

        public const string FragmentFilled = "\uEA8E";
        public const string Premium = "\uEA87";
        public const string Premium16 = "\uE9B5";
        public const string LockClosed16 = "\uE9BE";
        public const string Verified16 = "\uE97A";
        public const string Scam16 = "\uE9FA";
        public const string Fake16 = "\uE9F5";

        public const string PersonFilled = "\uE9B8";
        public const string PersonQuestionMarkFilled = "\uEA41";
        public const string PersonDeleteFilled = "\uEA40";
        public const string PeopleFilled = "\uE9B9";
        public const string PinFilled = "\uE9BB";
        public const string PinFilled16 = "\uEA8A";
        public const string LinkFilled = "\uE9BC";
        public const string FolderFilled = "\uE9BD";

        public const string ReactionFilled24 = "\uE9F1";

        public static readonly ChatFolderIcon2[] Folders = new ChatFolderIcon2[]
        {
            ChatFolderIcon2.Cat,
            ChatFolderIcon2.Book,
            ChatFolderIcon2.Money,
            ChatFolderIcon2.Game,
            ChatFolderIcon2.Light,
            ChatFolderIcon2.Like,
            ChatFolderIcon2.Note,
            ChatFolderIcon2.Palette,
            ChatFolderIcon2.Travel,
            ChatFolderIcon2.Sport,
            ChatFolderIcon2.Favorite,
            ChatFolderIcon2.Study,
            ChatFolderIcon2.Airplane,
            ChatFolderIcon2.Private,
            ChatFolderIcon2.Groups,
            ChatFolderIcon2.All,
            ChatFolderIcon2.Unread,
            ChatFolderIcon2.Bots,
            ChatFolderIcon2.Crown,
            ChatFolderIcon2.Flower,
            ChatFolderIcon2.Home,
            ChatFolderIcon2.Love,
            ChatFolderIcon2.Mask,
            ChatFolderIcon2.Party,
            ChatFolderIcon2.Trade,
            ChatFolderIcon2.Work,
            ChatFolderIcon2.Unmuted,
            ChatFolderIcon2.Channels,
            ChatFolderIcon2.Custom,
            ChatFolderIcon2.Setup
        };

        public static ChatFolderIcon2 ParseFolder(ChatFolder folder)
        {
            var icon = folder.Icon;
            if (string.IsNullOrEmpty(icon?.Name))
            {
                var text = Client.Execute(new GetChatFolderDefaultIconName(folder)) as ChatFolderIcon;
                if (text != null)
                {
                    icon = text;
                }
            }

            return ParseFolder(icon);
        }

        public static ChatFolderIcon2 ParseFolder(ChatFolderIcon icon)
        {
            if (icon != null && Enum.TryParse(icon.Name, out ChatFolderIcon2 result))
            {
                return result;
            }

            return ChatFolderIcon2.Custom;
        }

        public static (string, string) FolderToGlyph(ChatFolderIcon2 icon)
        {
            return icon switch
            {
                ChatFolderIcon2.Cat => ("\uE933", "\uE931"),
                ChatFolderIcon2.Crown => ("\uE935", "\uE934"),
                ChatFolderIcon2.Favorite => ("\uE937", "\uE936"),
                ChatFolderIcon2.Flower => ("\uE939", "\uE938"),
                ChatFolderIcon2.Game => ("\uE93B", "\uE93A"),
                ChatFolderIcon2.Home => ("\uE940", "\uE93C"),
                ChatFolderIcon2.Love => ("\uE942", "\uE941"),
                ChatFolderIcon2.Mask => ("\uE945", "\uE944"),
                ChatFolderIcon2.Party => ("\uE948", "\uE947"),
                ChatFolderIcon2.Sport => ("\uE94A", "\uE949"),
                ChatFolderIcon2.Study => ("\uE94C", "\uE94B"),
                ChatFolderIcon2.Trade => ("\uE94E", "\uE94D"),
                ChatFolderIcon2.Travel => ("\uE950", "\uE94F"),
                ChatFolderIcon2.Work => ("\uE952", "\uE951"),
                ChatFolderIcon2.All => ("\uE954", "\uE953"),
                ChatFolderIcon2.Unread => ("\uE9C1", "\uE9C0"),
                ChatFolderIcon2.Unmuted => ("\uE958", "\uE957"),
                ChatFolderIcon2.Bots => ("\uE95A", "\uE959"),
                ChatFolderIcon2.Channels => ("\uE95C", "\uE95B"),
                ChatFolderIcon2.Groups => ("\uE95E", "\uE95D"),
                ChatFolderIcon2.Private => ("\uE960", "\uE95F"),
                ChatFolderIcon2.Custom => ("\uE962", "\uE961"),
                ChatFolderIcon2.Book => ("\uE9CD", "\uE9CC"),
                ChatFolderIcon2.Money => ("\uE9CB", "\uE9CA"),
                ChatFolderIcon2.Light => ("\uE9C9", "\uE9C8"),
                ChatFolderIcon2.Like => ("\uE9C7", "\uE9C6"),
                ChatFolderIcon2.Note => ("\uE9C5", "\uE9C4"),
                ChatFolderIcon2.Palette => ("\uE9C3", "\uE9C2"),
                ChatFolderIcon2.Airplane => ("\uE9D1", "\uE9D1"),
                ChatFolderIcon2.Setup => ("\uE9D0", "\uE9CF"),
                _ => ("\uE962", "\uE961")
            };
        }
    }
}
