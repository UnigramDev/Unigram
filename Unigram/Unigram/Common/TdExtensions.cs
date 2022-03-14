using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Common
{
    public static class TdExtensions
    {
        public static Vector2 ToVector2(this Telegram.Td.Api.Point point)
        {
            return new Vector2((float)point.X, (float)point.Y);
        }

        public static bool IsValidState(this Call call)
        {
            if (call == null || call.State is CallStateDiscarded || call.State is CallStateError)
            {
                return false;
            }

            return true;
        }

        public static File InvalidFile()
        {
            return new File(0, 0, 0, new LocalFile(string.Empty, false, false, false, false, 0, 0, 0), new RemoteFile(string.Empty, string.Empty, false, false, 0));
        }

        public static int ToId(this ChatList chatList)
        {
            if (chatList is ChatListMain or null)
            {
                return 0;
            }
            else if (chatList is ChatListArchive)
            {
                return 1;
            }
            else if (chatList is ChatListFilter filter)
            {
                return filter.ChatFilterId;
            }

            return -1;
        }

        #region Json

        public static bool GetNamedBoolean(this JsonValueObject json, string key, bool defaultValue)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueBoolean value)
            {
                return value.Value;
            }

            return defaultValue;
        }

        public static float GetNamedNumber(this JsonValueObject json, string key, float defaultValue)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueNumber value)
            {
                return (float)value.Value;
            }

            return defaultValue;
        }

        public static string GetNamedString(this JsonValueObject json, string key, string defaultValue)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueString value)
            {
                return value.Value;
            }

            return defaultValue;
        }

        public static JsonValueObject GetNamedObject(this JsonValueObject json, string key)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueObject value)
            {
                return value;
            }

            return null;
        }

        public static JsonObjectMember GetNamedValue(this JsonValueObject json, string key)
        {
            if (json == null)
            {
                return null;
            }

            return json.Members.FirstOrDefault(x => string.Equals(key, x.Key, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        public static string ToOutcomeText(this MessageCall call, bool outgoing)
        {
            var missed = call.DiscardReason is CallDiscardReasonMissed or CallDiscardReasonDeclined;

            if (call.IsVideo)
            {
                return missed ? (outgoing ? Strings.Resources.CallMessageVideoOutgoingMissed : Strings.Resources.CallMessageVideoIncomingMissed) : (outgoing ? Strings.Resources.CallMessageVideoOutgoing : Strings.Resources.CallMessageVideoIncoming);
            }
            else
            {
                return missed ? (outgoing ? Strings.Resources.CallMessageOutgoingMissed : Strings.Resources.CallMessageIncomingMissed) : (outgoing ? Strings.Resources.CallMessageOutgoing : Strings.Resources.CallMessageIncoming);
            }
        }

        public static bool IsMoving(this Background background)
        {
            if (background?.Type is BackgroundTypePattern pattern)
            {
                return pattern.IsMoving;
            }
            else if (background?.Type is BackgroundTypeWallpaper wallpaper)
            {
                return wallpaper.IsMoving;
            }

            return false;
        }

        public static Color GetForeground(this BackgroundTypePattern pattern)
        {
            if (pattern.Fill is BackgroundFillSolid solid)
            {
                return ColorEx.GetPatternColor(solid.Color.ToColor());
            }
            else if (pattern.Fill is BackgroundFillGradient gradient)
            {
                return ColorEx.GetPatternColor(ColorEx.GetAverageColor(gradient.TopColor, gradient.BottomColor));
            }
            else if (pattern.Fill is BackgroundFillFreeformGradient freeform)
            {
                var averageColor = ColorEx.GetAverageColor(freeform.Colors[2], ColorEx.GetAverageColor(freeform.Colors[0], freeform.Colors[1]));
                if (freeform.Colors.Count > 3)
                {
                    averageColor = ColorEx.GetAverageColor(freeform.Colors[3], averageColor);
                }

                return ColorEx.GetPatternColor(averageColor, true);
            }

            return Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
        }

        public static Brush ToBrush(this BackgroundTypeFill fill)
        {
            return fill.Fill.ToBrush();
        }

        public static Brush ToBrush(this BackgroundTypePattern pattern)
        {
            return pattern.Fill.ToBrush();
        }

        public static Brush ToBrush(this BackgroundFill fill)
        {
            if (fill is BackgroundFillSolid solid)
            {
                return new SolidColorBrush(solid.Color.ToColor());
            }
            else if (fill is BackgroundFillGradient gradient)
            {
                return TdBackground.GetGradient(gradient.TopColor, gradient.BottomColor, gradient.RotationAngle);
            }

            return null;
        }

        public static bool IsFreeformGradient(this Background background)
        {
            if (background?.Type is BackgroundTypeFill typeFill)
            {
                return typeFill.Fill is BackgroundFillFreeformGradient;
            }
            else if (background?.Type is BackgroundTypePattern typePattern)
            {
                return typePattern.Fill is BackgroundFillFreeformGradient;
            }

            return false;
        }

        public static Color[] GetColors(this BackgroundFillFreeformGradient freeform)
        {
            return freeform.Colors.Select(x => x.ToColor()).ToArray();
        }

        public static bool ListEquals(this ChatList x, ChatList y, bool allowNull = true)
        {
            if ((x is ChatListMain || x == null) && (y is ChatListMain || (y == null && allowNull)))
            {
                return true;
            }
            if (x is ChatListArchive && y is ChatListArchive)
            {
                return true;
            }
            else if (x is ChatListFilter filterX && y is ChatListFilter filterY)
            {
                return filterX.ChatFilterId == filterY.ChatFilterId;
            }

            return false;
        }

        public static bool IsInstantGallery(this WebPage webPage)
        {
            return webPage.InstantViewVersion != 0 &&
                (string.Equals(webPage.SiteName, "twitter", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(webPage.SiteName, "instagram", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(webPage.Type, "telegram_album", StringComparison.OrdinalIgnoreCase));
        }

        public static bool NeedInfo(this Invoice invoice)
        {
            return invoice.NeedShippingAddress || invoice.NeedPhoneNumber || invoice.NeedName || invoice.NeedEmailAddress;
        }

        public static bool HasVideoInfo(this GroupCallParticipant participant)
        {
            return participant.ScreenSharingVideoInfo != null || participant.VideoInfo != null;
        }

        public static IEnumerable<GroupCallParticipantVideoInfo> GetVideoInfo(this GroupCallParticipant participant)
        {
            if (participant.ScreenSharingVideoInfo != null)
            {
                yield return participant.ScreenSharingVideoInfo;
            }

            if (participant.VideoInfo != null)
            {
                yield return participant.VideoInfo;
            }
        }

        public static InputThumbnail ToInputThumbnail(this PhotoSize photo)
        {
            if (photo == null)
            {
                return null;
            }

            return new InputThumbnail(new InputFileId(photo.Photo.Id), photo.Width, photo.Height);
        }

        public static InputThumbnail ToInput(this Thumbnail thumbnail)
        {
            if (thumbnail == null)
            {
                return null;
            }

            return new InputThumbnail(new InputFileId(thumbnail.File.Id), thumbnail.Width, thumbnail.Height);
        }

        public static bool IsEqualTo(this Message x, Message y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static bool IsEqualTo(this MessageViewModel x, Message y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static bool IsEqualTo(this MessageViewModel x, MessageViewModel y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static IEnumerable<FormattedText> Split(this FormattedText text, long maxLength)
        {
            int count = (int)Math.Ceiling(text.Text.Length / (double)maxLength);
            for (int a = 0; a < count; a++)
            {
                yield return text.Substring(a * maxLength, maxLength);
            }
        }

        public static FormattedText Substring(this FormattedText text, long startIndex, long length)
        {
            if (text.Text.Length < length)
            {
                return text;
            }

            var message = text.Text.Substring((int)startIndex, Math.Min(text.Text.Length - (int)startIndex, (int)length));
            var sub = new List<TextEntity>();

            foreach (var entity in text.Entities)
            {
                // Included, Included
                if (entity.Offset >= startIndex && entity.Offset + entity.Length <= startIndex + length)
                {
                    var replace = new TextEntity { Offset = entity.Offset - (int)startIndex, Length = entity.Length };
                    sub.Add(replace);
                }
                // Before, Included
                else if (entity.Offset < startIndex && entity.Offset + entity.Length > startIndex && entity.Offset + entity.Length <= startIndex + length)
                {
                    var replace = new TextEntity { Offset = 0, Length = entity.Length - ((int)startIndex - entity.Offset) };
                    sub.Add(replace);
                }
                // Included, After
                else if (entity.Offset >= startIndex && entity.Offset < startIndex + length && entity.Offset + entity.Length > startIndex + length)
                {
                    var difference = entity.Offset + entity.Length - startIndex + length;

                    var replace = new TextEntity { Offset = entity.Offset - (int)startIndex, Length = entity.Length - (int)difference };
                    sub.Add(replace);
                }
                // Before, After
                else if (entity.Offset < startIndex && entity.Offset + entity.Length > startIndex + length)
                {
                    var replace = new TextEntity { Offset = 0, Length = message.Length };
                    sub.Add(replace);
                }
            }

            return new FormattedText(message, sub);
        }

        public static string ToPlainText(this PageBlockCaption caption)
        {
            return caption.Text.ToPlainText();
        }

        public static string ToPlainText(this RichText text)
        {
            switch (text)
            {
                case RichTextPlain plainText:
                    return plainText.Text;
                case RichTexts concatText:
                    var builder = new StringBuilder();
                    foreach (var concat in concatText.Texts)
                    {
                        builder.Append(ToPlainText(concat));
                    }
                    return builder.ToString();
                case RichTextAnchorLink anchorLink:
                    return ToPlainText(anchorLink.Text);
                case RichTextBold boldText:
                    return ToPlainText(boldText.Text);
                case RichTextEmailAddress emailText:
                    return ToPlainText(emailText.Text);
                case RichTextFixed fixedText:
                    return ToPlainText(fixedText.Text);
                case RichTextItalic italicText:
                    return ToPlainText(italicText.Text);
                case RichTextStrikethrough strikeText:
                    return ToPlainText(strikeText.Text);
                case RichTextUnderline underlineText:
                    return ToPlainText(underlineText.Text);
                case RichTextUrl urlText:
                    return ToPlainText(urlText.Text);
                default:
                    return null;
            }
        }

        public static bool IsNullOrEmpty(this RichText text)
        {
            if (text == null)
            {
                return true;
            }

            return string.IsNullOrEmpty(text.ToPlainText());
        }

        public static bool IsEditable(this TextEntity entity)
        {
            switch (entity.Type)
            {
                case TextEntityTypeBold:
                case TextEntityTypeItalic:
                case TextEntityTypeCode:
                case TextEntityTypePre:
                case TextEntityTypePreCode:
                case TextEntityTypeTextUrl:
                case TextEntityTypeMentionName:
                    return true;
                default:
                    return false;
            }
        }

        public static Photo GetPhoto(this Message message)
        {
            switch (message.Content)
            {
                case MessageGame game:
                    return game.Game.Photo;
                case MessageInvoice invoice:
                    return invoice.Photo;
                case MessagePhoto photo:
                    return photo.Photo;
                case MessageText text:
                    return text.WebPage?.Photo;
                case MessageChatChangePhoto chatChangePhoto:
                    return chatChangePhoto.Photo.ToPhoto();
                default:
                    return null;
            }
        }

        public static (File File, Thumbnail Thumbnail, string FileName) GetFileAndThumbnailAndName(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return (animation.Animation.AnimationValue, animation.Animation.Thumbnail, animation.Animation.FileName);
                case MessageAudio audio:
                    return (audio.Audio.AudioValue, audio.Audio.AlbumCoverThumbnail, audio.Audio.FileName);
                case MessageDocument document:
                    return (document.Document.DocumentValue, document.Document.Thumbnail, document.Document.FileName);
                case MessageGame game:
                    if (game.Game.Animation != null)
                    {
                        return (game.Game.Animation.AnimationValue, game.Game.Animation.Thumbnail, game.Game.Animation.FileName);
                    }
                    else if (game.Game.Photo != null)
                    {
                        var big = game.Game.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null, null);
                        }
                    }
                    break;
                case MessagePhoto photo:
                    {
                        var big = photo.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null, null);
                        }
                    }
                    break;
                case MessageSticker sticker:
                    return (sticker.Sticker.StickerValue, null, null);
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return (text.WebPage.Animation.AnimationValue, text.WebPage.Animation.Thumbnail, text.WebPage.Animation.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return (text.WebPage.Audio.AudioValue, text.WebPage.Audio.AlbumCoverThumbnail, text.WebPage.Audio.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return (text.WebPage.Document.DocumentValue, text.WebPage.Document.Thumbnail, text.WebPage.Document.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Sticker != null)
                    {
                        return (text.WebPage.Sticker.StickerValue, null, null);
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return (text.WebPage.Video.VideoValue, text.WebPage.Video.Thumbnail, text.WebPage.Video.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return (text.WebPage.VideoNote.Video, text.WebPage.VideoNote.Thumbnail, null);
                    }
                    else if (text.WebPage != null && text.WebPage.VoiceNote != null)
                    {
                        return (text.WebPage.VoiceNote.Voice, null, null);
                    }
                    else if (text.WebPage != null && text.WebPage.Photo != null)
                    {
                        var big = text.WebPage.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null, null);
                        }
                    }
                    break;
                case MessageVideo video:
                    return (video.Video.VideoValue, video.Video.Thumbnail, video.Video.FileName);
                case MessageVideoNote videoNote:
                    return (videoNote.VideoNote.Video, videoNote.VideoNote.Thumbnail, null);
                case MessageVoiceNote voiceNote:
                    return (voiceNote.VoiceNote.Voice, null, null);
            }

            return (null, null, null);
        }

        public static File GetFile(this MessageViewModel message)
        {
            return GetFile(message.GeneratedContent ?? message.Content);
        }

        public static File GetFile(this Message message)
        {
            return GetFile(message.Content);
        }

        public static File GetFile(this MessageContent content)
        {
            switch (content)
            {
                case MessageAnimation animation:
                    return animation.Animation.AnimationValue;
                case MessageAudio audio:
                    return audio.Audio.AudioValue;
                case MessageDocument document:
                    return document.Document.DocumentValue;
                case MessageGame game:
                    if (game.Game.Animation != null)
                    {
                        return game.Game.Animation.AnimationValue;
                    }
                    else if (game.Game.Photo != null)
                    {
                        return game.Game.Photo.GetBig()?.Photo;
                    }
                    break;
                case MessagePhoto photo:
                    return photo.Photo.GetBig()?.Photo;
                case MessageSticker sticker:
                    return sticker.Sticker.StickerValue;
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return text.WebPage.Animation.AnimationValue;
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return text.WebPage.Audio.AudioValue;
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return text.WebPage.Document.DocumentValue;
                    }
                    else if (text.WebPage != null && text.WebPage.Sticker != null)
                    {
                        return text.WebPage.Sticker.StickerValue;
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return text.WebPage.Video.VideoValue;
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Video;
                    }
                    else if (text.WebPage != null && text.WebPage.VoiceNote != null)
                    {
                        return text.WebPage.VoiceNote.Voice;
                    }
                    else if (text.WebPage != null && text.WebPage.Photo != null)
                    {
                        return text.WebPage.Photo.GetBig()?.Photo;
                    }
                    break;
                case MessageVideo video:
                    return video.Video.VideoValue;
                case MessageVideoNote videoNote:
                    return videoNote.VideoNote.Video;
                case MessageVoiceNote voiceNote:
                    return voiceNote.VoiceNote.Voice;
            }

            return null;
        }

        public static bool IsAnimatedContentDownloadCompleted(this MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            switch (content)
            {
                case MessageAnimation animation:
                    return animation.Animation.AnimationValue.Local.IsDownloadingCompleted;
                case MessageSticker sticker:
                    return sticker.Sticker.Type is StickerTypeAnimated or StickerTypeVideo && sticker.Sticker.StickerValue.Local.IsDownloadingCompleted;
                case MessageVideoNote videoNote:
                    return videoNote.VideoNote.Video.Local.IsDownloadingCompleted;
                case MessageGame game:
                    if (game.Game.Animation != null)
                    {
                        return game.Game.Animation.AnimationValue.Local.IsDownloadingCompleted;
                    }
                    return false;
                case MessageText text:
                    if (text.WebPage?.Animation != null)
                    {
                        return text.WebPage.Animation.AnimationValue.Local.IsDownloadingCompleted;
                    }
                    else if (text.WebPage?.Sticker != null)
                    {
                        return text.WebPage.Sticker.Type is StickerTypeAnimated or StickerTypeVideo && text.WebPage.Sticker.StickerValue.Local.IsDownloadingCompleted;
                    }
                    else if (text.WebPage?.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Video.Local.IsDownloadingCompleted;
                    }
                    else if (text.WebPage?.Video != null)
                    {
                        // Videos are streamed
                        return true;
                    }
                    return false;
                case MessageDice dice:
                    var state = dice.InitialState;
                    if (state is DiceStickersRegular regular)
                    {
                        return regular.Sticker.StickerValue.Local.IsDownloadingCompleted;
                    }
                    else if (state is DiceStickersSlotMachine slotMachine)
                    {
                        return slotMachine.Background.StickerValue.Local.IsDownloadingCompleted
                            && slotMachine.LeftReel.StickerValue.Local.IsDownloadingCompleted
                            && slotMachine.CenterReel.StickerValue.Local.IsDownloadingCompleted
                            && slotMachine.RightReel.StickerValue.Local.IsDownloadingCompleted
                            && slotMachine.Lever.StickerValue.Local.IsDownloadingCompleted;
                    }

                    return false;
                case MessageVideo:
                    // Videos are streamed
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsInitialState(this MessageDice dice)
        {
            var state = dice.FinalState;
            if (state == null || !state.IsDownloadingCompleted())
            {
                return true;
            }

            return false;
        }

        public static bool IsFinalState(this MessageDice dice)
        {
            var state = dice.FinalState;
            if (state == null || !state.IsDownloadingCompleted())
            {
                return false;
            }

            return true;
        }

        public static DiceStickers GetState(this MessageDice dice)
        {
            var state = dice.FinalState;
            if (state == null || !state.IsDownloadingCompleted())
            {
                state = dice.InitialState;
            }

            return state;
        }

        public static bool IsDownloadingCompleted(this DiceStickers state)
        {
            if (state is DiceStickersRegular regular)
            {
                return regular.Sticker.StickerValue.Local.IsDownloadingCompleted;
            }
            else if (state is DiceStickersSlotMachine slotMachine)
            {
                return slotMachine.Background.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.LeftReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.CenterReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.RightReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.Lever.StickerValue.Local.IsDownloadingCompleted;
            }

            return false;
        }

        public static Thumbnail GetThumbnail(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return animation.Animation.Thumbnail;
                case MessageAudio audio:
                    return audio.Audio.AlbumCoverThumbnail;
                case MessageDocument document:
                    return document.Document.Thumbnail;
                case MessageGame game:
                    return game.Game.Animation?.Thumbnail;
                case MessageSticker sticker:
                    return sticker.Sticker.Thumbnail;
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return text.WebPage.Animation.Thumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return text.WebPage.Audio.AlbumCoverThumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return text.WebPage.Document.Thumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Sticker != null)
                    {
                        return text.WebPage.Sticker.Thumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return text.WebPage.Video.Thumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Thumbnail;
                    }
                    break;
                case MessageVideo video:
                    return video.Video.Thumbnail;
                case MessageVideoNote videoNote:
                    return videoNote.VideoNote.Thumbnail;
            }

            return null;
        }

        public static Minithumbnail GetMinithumbnail(this Message message, bool secret = false)
        {
            switch (message.Content)
            {
                case MessagePhoto photo:
                    return photo.IsSecret && !secret ? null : photo.Photo.Minithumbnail;
                case MessageAnimation animation:
                    return animation.IsSecret && !secret ? null : animation.Animation.Minithumbnail;
                case MessageAudio audio:
                    return audio.Audio.AlbumCoverMinithumbnail;
                case MessageDocument document:
                    return document.Document.Minithumbnail;
                case MessageGame game:
                    return game.Game.Animation?.Minithumbnail;
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return text.WebPage.Animation.Minithumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return text.WebPage.Audio.AlbumCoverMinithumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return text.WebPage.Document.Minithumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return text.WebPage.Video.Minithumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Minithumbnail;
                    }
                    else if (text.WebPage != null && text.WebPage.Photo != null)
                    {
                        return text.WebPage.Photo.Minithumbnail;
                    }
                    break;
                case MessageVideo video:
                    return video.IsSecret && !secret ? null : video.Video.Minithumbnail;
                case MessageVideoNote videoNote:
                    return videoNote.IsSecret && !secret ? null : videoNote.VideoNote.Minithumbnail;
            }

            return null;
        }

        public static FormattedText GetCaption(this MessageViewModel message)
        {
            return message.Content.GetCaption();
        }

        public static FormattedText GetCaption(this Message message)
        {
            return message.Content.GetCaption();
        }

        public static FormattedText GetCaption(this MessageContent content)
        {
            return content switch
            {
                MessageAlbum album => album.Caption,
                MessageAnimation animation => animation.Caption,
                MessageAudio audio => audio.Caption,
                MessageDocument document => document.Caption,
                MessagePhoto photo => photo.Caption,
                MessageVideo video => video.Caption,
                MessageVoiceNote voiceNote => voiceNote.Caption,
                MessageBigEmoji bigEmoji => bigEmoji.Text,
                MessageText text => text.Text,
                _ => null,
            };
        }

        public static bool HasCaption(this MessageContent content)
        {
            var caption = content.GetCaption();
            return caption != null && !string.IsNullOrEmpty(caption.Text);
        }

        public static Photo ToPhoto(this ChatPhotoInfo chatPhoto)
        {
            return new Photo(false, null, new PhotoSize[] { new PhotoSize("t", chatPhoto.Small, 160, 160, new int[0]), new PhotoSize("i", chatPhoto.Big, 640, 640, new int[0]) });
        }

        public static Photo ToPhoto(this ChatPhoto chatPhoto)
        {
            return new Photo(false, chatPhoto.Minithumbnail, chatPhoto.Sizes);
        }

        public static bool IsSimple(this WebPage webPage)
        {
            return webPage.Animation == null && webPage.Audio == null && webPage.Document == null && webPage.Sticker == null && webPage.Video == null && webPage.VideoNote == null && webPage.VoiceNote == null && webPage.Photo == null;
        }

        public static bool IsMedia(this WebPage webPage)
        {
            if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return webPage.Animation != null || webPage.Audio != null || webPage.Document != null || webPage.Sticker != null || webPage.Video != null || webPage.VideoNote != null || webPage.VoiceNote != null || webPage.IsPhoto();
        }

        public static bool IsPhoto(this WebPage webPage)
        {
            if (webPage.Photo != null && webPage.Type != null)
            {
                if (string.Equals(webPage.Type, "photo", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "video", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "embed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "gif", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "document", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "telegram_album", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(webPage.Type, "article", StringComparison.OrdinalIgnoreCase))
                {
                    var photo = webPage.Photo;
                    var big = photo.GetBig();

                    return big != null && big.Width > 256 && webPage.InstantViewVersion != 0;
                }
            }

            return false;
        }

        public static bool IsSmallPhoto(this WebPage webPage)
        {
            if (webPage.Photo != null && (webPage.SiteName.Length > 0 || webPage.Title.Length > 0 || webPage.Author.Length > 0 || webPage.Description?.Text.Length > 0))
            {
                return !webPage.IsMedia();
            }

            return false;
        }

        public static bool IsSecret(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return animation.IsSecret;
                case MessagePhoto photo:
                    return photo.IsSecret;
                case MessageVideo video:
                    return video.IsSecret;
                case MessageVideoNote videoNote:
                    return videoNote.IsSecret;

                //case MessageExpiredPhoto expiredPhoto:
                //case MessageExpiredVideo expiredVideo:
                //    return true;
                default:
                    return false;
            }
        }

        public static bool IsService(this Message message)
        {
            switch (message.Content)
            {
                case MessageBasicGroupChatCreate:
                case MessageChatAddMembers:
                case MessageChatChangePhoto:
                case MessageChatChangeTitle:
                case MessageChatSetTheme:
                case MessageChatDeleteMember:
                case MessageChatDeletePhoto:
                case MessageChatJoinByLink:
                case MessageChatJoinByRequest:
                case MessageChatSetTtl:
                case MessageChatUpgradeFrom:
                case MessageChatUpgradeTo:
                case MessageContactRegistered:
                case MessageCustomServiceAction:
                case MessageGameScore:
                case MessageInviteVideoChatParticipants:
                case MessageProximityAlertTriggered:
                case MessagePassportDataSent:
                case MessagePaymentSuccessful:
                case MessagePinMessage:
                case MessageScreenshotTaken:
                case MessageSupergroupChatCreate:
                case MessageVideoChatEnded:
                case MessageVideoChatScheduled:
                case MessageVideoChatStarted:
                case MessageWebsiteConnected:
                    return true;
                case MessageExpiredPhoto:
                case MessageExpiredVideo:
                    return true;
                // Local types:
                case MessageChatEvent:
                case MessageHeaderDate:
                case MessageHeaderUnread:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMedia(this InlineQueryResult result)
        {
            switch (result)
            {
                case InlineQueryResultAnimation:
                case InlineQueryResultPhoto:
                    return true;
                case InlineQueryResultVideo video:
                    return string.IsNullOrEmpty(video.Title);
                default:
                    return false;
            }
        }

        public static string GetId(this InlineQueryResult result)
        {
            switch (result)
            {
                case InlineQueryResultAnimation animation:
                    return animation.Id;
                case InlineQueryResultArticle article:
                    return article.Id;
                case InlineQueryResultAudio audio:
                    return audio.Id;
                case InlineQueryResultContact contact:
                    return contact.Id;
                case InlineQueryResultDocument document:
                    return document.Id;
                case InlineQueryResultGame game:
                    return game.Id;
                case InlineQueryResultLocation location:
                    return location.Id;
                case InlineQueryResultPhoto photo:
                    return photo.Id;
                case InlineQueryResultSticker sticker:
                    return sticker.Id;
                case InlineQueryResultVenue venue:
                    return venue.Id;
                case InlineQueryResultVideo video:
                    return video.Id;
                case InlineQueryResultVoiceNote voiceNote:
                    return voiceNote.Id;
                default:
                    return null;
            }
        }

        public static Sticker GetThumbnail(this StickerSetInfo stickerSet)
        {
            if (stickerSet.Thumbnail != null)
            {
                StickerType type = stickerSet.Thumbnail.Format switch
                {
                    ThumbnailFormatWebp => new StickerTypeStatic(),
                    ThumbnailFormatWebm => new StickerTypeVideo(),
                    ThumbnailFormatTgs => new StickerTypeAnimated(),
                    _ => default
                };

                if (stickerSet.Thumbnail.Format is ThumbnailFormatTgs)
                {
                    return new Sticker(stickerSet.Id, 512, 512, "\U0001F4A9", type, stickerSet.ThumbnailOutline, stickerSet.Thumbnail, stickerSet.Thumbnail.File);
                }

                return new Sticker(stickerSet.Id, stickerSet.Thumbnail.Width, stickerSet.Thumbnail.Height, "\U0001F4A9", type, stickerSet.ThumbnailOutline, stickerSet.Thumbnail, stickerSet.Thumbnail.File);
            }

            var cover = stickerSet.Covers.FirstOrDefault();
            if (cover != null)
            {
                return cover;
            }

            return null;
        }

        public static bool IsUnread(this Chat chat)
        {
            if (chat.IsMarkedAsUnread)
            {
                return true;
            }

            return chat.UnreadCount > 0;
        }

        public static bool IsForever(this ChatMemberStatusRestricted restricted)
        {
            return restricted.RestrictedUntilDate == 0 || Math.Abs((restricted.RestrictedUntilDate - DateTime.Now.ToTimestamp()) / 1000) > 5 * 365 * 24 * 60 * 60;
        }

        public static bool IsForever(this ChatMemberStatusBanned banned)
        {
            return banned.BannedUntilDate == 0 || Math.Abs((banned.BannedUntilDate - DateTime.Now.ToTimestamp()) / 1000) > 5 * 365 * 24 * 60 * 60;
        }


        public static string GetTitle(this Audio audio)
        {
            var performer = string.IsNullOrEmpty(audio.Performer) ? null : audio.Performer;
            var title = string.IsNullOrEmpty(audio.Title) ? null : audio.Title;

            if (performer == null)
            {
                if (title == null)
                {
                    return audio.FileName;
                }

                return title;
            }
            else
            {
                return $"{performer} - {title}";
            }
        }

        public static ChatPosition GetPosition(this Chat chat, ChatList chatList)
        {
            if (chat == null)
            {
                return null;
            }

            Monitor.Enter(chat);

            for (int i = 0; i < chat.Positions.Count; i++)
            {
                if (chat.Positions[i].List.ListEquals(chatList))
                {
                    Monitor.Exit(chat);
                    return chat.Positions[i];
                }
            }

            Monitor.Exit(chat);
            return null;
        }

        public static long GetOrder(this Chat chat, ChatList chatList)
        {
            if (chat == null)
            {
                return 0;
            }

            Monitor.Enter(chat);

            for (int i = 0; i < chat.Positions.Count; i++)
            {
                if (chat.Positions[i].List.ListEquals(chatList))
                {
                    Monitor.Exit(chat);
                    return chat.Positions[i].Order;
                }
            }

            Monitor.Exit(chat);
            return 0;
        }

        public static TdNetworkType GetNetworkType(this NetworkStatisticsEntry entry)
        {
            return TdNetworkType.WiFi;

            if (entry is NetworkStatisticsEntryCall call)
            {
                switch (call.NetworkType)
                {
                    case NetworkTypeMobile:
                        return TdNetworkType.Mobile;
                    case NetworkTypeMobileRoaming:
                        return TdNetworkType.MobileRoaming;
                    case NetworkTypeNone:
                        return TdNetworkType.None;
                    case NetworkTypeOther:
                    //return TdNetworkType.Other;
                    case NetworkTypeWiFi:
                        return TdNetworkType.WiFi;
                }
            }
            else if (entry is NetworkStatisticsEntryFile file)
            {
                switch (file.NetworkType)
                {
                    case NetworkTypeMobile:
                        return TdNetworkType.Mobile;
                    case NetworkTypeMobileRoaming:
                        return TdNetworkType.MobileRoaming;
                    case NetworkTypeNone:
                        return TdNetworkType.None;
                    case NetworkTypeOther:
                    //return TdNetworkType.Other;
                    case NetworkTypeWiFi:
                        return TdNetworkType.WiFi;
                }
            }

            return TdNetworkType.Other;
        }

        public static bool IsEqual(this MessageSender sender, MessageSender compare)
        {
            if (sender is MessageSenderUser user1 && compare is MessageSenderUser user2)
            {
                return user1.UserId == user2.UserId;
            }
            else if (sender is MessageSenderChat chat1 && compare is MessageSenderChat chat2)
            {
                return chat1.ChatId == chat2.ChatId;
            }

            return false;
        }

        public static bool IsUser(this MessageSender sender, long userId)
        {
            return sender is MessageSenderUser user && user.UserId == userId;
        }

        public static bool IsChat(this MessageSender sender, long chatId)
        {
            return sender is MessageSenderChat chat && chat.ChatId == chatId;
        }

        public static long ComparaTo(this MessageSender sender, MessageSender compare)
        {
            if (sender is MessageSenderUser user1 && compare is MessageSenderUser user2)
            {
                return user1.UserId - user2.UserId;
            }
            else if (sender is MessageSenderChat chat1 && compare is MessageSenderChat chat2)
            {
                return chat1.ChatId - chat2.ChatId;
            }

            return -1;
        }

        public static bool IsSaved(this Message message, long savedMessagesId)
        {
            if (message.ForwardInfo?.Origin is MessageForwardOriginUser)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChat)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginMessageImport)
            {
                return true;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
            {
                return message.ChatId == savedMessagesId;
            }

            return false;
        }

        public static string GetFullName(this User user)
        {
            if (user == null || user.Type is UserTypeDeleted)
            {
                return Strings.Resources.HiddenName;
            }

            if (user.FirstName.Length > 0 && user.LastName.Length > 0)
            {
                return $"{user.FirstName} {user.LastName}";
            }
            else if (user.FirstName.Length > 0)
            {
                return user.FirstName;
            }

            return user.LastName;
        }

        public static string GetFullName(this Contact user)
        {
            if (user.FirstName.Length > 0 && user.LastName.Length > 0)
            {
                return $"{user.FirstName} {user.LastName}";
            }
            else if (user.FirstName.Length > 0)
            {
                return user.FirstName;
            }

            return user.LastName;
        }

        public static PhotoSize GetSize(this Photo photo, bool thumbnail)
        {
            return thumbnail ? photo.GetSmall() : photo.GetBig();
        }

        public static PhotoSize GetSmall(this Photo photo)
        {
            //var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "t"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            return photo.Sizes.FirstOrDefault(x => x.Photo.Local.IsDownloadingCompleted || x.Photo.Local.CanBeDownloaded);
        }

        public static PhotoSize GetBig(this Photo photo)
        {
            //var local = photo.Sizes.LastOrDefault(x => string.Equals(x.Type, "i"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            return photo.Sizes.LastOrDefault(x => x.Photo.Local.IsDownloadingCompleted || x.Photo.Local.CanBeDownloaded);
        }

        public static PhotoSize GetSmall(this ChatPhoto photo)
        {
            //var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "t"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            return photo.Sizes.FirstOrDefault(x => x.Photo.Local.IsDownloadingCompleted || x.Photo.Local.CanBeDownloaded);
        }

        public static PhotoSize GetBig(this ChatPhoto photo)
        {
            //var local = photo.Sizes.LastOrDefault(x => string.Equals(x.Type, "i"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            return photo.Sizes.LastOrDefault(x => x.Photo.Local.IsDownloadingCompleted || x.Photo.Local.CanBeDownloaded);
        }

        public static string GetStartsAt(this MessageVideoChatScheduled messageVideoChatScheduled)
        {
            var date = Converters.Converter.DateTime(messageVideoChatScheduled.StartDate);
            return string.Format(Strings.Resources.formatDateAtTime, Converters.Converter.ShortDate.Format(date), Converters.Converter.ShortTime.Format(date));
        }

        public static string GetStartsAt(this GroupCall groupCall)
        {
            var date = Converters.Converter.DateTime(groupCall.ScheduledStartDate);
            if (date.Date == DateTime.Today)
            {
                return string.Format(Strings.Resources.TodayAtFormattedWithToday, Converters.Converter.ShortTime.Format(date));
            }
            else if (date.Date.AddDays(1) == DateTime.Today)
            {
                return string.Format(Strings.Resources.YesterdayAtFormatted, Converters.Converter.ShortTime.Format(date));
            }

            return string.Format(Strings.Resources.formatDateAtTime, Converters.Converter.ShortDate.Format(date), Converters.Converter.ShortTime.Format(date));
        }

        public static string GetStartsIn(this GroupCall groupCall)
        {
            var date = Converters.Converter.DateTime(groupCall.ScheduledStartDate);
            var duration = date - DateTime.Now;

            if (Math.Abs(duration.TotalDays) >= 7)
            {
                return Locale.Declension("Weeks", 1);
            }
            if (Math.Abs(duration.TotalDays) >= 1)
            {
                return Locale.Declension("Days", (int)duration.TotalDays);
            }
            else if (Math.Abs(duration.TotalHours) >= 1)
            {
                return (duration.TotalSeconds < 0 ? "-" : "") + duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return (duration.TotalSeconds < 0 ? "-" : "") + duration.ToString("mm\\:ss");
            }
        }

        public static string GetDuration(this MessageVideoChatEnded videoChatEnded)
        {
            var duration = TimeSpan.FromSeconds(videoChatEnded.Duration);
            if (duration.TotalDays >= 1)
            {
                return Locale.Declension("Days", (int)duration.TotalDays);
            }
            else if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static string GetDuration(this Video video)
        {
            var duration = TimeSpan.FromSeconds(video.Duration);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static string GetDuration(this Audio audio)
        {
            var duration = TimeSpan.FromSeconds(audio.Duration);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static string GetDuration(this VoiceNote voiceNote)
        {
            var duration = TimeSpan.FromSeconds(voiceNote.Duration);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }

        public static string GetDuration(this VideoNote videoNote)
        {
            var duration = TimeSpan.FromSeconds(videoNote.Duration);
            if (duration.TotalHours >= 1)
            {
                return duration.ToString("h\\:mm\\:ss");
            }
            else
            {
                return duration.ToString("mm\\:ss");
            }
        }






        public static bool IsMember(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator or
                ChatMemberStatusAdministrator or
                ChatMemberStatusMember or
                ChatMemberStatusRestricted;
        }

        public static int Count(this ChatPermissions permissions)
        {
            var count = 0;
            if (permissions.CanAddWebPagePreviews)
            {
                count++;
            }
            if (permissions.CanChangeInfo)
            {
                count++;
            }
            if (permissions.CanInviteUsers)
            {
                count++;
            }
            if (permissions.CanPinMessages)
            {
                count++;
            }
            if (permissions.CanSendMediaMessages)
            {
                count++;
            }
            if (permissions.CanSendMessages)
            {
                count++;
            }
            if (permissions.CanSendOtherMessages)
            {
                count++;
            }
            if (permissions.CanSendPolls)
            {
                count++;
            }

            return count;
        }

        public static int Total(this ChatPermissions permissions)
        {
            return 8;
        }

        public static bool CanPinMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPinMessages;
        }

        public static bool CanDeleteMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanDeleteMessages;
        }

        public static bool CanPinMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPinMessages;
        }

        public static bool CanDeleteMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanDeleteMessages;
        }

        public static bool CanChangeInfo(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanChangeInfo;
        }

        public static bool CanManageVideoChats(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanManageVideoChats;
        }

        public static bool CanManageVideoChats(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanManageVideoChats;
        }

        public static bool CanPostMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel)
            {
                return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPostMessages;
            }
            else
            {
                return supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator or ChatMemberStatusMember;
            }
        }

        public static bool CanRestrictMembers(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanRestrictMembers;
        }

        public static bool CanPromoteMembers(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPromoteMembers;
        }

        public static bool CanPromoteMembers(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator;
        }

        public static bool CanInviteUsers(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel && supergroup.MemberCount > 200)
            {
                return false;
            }

            //if (supergroup.AnyoneCanInvite && supergroup.Status is ChatMemberStatusMember)
            //{
            //    return true;
            //}

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers;
        }

        public static bool CanInviteUsers(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            //if (basicGroup.EveryoneIsAdministrator)
            //{
            //    return true;
            //}

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers;
        }

        public static bool CanPostMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator or ChatMemberStatusMember;
        }

        public static void Update(this File file, File update)
        {
            file.ExpectedSize = update.ExpectedSize;
            file.Size = update.Size;
            file.Local = update.Local;
            file.Remote = update.Remote;
        }

        public static File GetLocalFile(string path, string uniqueId = "")
        {
            return new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, path), false, false, false, true, 0, 0, 0), new RemoteFile(string.Empty, uniqueId, false, false, 0));
        }
    }

    public static class TdBackground
    {
        public static BackgroundType FromUri(Uri uri)
        {
            var slug = uri.Segments.Last();
            var query = uri.Query.ParseQueryString();

            if (TryGetColors(slug, '-', 1, 2, out int[] linear))
            {
                if (linear.Length > 1)
                {
                    query.TryGetValue("rotation", out string rotationKey);
                    int.TryParse(rotationKey ?? string.Empty, out int rotation);

                    return new BackgroundTypeFill(new BackgroundFillGradient(linear[0], linear[1], rotation));
                }

                return new BackgroundTypeFill(new BackgroundFillSolid(linear[0]));
            }
            else if (TryGetColors(slug, '~', 3, 4, out int[] freeform))
            {
                return new BackgroundTypeFill(new BackgroundFillFreeformGradient(freeform));
            }
            else
            {
                query.TryGetValue("mode", out string modeKey);
                query.TryGetValue("bg_color", out string bg_colorKey);

                var modeSplit = modeKey?.ToLower().Split('+') ?? new string[0];

                BackgroundFill fill = null;
                if (bg_colorKey != null && TryGetColors(bg_colorKey, '-', 1, 2, out int[] patternLinear))
                {
                    if (patternLinear.Length > 1)
                    {
                        query.TryGetValue("rotation", out string rotationKey);
                        int.TryParse(rotationKey ?? string.Empty, out int rotation);

                        fill = new BackgroundFillGradient(patternLinear[0], patternLinear[1], rotation);
                    }
                    else
                    {
                        fill = new BackgroundFillSolid(patternLinear[0]);
                    }
                }
                else if (bg_colorKey != null && TryGetColors(bg_colorKey, '~', 3, 4, out int[] patternFreeform))
                {
                    fill = new BackgroundFillFreeformGradient(patternFreeform);
                }

                if (fill != null)
                {
                    query.TryGetValue("intensity", out string intensityKey);
                    int.TryParse(intensityKey, out int intensity);

                    return new BackgroundTypePattern(fill, Math.Abs(intensity), intensity < 0, modeSplit.Contains("motion"));
                }
                else
                {
                    return new BackgroundTypeWallpaper(modeSplit.Contains("blur"), modeSplit.Contains("motion"));
                }
            }
        }

        private static bool TryGetColors(string slug, char separator, int minimum, int maximum, out int[] colors)
        {
            var split = slug?.Split(separator);
            if (split != null && split.Length >= minimum && split.Length >= maximum)
            {
                colors = new int[split.Length];

                for (int i = 0; i < split.Length; i++)
                {
                    if (int.TryParse(split[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color))
                    {
                        colors[i] = color;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            colors = null;
            return false;
        }

        public static LinearGradientBrush GetGradient(int topColor, int bottomColor, int angle)
        {
            return GetGradient(topColor.ToColor(), bottomColor.ToColor(), angle);
        }

        public static LinearGradientBrush GetGradient(Color topColor, Color bottomColor, int angle)
        {
            Point topPoint;
            Point bottomPoint;

            switch (angle)
            {
                case 0:
                case 360:
                    topPoint = new Point(0.5, 0);
                    bottomPoint = new Point(0.5, 1);
                    break;
                case 45:
                default:
                    topPoint = new Point(1, 0);
                    bottomPoint = new Point(0, 1);
                    break;
                case 90:
                    topPoint = new Point(1, 0.5);
                    bottomPoint = new Point(0, 0.5);
                    break;
                case 135:
                    topPoint = new Point(1, 1);
                    bottomPoint = new Point(0, 0);
                    break;
                case 180:
                    topPoint = new Point(0.5, 1);
                    bottomPoint = new Point(0.5, 0);
                    break;
                case 225:
                    topPoint = new Point(0, 1);
                    bottomPoint = new Point(1, 0);
                    break;
                case 270:
                    topPoint = new Point(0, 0.5);
                    bottomPoint = new Point(1, 0.5);
                    break;
                case 315:
                    topPoint = new Point(0, 0);
                    bottomPoint = new Point(1, 1);
                    break;
            }

            var brush = new LinearGradientBrush();
            brush.GradientStops.Add(new GradientStop { Color = topColor, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = bottomColor, Offset = 1 });
            brush.StartPoint = topPoint;
            brush.EndPoint = bottomPoint;

            return brush;
        }
    }
}

namespace Telegram.Td.Api
{
    public class MessageBigEmoji : MessageContent
    {
        public FormattedText Text { get; set; }

        public int Count { get; set; }

        public MessageBigEmoji()
        {

        }

        public MessageBigEmoji(FormattedText text, int count)
        {
            Text = text;
            Count = count;
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class MessageAlbum : MessageContent
    {
        public bool IsMedia { get; }

        public FormattedText Caption { get; set; }

        public UniqueList<long, MessageViewModel> Messages { get; } = new UniqueList<long, MessageViewModel>(x => x.Id);

        public MessageAlbum(bool media)
        {
            IsMedia = media;
        }

        public const int ITEM_MARGIN = 0;
        public const int MAX_WIDTH = 420 + ITEM_MARGIN;
        public const int MAX_HEIGHT = 420 + ITEM_MARGIN;

        private IList<GroupMediaLayout> _positions;

        public void Invalidate()
        {
            _positions = null;
        }

        public (Rect[], Size) GetPositionsForWidth(double w)
        {
            var positions = _positions ??= MosaicAlbumLayout.LayoutMediaGroup(GetSizes(), MAX_WIDTH, MAX_HEIGHT, 2);

            var dimensions = new Size();
            foreach (var itemInfo in positions)
            {
                dimensions.Width = Math.Max(dimensions.Width, Math.Round(itemInfo.Geometry.Right));
                dimensions.Height = Math.Max(dimensions.Height, Math.Round(itemInfo.Geometry.Bottom));
            }

            var ratio = w / MAX_WIDTH;
            var rects = new Rect[positions.Count];

            for (int i = 0; i < rects.Length; i++)
            {
                var rect = positions[i].Geometry;
                rects[i] = new Rect(rect.X * ratio, rect.Y * ratio, rect.Width * ratio, rect.Height * ratio);
            }

            return (rects, new Size(dimensions.Width * ratio, dimensions.Height * ratio));
        }

        private IList<Size> GetSizes()
        {
            var sizes = new List<Size>(Messages.Count);

            foreach (var message in Messages)
            {
                if (message.Content is MessagePhoto photoMedia)
                {
                    sizes.Add(GetClosestPhotoSizeWithSize(photoMedia.Photo.Sizes, 1280, false));
                }
                else if (message.Content is MessageVideo videoMedia)
                {
                    if (videoMedia.Video.Width != 0 && videoMedia.Video.Height != 0)
                    {
                        sizes.Add(new Size(videoMedia.Video.Width, videoMedia.Video.Height));
                    }
                    else if (videoMedia.Video.Thumbnail != null)
                    {
                        sizes.Add(new Size(videoMedia.Video.Thumbnail.Width, videoMedia.Video.Thumbnail.Height));
                    }
                    else
                    {
                        // We are returning a random size, it's still better than NaN.
                        sizes.Add(new Size(1280, 1280));
                    }
                }
            }

            return sizes;
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side)
        {
            return GetClosestPhotoSizeWithSize(sizes, side, false);
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side, bool byMinSide)
        {
            if (sizes == null || sizes.IsEmpty())
            {
                // We are returning a random size, it's still better than NaN.
                return new Size(1280, 1280);
            }

            int lastSide = 0;
            PhotoSize closestObject = null;
            for (int a = 0; a < sizes.Count; a++)
            {
                PhotoSize obj = sizes[a];
                if (obj == null)
                {
                    continue;
                }

                int w = obj.Width;
                int h = obj.Height;

                if (byMinSide)
                {
                    int currentSide = h >= w ? w : h;
                    if (closestObject == null || side > 100 && side > lastSide && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
                else
                {
                    int currentSide = w >= h ? w : h;
                    if (closestObject == null || side > 100 && currentSide <= side && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
            }

            return new Size(closestObject.Width, closestObject.Height);
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class MessageChatEvent : MessageContent
    {
        /// <summary>
        /// Action performed by the user.
        /// </summary>
        public ChatEventAction Action { get; set; }

        /// <summary>
        /// Identifier of the user who performed the action that triggered the event.
        /// </summary>
        public MessageSender MemberId { get; set; }

        /// <summary>
        /// Point in time (Unix timestamp) when the event happened.
        /// </summary>
        public int Date { get; set; }

        /// <summary>
        /// Chat event identifier.
        /// </summary>
        public long Id { get; set; }

        public MessageChatEvent(ChatEvent chatEvent)
        {
            Action = chatEvent.Action;
            MemberId = chatEvent.MemberId;
            Date = chatEvent.Date;
            Id = chatEvent.Id;
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class MessageHeaderDate : MessageContent
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class MessageHeaderUnread : MessageContent
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }
}
