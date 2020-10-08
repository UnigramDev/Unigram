using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public static class TdExtensions
    {
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
            if (chatList is ChatListMain || chatList == null)
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

        public static double GetNamedNumber(this JsonValueObject json, string key, double defaultValue)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueNumber value)
            {
                return value.Value;
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
            var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

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
                return ColorEx.GetPatternColor(ColorEx.GetAverageColor(gradient.TopColor.ToColor(), gradient.BottomColor.ToColor()));
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

        public static bool AreEqual(this Message x, Message y)
        {
            if (y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static IEnumerable<FormattedText> Split(this FormattedText text, int maxLength)
        {
            int count = (int)Math.Ceiling(text.Text.Length / (double)maxLength);
            for (int a = 0; a < count; a++)
            {
                yield return text.Substring(a * maxLength, maxLength);
            }
        }

        public static FormattedText Substring(this FormattedText text, int startIndex, int length)
        {
            if (text.Text.Length < length)
            {
                return text;
            }

            var message = text.Text.Substring(startIndex, Math.Min(text.Text.Length - startIndex, length));
            var sub = new List<TextEntity>();

            foreach (var entity in text.Entities)
            {
                // Included, Included
                if (entity.Offset >= startIndex && entity.Offset + entity.Length <= startIndex + length)
                {
                    var replace = new TextEntity { Offset = entity.Offset - startIndex, Length = entity.Length };
                    sub.Add(replace);
                }
                // Before, Included
                else if (entity.Offset < startIndex && entity.Offset + entity.Length > startIndex && entity.Offset + entity.Length <= startIndex + length)
                {
                    var replace = new TextEntity { Offset = 0, Length = entity.Length - (startIndex - entity.Offset) };
                    sub.Add(replace);
                }
                // Included, After
                else if (entity.Offset >= startIndex && entity.Offset < startIndex + length && entity.Offset + entity.Length > startIndex + length)
                {
                    var difference = (entity.Offset + entity.Length) - startIndex + length;

                    var replace = new TextEntity { Offset = entity.Offset - startIndex, Length = entity.Length - difference };
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
                case TextEntityTypeBold bold:
                case TextEntityTypeItalic italic:
                case TextEntityTypeCode code:
                case TextEntityTypePre pre:
                case TextEntityTypePreCode preCode:
                case TextEntityTypeTextUrl textUrl:
                case TextEntityTypeMentionName mentionName:
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

        public static (File File, string FileName) GetFileAndName(this Message message, bool allowPhoto)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return (animation.Animation.AnimationValue, animation.Animation.FileName);
                case MessageAudio audio:
                    return (audio.Audio.AudioValue, audio.Audio.FileName);
                case MessageDocument document:
                    return (document.Document.DocumentValue, document.Document.FileName);
                case MessageGame game:
                    if (game.Game.Animation != null)
                    {
                        return (game.Game.Animation.AnimationValue, game.Game.Animation.FileName);
                    }
                    else if (game.Game.Photo != null && allowPhoto)
                    {
                        var big = game.Game.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null);
                        }
                    }
                    break;
                case MessagePhoto photo:
                    if (allowPhoto)
                    {
                        var big = photo.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null);
                        }
                    }
                    break;
                case MessageSticker sticker:
                    return (sticker.Sticker.StickerValue, null);
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return (text.WebPage.Animation.AnimationValue, text.WebPage.Animation.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return (text.WebPage.Audio.AudioValue, text.WebPage.Audio.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return (text.WebPage.Document.DocumentValue, text.WebPage.Document.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.Sticker != null)
                    {
                        return (text.WebPage.Sticker.StickerValue, null);
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return (text.WebPage.Video.VideoValue, text.WebPage.Video.FileName);
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return (text.WebPage.VideoNote.Video, null);
                    }
                    else if (text.WebPage != null && text.WebPage.VoiceNote != null)
                    {
                        return (text.WebPage.VoiceNote.Voice, null);
                    }
                    else if (text.WebPage != null && text.WebPage.Photo != null && allowPhoto)
                    {
                        var big = text.WebPage.Photo.GetBig();
                        if (big != null)
                        {
                            return (big.Photo, null);
                        }
                    }
                    break;
                case MessageVideo video:
                    return (video.Video.VideoValue, video.Video.FileName);
                case MessageVideoNote videoNote:
                    return (videoNote.VideoNote.Video, null);
                case MessageVoiceNote voiceNote:
                    return (voiceNote.VoiceNote.Voice, null);
            }

            return (null, null);
        }

        public static File GetFile(this MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            switch (content)
            {
                case MessageAnimation animation:
                    return animation.Animation.AnimationValue;
                case MessageAudio audio:
                    return audio.Audio.AudioValue;
                case MessageDocument document:
                    return document.Document.DocumentValue;
                case MessageGame game:
                    return game.Game.Animation?.AnimationValue;
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

        public static File GetFile(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return animation.Animation.AnimationValue;
                case MessageAudio audio:
                    return audio.Audio.AudioValue;
                case MessageDocument document:
                    return document.Document.DocumentValue;
                case MessageGame game:
                    return game.Game.Animation?.AnimationValue;
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

        public static File GetAnimation(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return animation.Animation.AnimationValue;
                case MessageGame game:
                    return game.Game.Animation?.AnimationValue;
                case MessageText text:
                    return text.WebPage?.Animation?.AnimationValue;

                case MessageVideoNote videoNote:
                    return videoNote.VideoNote.Video;
                default:
                    return null;
            }
        }

        public static File GetAnimatedSticker(this MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            switch (content)
            {
                case MessageSticker sticker:
                    return sticker.Sticker.IsAnimated ? sticker.Sticker.StickerValue : null;
                case MessageText text:
                    return text.WebPage?.Sticker?.IsAnimated ?? false ? text.WebPage?.Sticker?.StickerValue : null;
                default:
                    return null;
            }
        }

        public static File GetAnimatedSticker(this Message message)
        {
            switch (message.Content)
            {
                case MessageSticker sticker:
                    return sticker.Sticker.IsAnimated ? sticker.Sticker.StickerValue : null;
                case MessageText text:
                    return text.WebPage?.Sticker?.IsAnimated ?? false ? text.WebPage?.Sticker?.StickerValue : null;
                default:
                    return null;
            }
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

        public static Minithumbnail GetMinithumbnail(this Message message, bool secret)
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

        public static FormattedText GetCaption(this Message message)
        {
            return message.Content.GetCaption();
        }

        public static FormattedText GetCaption(this MessageContent content)
        {
            switch (content)
            {
                case MessageAlbum album:
                    return album.Caption;
                case MessageAnimation animation:
                    return animation.Caption;
                case MessageAudio audio:
                    return audio.Caption;
                case MessageDocument document:
                    return document.Caption;
                case MessagePhoto photo:
                    return photo.Caption;
                case MessageVideo video:
                    return video.Caption;
                case MessageVoiceNote voiceNote:
                    return voiceNote.Caption;

                case MessageText text:
                    return text.Text;
            }

            return null;
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
            return webPage.Animation != null || webPage.Audio != null || webPage.Document != null || webPage.Sticker != null || webPage.Video != null || webPage.VideoNote != null || webPage.VoiceNote != null || webPage.IsPhoto();
        }

        public static bool IsPhoto(this WebPage webPage)
        {
            if (webPage.Photo != null && webPage.Type != null)
            {
                if (string.Equals(webPage.Type, "photo", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "video", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "telegram_album", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(webPage.Type, "article", StringComparison.OrdinalIgnoreCase))
                {
                    var photo = webPage.Photo;
                    var big = photo.GetBig();

                    return big != null && big.Width > 400 && webPage.InstantViewVersion != 0;
                }
            }

            return webPage.Photo != null;
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
                case MessageBasicGroupChatCreate basicGroupChatCreate:
                case MessageChatAddMembers chatAddMembers:
                case MessageChatChangePhoto chatChangePhoto:
                case MessageChatChangeTitle chatChangeTitle:
                case MessageChatDeleteMember chatDeleteMember:
                case MessageChatDeletePhoto chatDeletePhoto:
                case MessageChatJoinByLink chatJoinByLink:
                case MessageChatSetTtl chatSetTtl:
                case MessageChatUpgradeFrom chatUpgradeFrom:
                case MessageChatUpgradeTo chatUpgradeTo:
                case MessageContactRegistered contactRegistered:
                case MessageCustomServiceAction customServiceAction:
                case MessageGameScore gameScore:
                case MessagePassportDataSent passportDataSent:
                case MessagePaymentSuccessful paymentSuccessful:
                case MessagePinMessage pinMessage:
                case MessageScreenshotTaken screenshotTaken:
                case MessageSupergroupChatCreate supergroupChatCreate:
                case MessageWebsiteConnected websiteConnected:
                    return true;
                case MessageExpiredPhoto expiredPhoto:
                case MessageExpiredVideo expiredVideo:
                    return true;
                // Local types:
                case MessageChatEvent chatEvent:
                case MessageHeaderDate headerDate:
                case MessageHeaderUnread headerUnread:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMedia(this InlineQueryResult result)
        {
            switch (result)
            {
                case InlineQueryResultAnimation animation:
                case InlineQueryResultPhoto photo:
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

        public static string GetRestrictionReason(this User user)
        {
            return GetRestrictionReason(user.RestrictionReason);
        }

        public static string GetRestrictionReason(this Supergroup supergroup)
        {
            return GetRestrictionReason(supergroup.RestrictionReason);
        }

        public static string GetRestrictionReason(string reason)
        {
            if (reason.Length > 0)
            {
                var fullTypeEnd = reason.IndexOf(':');
                if (fullTypeEnd <= 0)
                {
                    return null;
                }

                // {fulltype} is in "{type}-{tag}-{tag}-{tag}" format
                // if we find "all" tag we return the restriction string
                var typeTags = reason.Substring(0, fullTypeEnd).Split('-');
#if STORE_RESTRICTIVE
                var restrictionApplies = typeTags.Contains("all") || typeTags.Contains("ios");
#else
                var restrictionApplies = typeTags.Contains("all");
#endif
                if (restrictionApplies)
                {
                    return reason.Substring(fullTypeEnd + 1).Trim();
                }
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

            if (performer == null || title == null)
            {
                return audio.FileName;
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
            if (entry is NetworkStatisticsEntryCall call)
            {
                switch (call.NetworkType)
                {
                    case NetworkTypeMobile mobile:
                        return TdNetworkType.Mobile;
                    case NetworkTypeMobileRoaming mobileRoaming:
                        return TdNetworkType.MobileRoaming;
                    case NetworkTypeNone none:
                        return TdNetworkType.None;
                    case NetworkTypeOther other:
                    //return TdNetworkType.Other;
                    case NetworkTypeWiFi wifi:
                        return TdNetworkType.WiFi;
                }
            }
            else if (entry is NetworkStatisticsEntryFile file)
            {
                switch (file.NetworkType)
                {
                    case NetworkTypeMobile mobile:
                        return TdNetworkType.Mobile;
                    case NetworkTypeMobileRoaming mobileRoaming:
                        return TdNetworkType.MobileRoaming;
                    case NetworkTypeNone none:
                        return TdNetworkType.None;
                    case NetworkTypeOther other:
                    //return TdNetworkType.Other;
                    case NetworkTypeWiFi wifi:
                        return TdNetworkType.WiFi;
                }
            }

            return TdNetworkType.Other;
        }

        public static bool IsSaved(this Message message, int savedMessagesId)
        {
            if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
            {
                return message.ForwardInfo.FromChatId != 0;
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
            {
                return message.ChatId == savedMessagesId;
            }

            return false;
        }

        public static string GetFullName(this User user)
        {
            if (user.Type is UserTypeDeleted)
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

        public static bool UpdateFile(this Background background, File file)
        {
            var any = false;
            if (background.Document == null)
            {
                return false;
            }

            if (background.Document.Thumbnail != null && background.Document.Thumbnail.File.Id == file.Id)
            {
                background.Document.Thumbnail.File = file;
                any = true;
            }

            if (background.Document.DocumentValue.Id == file.Id)
            {
                background.Document.DocumentValue = file;
                any = true;
            }

            return any;
        }

        public static PhotoSize GetSmall(this Photo photo)
        {
            var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "t"));
            if (local != null)
            {
                return local;
            }

            return photo.Sizes.OrderBy(x => x.Width).FirstOrDefault();

            PhotoSize thumb = null;
            int thumbLevel = -1;

            foreach (var i in photo.Sizes)
            {
                var size = i.Type.Length > 0 ? i.Type[0] : 'z';
                int newThumbLevel = -1;

                switch (size)
                {
                    case 's': newThumbLevel = 0; break; // box 100x100
                    case 'm': newThumbLevel = 2; break; // box 320x320
                    case 'x': newThumbLevel = 5; break; // box 800x800
                    case 'y': newThumbLevel = 6; break; // box 1280x1280
                    case 'w': newThumbLevel = 8; break; // box 2560x2560
                    case 'a': newThumbLevel = 1; break; // crop 160x160
                    case 'b': newThumbLevel = 3; break; // crop 320x320
                    case 'c': newThumbLevel = 4; break; // crop 640x640
                    case 'd': newThumbLevel = 7; break; // crop 1280x1280
                }

                if (newThumbLevel < 0)
                {
                    continue;
                }
                if (thumbLevel < 0 || newThumbLevel < thumbLevel)
                {
                    thumbLevel = newThumbLevel;
                    thumb = i;
                }
            }

            return thumb;
        }

        public static PhotoSize GetBig(this Photo photo)
        {
            //var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "i"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            //return photo.Sizes.Where(x => !string.Equals(x.Type, "i")).OrderByDescending(x => x.Width).FirstOrDefault();

            PhotoSize full = null;
            int fullLevel = -1;

            foreach (var i in photo.Sizes)
            {
                var size = i.Type.Length > 0 ? i.Type[0] : 'z';
                int newFullLevel = -1;

                switch (size)
                {
                    case 's': newFullLevel = 5; break; // box 100x100
                    case 'm': newFullLevel = 4; break; // box 320x320
                    case 'x': newFullLevel = 2; break; // box 800x800
                    case 'y': newFullLevel = 1; break; // box 1280x1280
                    case 'w': newFullLevel = 3; break; // box 2560x2560
                    case 'a': newFullLevel = 9; break; // crop 160x160
                    case 'b': newFullLevel = 8; break; // crop 320x320
                    case 'c': newFullLevel = 7; break; // crop 640x640
                    case 'd': newFullLevel = 6; break; // crop 1280x1280
                    case 'i': newFullLevel = i.Photo.Local.IsDownloadingCompleted || i.Photo.Local.CanBeDownloaded ? 0 : 10; break;
                    case 'u': newFullLevel = 10; break;
                }

                if (newFullLevel < 0)
                {
                    continue;
                }
                if (fullLevel < 0 || newFullLevel < fullLevel)
                {
                    fullLevel = newFullLevel;
                    full = i;
                }
            }

            return full;
        }

        public static PhotoSize GetSmall(this ChatPhoto photo)
        {
            //var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "t"));
            //if (local != null && (local.Photo.Local.IsDownloadingCompleted || local.Photo.Local.CanBeDownloaded))
            //{
            //    return local;
            //}

            //return photo.Sizes.Where(x => !string.Equals(x.Type, "t")).OrderBy(x => x.Width).FirstOrDefault();

            PhotoSize thumb = null;
            int thumbLevel = -1;

            foreach (var i in photo.Sizes)
            {
                var size = i.Type.Length > 0 ? i.Type[0] : 'z';
                int newThumbLevel = -1;

                switch (size)
                {
                    case 's': newThumbLevel = 1; break; // box 100x100
                    case 'm': newThumbLevel = 3; break; // box 320x320
                    case 'x': newThumbLevel = 6; break; // box 800x800
                    case 'y': newThumbLevel = 7; break; // box 1280x1280
                    case 'w': newThumbLevel = 9; break; // box 2560x2560
                    case 'a': newThumbLevel = 2; break; // crop 160x160
                    case 'b': newThumbLevel = 4; break; // crop 320x320
                    case 'c': newThumbLevel = 5; break; // crop 640x640
                    case 'd': newThumbLevel = 8; break; // crop 1280x1280
                    case 't': newThumbLevel = i.Photo.Local.IsDownloadingCompleted || i.Photo.Local.CanBeDownloaded ? 0 : 10; break;
                }

                if (newThumbLevel < 0)
                {
                    continue;
                }
                if (thumbLevel < 0 || newThumbLevel < thumbLevel)
                {
                    thumbLevel = newThumbLevel;
                    thumb = i;
                }
            }

            return thumb;
        }

        public static PhotoSize GetBig(this ChatPhoto photo)
        {
            var local = photo.Sizes.FirstOrDefault(x => string.Equals(x.Type, "i"));
            if (local != null)
            {
                return local;
            }

            return photo.Sizes.OrderByDescending(x => x.Width).FirstOrDefault();

            PhotoSize full = null;
            int fullLevel = -1;

            foreach (var i in photo.Sizes)
            {
                var size = i.Type.Length > 0 ? i.Type[0] : 'z';
                int newFullLevel = -1;

                switch (size)
                {
                    case 's': newFullLevel = 4; break; // box 100x100
                    case 'm': newFullLevel = 3; break; // box 320x320
                    case 'x': newFullLevel = 1; break; // box 800x800
                    case 'y': newFullLevel = 0; break; // box 1280x1280
                    case 'w': newFullLevel = 2; break; // box 2560x2560
                    case 'a': newFullLevel = 8; break; // crop 160x160
                    case 'b': newFullLevel = 7; break; // crop 320x320
                    case 'c': newFullLevel = 6; break; // crop 640x640
                    case 'd': newFullLevel = 5; break; // crop 1280x1280
                }

                if (newFullLevel < 0)
                {
                    continue;
                }
                if (fullLevel < 0 || newFullLevel < fullLevel)
                {
                    fullLevel = newFullLevel;
                    full = i;
                }
            }

            return full;
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

            return supergroup.Status is ChatMemberStatusCreator ||
                supergroup.Status is ChatMemberStatusAdministrator ||
                supergroup.Status is ChatMemberStatusMember ||
                supergroup.Status is ChatMemberStatusRestricted;
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

        public static bool CanPinMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanPinMessages;
        }

        public static bool CanChangeInfo(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanChangeInfo;
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
                return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator || supergroup.Status is ChatMemberStatusMember;
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

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator || basicGroup.Status is ChatMemberStatusMember;
        }























        public static bool UpdateFile(this Thumbnail thumbnail, File file)
        {
            if (thumbnail.File.Id == file.Id)
            {
                thumbnail.File = file;
                return true;
            }

            return false;
        }

        public static bool UpdateFile(this PhotoSize size, File file)
        {
            if (size.Photo.Id == file.Id)
            {
                size.Photo = file;
                return true;
            }

            return false;
        }

        public static bool UpdateFile(this Chat chat, File file)
        {
            var any = false;
            if (chat.Photo != null)
            {
                if (chat.Photo.Small.Id == file.Id)
                {
                    chat.Photo.Small = file;
                    any = true;
                }

                if (chat.Photo.Big.Id == file.Id)
                {
                    chat.Photo.Big = file;
                    any = true;
                }
            }

            return any;
        }

        public static bool UpdateFile(this User user, File file)
        {
            var any = false;
            if (user.ProfilePhoto != null)
            {
                if (user.ProfilePhoto.Small.Id == file.Id)
                {
                    user.ProfilePhoto.Small = file;
                    any = true;
                }

                if (user.ProfilePhoto.Big.Id == file.Id)
                {
                    user.ProfilePhoto.Big = file;
                    any = true;
                }
            }

            return any;
        }

        public static bool UpdateFile(this Message message, File file)
        {
            switch (message.Content)
            {
                case MessageAlbum album:
                    return album.UpdateFile(file);
                case MessageAnimation animation:
                    return animation.UpdateFile(file);
                case MessageAudio audio:
                    return audio.UpdateFile(file);
                case MessageDocument document:
                    return document.UpdateFile(file);
                case MessageGame game:
                    return game.UpdateFile(file);
                case MessageInvoice invoice:
                    return invoice.UpdateFile(file);
                case MessagePhoto photo:
                    return photo.UpdateFile(file);
                case MessageSticker sticker:
                    return sticker.UpdateFile(file);
                case MessageText text:
                    return text.UpdateFile(file);
                case MessageVideo video:
                    return video.UpdateFile(file);
                case MessageVideoNote videoNote:
                    return videoNote.UpdateFile(file);
                case MessageVoiceNote voiceNote:
                    return voiceNote.UpdateFile(file);
                case MessageChatChangePhoto chatChangePhoto:
                    return chatChangePhoto.UpdateFile(file);
                default:
                    return false;
            }
        }

        public static bool UpdateFile(this InlineQueryResult result, File file)
        {
            switch (result)
            {
                case InlineQueryResultAnimation animation:
                    return animation.Animation.UpdateFile(file);
                case InlineQueryResultArticle article:
                    return article.Thumbnail?.UpdateFile(file) ?? false;
                case InlineQueryResultAudio audio:
                    return audio.Audio.UpdateFile(file);
                case InlineQueryResultContact contact:
                    return contact.Thumbnail?.UpdateFile(file) ?? false;
                case InlineQueryResultDocument document:
                    return document.Document.UpdateFile(file);
                case InlineQueryResultGame game:
                    return game.Game.UpdateFile(file);
                case InlineQueryResultLocation location:
                    return location.Thumbnail?.UpdateFile(file) ?? false;
                case InlineQueryResultPhoto photo:
                    return photo.Photo.UpdateFile(file);
                case InlineQueryResultSticker sticker:
                    return sticker.Sticker.UpdateFile(file);
                case InlineQueryResultVenue venue:
                    return venue.Thumbnail?.UpdateFile(file) ?? false;
                case InlineQueryResultVideo video:
                    return video.Video.UpdateFile(file);
                case InlineQueryResultVoiceNote voiceNote:
                    return voiceNote.VoiceNote.UpdateFile(file);
                default:
                    return false;
            }
        }



        public static bool UpdateFile(this MessageAnimation animation, File file)
        {
            return animation.Animation.UpdateFile(file);
        }

        public static bool UpdateFile(this Animation animation, File file)
        {
            var any = false;
            if (animation.Thumbnail != null && animation.Thumbnail.File.Id == file.Id)
            {
                animation.Thumbnail.File = file;
                any = true;
            }

            if (animation.AnimationValue.Id == file.Id)
            {
                animation.AnimationValue = file;
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageAudio audio, File file)
        {
            return audio.Audio.UpdateFile(file);
        }

        public static bool UpdateFile(this Audio audio, File file)
        {
            var any = false;
            if (audio.AlbumCoverThumbnail != null && audio.AlbumCoverThumbnail.File.Id == file.Id)
            {
                audio.AlbumCoverThumbnail.File = file;
                any = true;
            }

            if (audio.AudioValue.Id == file.Id)
            {
                audio.AudioValue = file;
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageDocument document, File file)
        {
            return document.Document.UpdateFile(file);
        }

        public static bool UpdateFile(this Document document, File file)
        {
            var any = false;
            if (document.Thumbnail != null && document.Thumbnail.File.Id == file.Id)
            {
                document.Thumbnail.File = file;
                any = true;
            }

            if (document.DocumentValue.Id == file.Id)
            {
                document.DocumentValue = file;
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageGame game, File file)
        {
            return game.Game.UpdateFile(file);
        }

        public static bool UpdateFile(this Game game, File file)
        {
            var any = false;
            if (game.Animation != null && game.Animation.UpdateFile(file))
            {
                any = true;
            }

            if (game.Photo != null && game.Photo.UpdateFile(file))
            {
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageInvoice invoice, File file)
        {
            if (invoice.Photo != null)
            {
                return invoice.Photo.UpdateFile(file);
            }

            return false;
        }



        public static bool UpdateFile(this MessageAlbum album, File file)
        {
            var any = false;
            foreach (var message in album.Messages)
            {
                if (message.UpdateFile(file))
                {
                    any = true;
                }
            }

            return any;
        }



        public static bool UpdateFile(this MessagePhoto photo, File file)
        {
            return photo.Photo.UpdateFile(file);
        }

        public static bool UpdateFile(this Photo photo, File file)
        {
            var any = false;
            foreach (var size in photo.Sizes)
            {
                if (size.Photo.Id == file.Id)
                {
                    size.Photo = file;
                    any = true;
                }
            }

            return any;
        }



        public static bool UpdateFile(this ChatPhotoInfo photo, File file)
        {
            var any = false;
            if (photo.Small.Id == file.Id)
            {
                photo.Small = file;
                any = true;
            }

            if (photo.Big.Id == file.Id)
            {
                photo.Big = file;
                any = true;
            }

            return any;
        }

        public static bool UpdateFile(this ChatPhoto photo, File file)
        {
            var any = false;
            foreach (var size in photo.Sizes)
            {
                if (size.Photo.Id == file.Id)
                {
                    size.Photo = file;
                    any = true;
                }
            }

            if (photo.Animation?.File.Id == file.Id)
            {
                photo.Animation.File = file;
                any = true;
            }

            return any;
        }




        public static bool UpdateFile(this MessageSticker sticker, File file)
        {
            return sticker.Sticker.UpdateFile(file);
        }

        public static bool UpdateFile(this Sticker sticker, File file)
        {
            if (sticker.Thumbnail != null && sticker.Thumbnail.File.Id == file.Id)
            {
                sticker.Thumbnail.File = file;
                return true;
            }
            if (sticker.StickerValue.Id == file.Id)
            {
                sticker.StickerValue = file;
                return true;
            }

            return false;
        }



        public static bool UpdateFile(this MessageText text, File file)
        {
            if (text.WebPage != null)
            {
                return text.WebPage.UpdateFile(file);
            }

            return false;
        }

        public static bool UpdateFile(this WebPage webPage, File file)
        {
            var any = false;
            if (webPage.Animation != null && webPage.Animation.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.Audio != null && webPage.Audio.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.Document != null && webPage.Document.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.Photo != null && webPage.Photo.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.Sticker != null && webPage.Sticker.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.Video != null && webPage.Video.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.VideoNote != null && webPage.VideoNote.UpdateFile(file))
            {
                any = true;
            }

            if (webPage.VoiceNote != null && webPage.VoiceNote.UpdateFile(file))
            {
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageVideo video, File file)
        {
            return video.Video.UpdateFile(file);
        }

        public static bool UpdateFile(this Video video, File file)
        {
            var any = false;
            if (video.Thumbnail != null && video.Thumbnail.File.Id == file.Id)
            {
                video.Thumbnail.File = file;
                any = true;
            }

            if (video.VideoValue.Id == file.Id)
            {
                video.VideoValue = file;
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageVideoNote videoNote, File file)
        {
            return videoNote.VideoNote.UpdateFile(file);
        }

        public static bool UpdateFile(this VideoNote videoNote, File file)
        {
            var any = false;
            if (videoNote.Thumbnail != null && videoNote.Thumbnail.File.Id == file.Id)
            {
                videoNote.Thumbnail.File = file;
                any = true;
            }

            if (videoNote.Video.Id == file.Id)
            {
                videoNote.Video = file;
                any = true;
            }

            return any;
        }



        public static bool UpdateFile(this MessageVoiceNote voiceNote, File file)
        {
            return voiceNote.VoiceNote.UpdateFile(file);
        }

        public static bool UpdateFile(this VoiceNote voiceNote, File file)
        {
            if (voiceNote.Voice.Id == file.Id)
            {
                voiceNote.Voice = file;
                return true;
            }

            return false;
        }



        public static bool UpdateFile(this MessageChatChangePhoto chatChangePhoto, File file)
        {
            return chatChangePhoto.Photo.UpdateFile(file);
        }



        public static void Update(this File file, File update)
        {
            file.ExpectedSize = update.ExpectedSize;
            file.Size = update.Size;
            file.Local = update.Local;
            file.Remote = update.Remote;
        }

        public static void Update(this LocalFile local, LocalFile update)
        {
            local.CanBeDeleted = update.CanBeDeleted;
            local.CanBeDownloaded = update.CanBeDownloaded;
            local.DownloadedPrefixSize = update.DownloadedPrefixSize;
            local.DownloadedSize = update.DownloadedSize;
            local.DownloadOffset = update.DownloadOffset;
            local.IsDownloadingActive = update.IsDownloadingActive;
            local.IsDownloadingCompleted = update.IsDownloadingCompleted;
            local.Path = update.Path;
        }

        public static void Update(this RemoteFile remote, RemoteFile update)
        {
            remote.Id = update.Id;
            remote.IsUploadingActive = update.IsUploadingActive;
            remote.IsUploadingCompleted = update.IsUploadingCompleted;
            remote.UniqueId = update.UniqueId;
            remote.UploadedSize = update.UploadedSize;
        }

        public static File GetLocalFile(string path)
        {
            return new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, path), false, false, false, true, 0, 0, 0), new RemoteFile(string.Empty, string.Empty, false, false, 0));
        }
    }

    public static class TdBackground
    {
        public static BackgroundType FromUri(Uri uri)
        {
            var slug = uri.Segments.Last();
            var query = uri.Query.ParseQueryString();

            var split = slug.Split('-');
            if (split.Length > 0 && split[0].Length == 6 && int.TryParse(split[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int topColor))
            {
                if (split.Length > 1 && split[1].Length == 6 && int.TryParse(split[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bottomColor))
                {
                    query.TryGetValue("rotation", out string rotationKey);
                    int.TryParse(rotationKey ?? string.Empty, out int rotation);

                    return new BackgroundTypeFill(new BackgroundFillGradient(topColor, bottomColor, rotation));
                }

                return new BackgroundTypeFill(new BackgroundFillSolid(topColor));
            }
            else
            {
                query.TryGetValue("mode", out string modeKey);
                query.TryGetValue("bg_color", out string bg_colorKey);

                var modeSplit = modeKey?.ToLower().Split('+') ?? new string[0];
                var bgSplit = bg_colorKey?.Split('-') ?? new string[0];

                if (bgSplit.Length > 0 && int.TryParse(bgSplit[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bgTopColor))
                {
                    BackgroundFill fill;
                    if (bgSplit.Length > 1 && int.TryParse(bgSplit[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bgBottomColor))
                    {
                        query.TryGetValue("rotation", out string rotationKey1);
                        int.TryParse(rotationKey1 ?? string.Empty, out int rotation1);

                        fill = new BackgroundFillGradient(bgTopColor, bgBottomColor, rotation1);
                    }
                    else
                    {
                        fill = new BackgroundFillSolid(bgTopColor);
                    }

                    query.TryGetValue("intensity", out string intensityKey);
                    int.TryParse(intensityKey, out int intensity);

                    return new BackgroundTypePattern(fill, intensity, modeSplit.Contains("motion"));
                }
                else
                {
                    return new BackgroundTypeWallpaper(modeSplit.Contains("blur"), modeSplit.Contains("motion"));
                }
            }
        }

        public static string ToString(Background background)
        {
            if (background.Type is BackgroundTypeFill typeFill)
            {
                if (typeFill.Fill is BackgroundFillSolid fillSolid)
                {
                    var color = fillSolid.Color.ToColor();
                    return string.Format("{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
                }
                else if (typeFill.Fill is BackgroundFillGradient fillGradient)
                {
                    var topColor = fillGradient.TopColor.ToColor();
                    var bottomColor = fillGradient.BottomColor.ToColor();

                    return string.Format("{0:X2}{1:X2}{2:X2}-{3:X2}{4:X2}{5:X2}?rotation={6}", topColor.R, topColor.G, topColor.B, bottomColor.R, bottomColor.G, bottomColor.B, fillGradient.RotationAngle);
                }
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                string builder = "?";
                if (typePattern.Fill is BackgroundFillSolid fillSolid)
                {
                    var color = fillSolid.Color.ToColor();
                    builder += string.Format("bg_color={0:X2}{1:X2}{2:X2}&", color.R, color.G, color.B);
                }
                else if (typePattern.Fill is BackgroundFillGradient fillGradient)
                {
                    var topColor = fillGradient.TopColor.ToColor();
                    var bottomColor = fillGradient.BottomColor.ToColor();

                    builder += string.Format("bg_color={0:X2}{1:X2}{2:X2}-{3:X2}{4:X2}{5:X2}&rotation={6}&", topColor.R, topColor.G, topColor.B, bottomColor.R, bottomColor.G, bottomColor.B, fillGradient.RotationAngle);
                }

                builder += $"intensity={typePattern.Intensity}&";

                if (typePattern.IsMoving)
                {
                    builder += "mode=motion";
                }

                return background.Name + builder.TrimEnd('&');
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                string builder = string.Empty;

                if (typeWallpaper.IsMoving)
                {
                    builder += "?mode=motion";
                }

                if (typeWallpaper.IsBlurred)
                {
                    if (builder.Length > 0)
                    {
                        builder += "+blur";
                    }
                    else
                    {
                        builder += "?mode=blur";
                    }
                }

                return background.Name + builder;
            }

            return null;
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
    public class MessageAlbum : MessageContent
    {
        public FormattedText Caption { get; set; }

        public UniqueList<long, MessageViewModel> Messages { get; } = new UniqueList<long, MessageViewModel>(x => x.Id);

        public MessageAlbum()
        {
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }

        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 320 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        private ((Rect, MosaicItemPosition)[], Size)? _positions;

        public void Invalidate()
        {
            _positions = null;
        }

        public (Rect[], Size) GetPositionsForWidth(double w)
        {
            var positions = _positions ??= MosaicAlbumLayout.chatMessageBubbleMosaicLayout(new Size(MAX_WIDTH, MAX_HEIGHT), GetSizes());

            var ratio = w / positions.Item2.Width;
            var rects = new Rect[positions.Item1.Length];

            for (int i = 0; i < rects.Length; i++)
            {
                var rect = positions.Item1[i].Item1;
                rects[i] = new Rect(rect.X * ratio, rect.Y * ratio, rect.Width * ratio, rect.Height * ratio);
            }

            return (rects, new Size(positions.Item2.Width * ratio, positions.Item2.Height * ratio));
        }

        private IEnumerable<Size> GetSizes()
        {
            foreach (var message in Messages)
            {
                if (message.Content is MessagePhoto photoMedia)
                {
                    yield return GetClosestPhotoSizeWithSize(photoMedia.Photo.Sizes, 1280, false);
                }
                else if (message.Content is MessageVideo videoMedia)
                {
                    if (videoMedia.Video.Width != 0 && videoMedia.Video.Height != 0)
                    {
                        yield return new Size(videoMedia.Video.Width, videoMedia.Video.Height);
                    }
                    else if (videoMedia.Video.Thumbnail != null)
                    {
                        yield return new Size(videoMedia.Video.Thumbnail.Width, videoMedia.Video.Thumbnail.Height);
                    }
                    else
                    {
                        yield return Size.Empty;
                    }
                }
            }
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side)
        {
            return GetClosestPhotoSizeWithSize(sizes, side, false);
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side, bool byMinSide)
        {
            if (sizes == null || sizes.IsEmpty())
            {
                return Size.Empty;
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
        public int UserId { get; set; }

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
            UserId = chatEvent.UserId;
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
