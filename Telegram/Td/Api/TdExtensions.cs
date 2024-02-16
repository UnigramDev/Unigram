//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Native;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Telegram.Td.Api
{
    public static class TdExtensions
    {
        public static int CountUnread(this ChatActiveStories activeStories, out bool closeFriends)
        {
            var count = 0;
            closeFriends = false;

            foreach (var story in activeStories.Stories)
            {
                if (story.StoryId > activeStories.MaxReadStoryId)
                {
                    if (story.IsForCloseFriends)
                    {
                        closeFriends = true;
                    }

                    count++;
                }
            }

            return count;
        }

        public static int TotalReactions(this MessageInteractionInfo info)
        {
            if (info?.Reactions != null)
            {
                return info.Reactions.Reactions.Sum(x => x.TotalCount);
            }

            return 0;
        }

        public static bool AreTheSame(this FormattedText x, FormattedText y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            return string.Equals(x.ToString(), y.ToString());
        }

        public static long UserId(this ChatBoost boost)
        {
            return boost.Source switch
            {
                ChatBoostSourceGiftCode giftCode => giftCode.UserId,
                ChatBoostSourceGiveaway giveaway => giveaway.UserId,
                ChatBoostSourcePremium premium => premium.UserId,
                _ => 0
            };
        }

        public static Vector2 ToVector2(this Point point)
        {
            return new Vector2((float)point.X, (float)point.Y);
        }

        public static Vector2 ToVector2(this Point point, float scale)
        {
            return new Vector2((float)point.X * scale, (float)point.Y * scale);
        }

        public static ChatNotificationSettings Clone(this ChatNotificationSettings settings)
        {
            return new ChatNotificationSettings(
                settings.UseDefaultMuteFor, settings.MuteFor,
                settings.UseDefaultSound, settings.SoundId,
                settings.UseDefaultShowPreview, settings.ShowPreview,
                settings.UseDefaultMuteStories, settings.MuteStories,
                settings.UseDefaultStorySound, settings.StorySoundId,
                settings.UseDefaultShowStorySender, settings.ShowStorySender,
                settings.UseDefaultDisablePinnedMessageNotifications, settings.DisablePinnedMessageNotifications,
                settings.UseDefaultDisableMentionNotifications, settings.DisableMentionNotifications);
        }

        public static bool HasActiveUsername(this Supergroup supergroup)
        {
            return ActiveUsername(supergroup).Length > 0;
        }

        public static bool HasActiveUsername(this Supergroup supergroup, out string username)
        {
            username = ActiveUsername(supergroup);
            return username.Length > 0;
        }

        public static string ActiveUsername(this Supergroup supergroup)
        {
            return supergroup?.Usernames?.ActiveUsernames.FirstOrDefault() ?? string.Empty;
        }

        public static string ActiveUsername(this Supergroup supergroup, string value)
        {
            return supergroup?.Usernames?.ActiveUsernames.FirstOrDefault(x => x.StartsWith(value)) ?? ActiveUsername(supergroup);
        }

        public static bool HasEditableUsername(this Supergroup supergroup)
        {
            return EditableUsername(supergroup).Length > 0;
        }

        public static string EditableUsername(this Supergroup supergroup)
        {
            return supergroup?.Usernames?.EditableUsername ?? string.Empty;
        }

        public static bool HasActiveUsername(this User user)
        {
            return ActiveUsername(user).Length > 0;
        }

        public static bool HasActiveUsername(this User user, out string username)
        {
            username = ActiveUsername(user);
            return username.Length > 0;
        }

        public static bool HasActiveUsername(this User user, string value, out string username)
        {
            username = user?.Usernames?.ActiveUsernames.FirstOrDefault(x => x.StartsWith(value, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            return username.Length > 0;
        }

        public static string ActiveUsername(this User user)
        {
            return user?.Usernames?.ActiveUsernames.FirstOrDefault() ?? string.Empty;
        }

        public static string ActiveUsername(this User user, string value)
        {
            return user?.Usernames?.ActiveUsernames.FirstOrDefault(x => x.StartsWith(value, StringComparison.OrdinalIgnoreCase)) ?? ActiveUsername(user);
        }

        public static string EditableUsername(this User user)
        {
            return user?.Usernames?.EditableUsername ?? string.Empty;
        }

        public static bool HasEditableUsername(this User user)
        {
            return EditableUsername(user).Length > 0;
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

        public static bool Contains(this ChatAvailableReactions reactions, string value)
        {
            if (reactions is ChatAvailableReactionsSome some)
            {
                return some.Reactions.OfType<ReactionTypeEmoji>().Any(x => x.Emoji == value);
            }

            return true;
        }

        public static bool AreTheSame(this ReactionType x, ReactionType y)
        {
            if (x is ReactionTypeEmoji oldEmoji
                && y is ReactionTypeEmoji newEmoji)
            {
                return oldEmoji.Emoji == newEmoji.Emoji;
            }
            else if (x is ReactionTypeCustomEmoji oldCustomEmoji
                && y is ReactionTypeCustomEmoji newCustomEmoji)
            {
                return oldCustomEmoji.CustomEmojiId == newCustomEmoji.CustomEmojiId;
            }

            return false;
        }

        public static bool AreTheSame(this MessageSelfDestructType x, MessageSelfDestructType y)
        {
            if (x is MessageSelfDestructTypeTimer oldTimer
                && y is MessageSelfDestructTypeTimer newTimer)
            {
                return oldTimer.SelfDestructTime == newTimer.SelfDestructTime;
            }
            else if (x is MessageSelfDestructTypeImmediately
                && y is MessageSelfDestructTypeImmediately)
            {
                return false;
            }

            return x == null && y == null;
        }

        public static bool AreTheSame(this ChatAvailableReactions x, ChatAvailableReactions y)
        {
            if (x is ChatAvailableReactionsAll && y is ChatAvailableReactionsAll)
            {
                return true;
            }
            else if (x is ChatAvailableReactionsSome xSome && y is ChatAvailableReactionsSome ySome)
            {
                if (xSome.Reactions?.Count != ySome.Reactions?.Count)
                {
                    return false;
                }

                HashSet<string> emojiHash = null;
                HashSet<long> customHash = null;

                for (int i = 0; i < xSome.Reactions.Count; i++)
                {
                    if (xSome.Reactions[i] is ReactionTypeEmoji emoji)
                    {
                        emojiHash ??= new HashSet<string>();
                        emojiHash.Add(emoji.Emoji);
                    }
                    else if (xSome.Reactions[i] is ReactionTypeCustomEmoji custom)
                    {
                        customHash ??= new HashSet<long>();
                        customHash.Add(custom.CustomEmojiId);
                    }
                }

                for (int i = 0; i < ySome.Reactions.Count; i++)
                {
                    if (ySome.Reactions[i] is ReactionTypeEmoji emoji && (emojiHash == null || !emojiHash.Contains(emoji.Emoji)))
                    {
                        return false;
                    }
                    else if (ySome.Reactions[i] is ReactionTypeCustomEmoji custom && (customHash == null || !customHash.Contains(custom.CustomEmojiId)))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static bool IsUser(this Chat chat, out long userId)
        {
            if (chat.Type is ChatTypePrivate privata)
            {
                userId = privata.UserId;
                return true;
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                userId = secret.UserId;
                return true;
            }

            userId = 0;
            return false;
        }

        public static bool IsBasicGroup(this Chat chat, out long basicGroupId)
        {
            if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                basicGroupId = basicGroup.BasicGroupId;
                return true;
            }

            basicGroupId = 0;
            return false;
        }

        public static bool IsSupergroup(this Chat chat, out long supergroupId, out bool isChannel)
        {
            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                supergroupId = supergroup.SupergroupId;
                isChannel = supergroup.IsChannel;
                return true;
            }

            supergroupId = 0;
            isChannel = false;
            return false;
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

        public static JsonValueArray GetNamedArray(this JsonValueObject json, string key)
        {
            var member = json.GetNamedValue(key);
            if (member?.Value is JsonValueArray value)
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
                return missed ? (outgoing ? Strings.CallMessageVideoOutgoingMissed : Strings.CallMessageVideoIncomingMissed) : (outgoing ? Strings.CallMessageVideoOutgoing : Strings.CallMessageVideoIncoming);
            }
            else
            {
                return missed ? (outgoing ? Strings.CallMessageOutgoingMissed : Strings.CallMessageIncomingMissed) : (outgoing ? Strings.CallMessageOutgoing : Strings.CallMessageIncoming);
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

        public static Brush ToBrush(this BackgroundTypeFill fill, int offset = 0)
        {
            return fill.Fill.ToBrush(offset);
        }

        public static Brush ToBrush(this BackgroundTypePattern pattern, int offset = 0)
        {
            return pattern.Fill.ToBrush(offset);
        }

        public static Brush ToBrush(this BackgroundFill fill, int offset = 0)
        {
            if (fill is BackgroundFillSolid solid)
            {
                return new SolidColorBrush(solid.Color.ToColor());
            }
            else if (fill is BackgroundFillGradient gradient)
            {
                return TdBackground.GetGradient(gradient.TopColor, gradient.BottomColor, gradient.RotationAngle);
            }
            else if (fill is BackgroundFillFreeformGradient freeformGradient)
            {
                // PERF: ChatBackgroundFreeform.Create is not the fastest,
                // would be better to execute it asynchronously.
                return new ImageBrush
                {
                    ImageSource = ChatBackgroundFreeform.Create(freeformGradient, offset),
                    Stretch = Stretch.UniformToFill
                };
            }

            return null;
        }

        public static ICanvasBrush ToCanvasBrush(this BackgroundTypeFill fill, ICanvasResourceCreator sender, uint width, uint height)
        {
            return fill.Fill.ToCanvasBrush(sender, width, height);
        }

        public static ICanvasBrush ToCanvasBrush(this BackgroundTypePattern pattern, ICanvasResourceCreator sender, uint width, uint height)
        {
            return pattern.Fill.ToCanvasBrush(sender, width, height);
        }

        public static ICanvasBrush ToCanvasBrush(this BackgroundFill fill, ICanvasResourceCreator sender, uint width, uint height)
        {
            if (fill is BackgroundFillSolid solid)
            {
                return new CanvasSolidColorBrush(sender, solid.Color.ToColor());
            }
            else if (fill is BackgroundFillGradient gradient)
            {
                return TdBackground.GetGradient(sender, gradient.TopColor, gradient.BottomColor, gradient.RotationAngle, width, height);
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

        public static bool AreTheSame(this ChatList x, ChatList y, bool allowNull = true)
        {
            if ((x is ChatListMain || x == null) && (y is ChatListMain || (y == null && allowNull)))
            {
                return true;
            }
            if (x is ChatListArchive && y is ChatListArchive)
            {
                return true;
            }
            else if (x is ChatListFolder folderX && y is ChatListFolder folderY)
            {
                return folderX.ChatFolderId == folderY.ChatFolderId;
            }

            return false;
        }

        public static bool AreTheSame(this StoryList x, StoryList y)
        {
            if (x is StoryListMain)
            {
                return y is StoryListMain;
            }
            else if (x is StoryListArchive)
            {
                return y is StoryListArchive;
            }

            return false;
        }

        public static bool CodeEquals(this Error error, ErrorCode code)
        {
            if (error == null)
            {
                return false;
            }

            if (Enum.IsDefined(typeof(ErrorCode), error.Code))
            {
                return (ErrorCode)error.Code == code;
            }

            return false;
        }

        public static bool MessageEquals(this Error error, ErrorType type)
        {
            if (error == null || error.Message == null)
            {
                return false;
            }

            var strings = error.Message.Split(':');
            var typeString = strings[0];
            if (Enum.IsDefined(typeof(ErrorType), typeString))
            {
                var value = (ErrorType)Enum.Parse(typeof(ErrorType), typeString, true);

                return value == type;
            }

            return false;
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

        public static Thumbnail ToThumbnail(this PhotoSize photo)
        {
            if (photo == null)
            {
                return null;
            }

            return new Thumbnail(new ThumbnailFormatJpeg(), photo.Width, photo.Height, photo.Photo);
        }

        public static InputThumbnail ToInput(this Thumbnail thumbnail)
        {
            if (thumbnail == null)
            {
                return null;
            }

            return new InputThumbnail(new InputFileId(thumbnail.File.Id), thumbnail.Width, thumbnail.Height);
        }

        public static bool AreTheSame(this Message x, Message y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static bool AreTheSame(this Message x, MessageWithOwner y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static bool AreTheSame(this MessageWithOwner x, Message y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ChatId == y.ChatId;
        }

        public static bool AreTheSame(this MessageWithOwner x, MessageWithOwner y)
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
            return Substring(text, (int)startIndex, (int)length);
        }

        public static FormattedText Substring(this FormattedText text, int startIndex, int length)
        {
            if (text.Text.Length < length)
            {
                return text;
            }

            var message = text.Text.Substring(startIndex, Math.Min(text.Text.Length - startIndex, length));
            IList<TextEntity> sub = null;

            foreach (var entity in text.Entities)
            {
                if (TextStyleRun.GetRelativeRange(entity.Offset, entity.Length, startIndex, length, out int newOffset, out int newLength))
                {
                    sub ??= new List<TextEntity>();
                    sub.Add(new TextEntity
                    {
                        Offset = newOffset,
                        Length = newLength,
                        Type = entity.Type
                    });
                }
            }

            return new FormattedText(message, sub ?? Array.Empty<TextEntity>());
        }

        public static FormattedText ToFormattedText(this PageBlockCaption caption)
        {
            return caption.Text.ToFormattedText();
        }

        public static FormattedText ToFormattedText(this RichText text)
        {
            return new FormattedText(text.ToPlainText(), Array.Empty<TextEntity>());
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
                case TextEntityTypeStrikethrough:
                case TextEntityTypeUnderline:
                case TextEntityTypeSpoiler:
                case TextEntityTypeBlockQuote:
                case TextEntityTypeCustomEmoji:
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

        public static (File File, Thumbnail Thumbnail, string FileName) GetFileAndThumbnailAndName(this MessageWithOwner message)
        {
            return GetFileAndThumbnailAndName(message.Content);
        }

        public static (File File, Thumbnail Thumbnail, string FileName) GetFileAndThumbnailAndName(this MessageContent content)
        {
            switch (content)
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

        public static Thumbnail GetThumbnail(this WebPage webPage)
        {
            if (webPage.Animation != null)
            {
                return webPage.Animation.Thumbnail;
            }
            else if (webPage.Audio != null)
            {
                return webPage.Audio.AlbumCoverThumbnail;
            }
            else if (webPage.Document != null)
            {
                return webPage.Document.Thumbnail;
            }
            else if (webPage.Sticker != null)
            {
                return webPage.Sticker.Thumbnail;
            }
            else if (webPage.Video != null)
            {
                return webPage.Video.Thumbnail;
            }
            else if (webPage.VideoNote != null)
            {
                return webPage.VideoNote.Thumbnail;
            }
            else if (webPage.VoiceNote != null)
            {
                return null;
            }
            else if (webPage.Photo != null)
            {
                var small = webPage.Photo.GetSmall();
                if (small != null)
                {
                    return small.ToThumbnail();
                }
            }

            return null;
        }

        public static File GetFile(this MessageWithOwner message)
        {
            if (message is MessageViewModel viewModel)
            {
                return GetFile(viewModel.GeneratedContent ?? message.Content);
            }

            return GetFile(message.Content);
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
                case MessageInvoice invoice:
                    return invoice.ExtendedMedia switch
                    {
                        MessageExtendedMediaPhoto photo => photo.Photo.GetBig()?.Photo,
                        MessageExtendedMediaVideo video => video.Video.VideoValue,
                        _ => invoice.Photo?.GetBig()?.Photo
                    };
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
                    return sticker.Sticker.Format is StickerFormatTgs or StickerFormatWebm && sticker.Sticker.StickerValue.Local.IsDownloadingCompleted;
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
                        return text.WebPage.Sticker.Format is StickerFormatTgs or StickerFormatWebm && text.WebPage.Sticker.StickerValue.Local.IsDownloadingCompleted;
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
                case MessageInvoice invoice:
                    return invoice.ExtendedMedia is MessageExtendedMediaVideo;
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

        public static FormattedText GetCaption(this MessageWithOwner message)
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
                MessageAnimatedEmoji animatedEmoji => animatedEmoji.AnimatedEmoji.Sticker?.FullType switch
                {
                    StickerFullTypeCustomEmoji customEmoji => new FormattedText(animatedEmoji.Emoji, new[]
                    {
                        new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId))
                    }),
                    _ => new FormattedText(animatedEmoji.Emoji, Array.Empty<TextEntity>())
                },
                MessageInvoice invoice => invoice.ExtendedMedia switch
                {
                    MessageExtendedMediaPreview preview => preview.Caption,
                    MessageExtendedMediaPhoto photo => photo.Caption,
                    MessageExtendedMediaVideo video => video.Caption,
                    MessageExtendedMediaUnsupported unsupported => unsupported.Caption,
                    _ => null,
                },
                _ => null,
            };
        }

        public static FormattedText ReplaceSpoilers(this FormattedText text, bool singleLine = true)
        {
            if (text.Entities?.Count > 0)
            {
                StringBuilder rep = null;
                List<TextEntity> ent = null;

                var chars = "⠁⠂⠄⠈⠐⠠⡀⢀⠃⠅⠆⠉⠊⠌⠑⠒⠔⠘⠡⠢⠤⠨⠰⡁⡂⡄⡈⡐⡠⢁⢂⢄⢈⢐⢠⣀⠇⠋⠍⠎⠓⠕⠖⠙⠚⠜⠣⠥⠦⠩⠪⠬⠱⠲⠴⠸⡃⡅⡆⡉⡊⡌⡑⡒⡔⡘⡡⡢⡤⡨⡰⢃⢅⢆⢉⢊⢌⢑⢒⢔⢘⢡⢢⢤⢨⢰⣁⣂⣄⣈⣐⣠⠏⠗⠛⠝⠞⠧⠫⠭⠮⠳⠵⠶⠹⠺⠼⡇⡋⡍⡎⡓⡕⡖⡙⡚⡜⡣⡥⡦⡩⡪⡬⡱⡲⡴⡸⢇⢋⢍⢎⢓⢕⢖⢙⢚⢜⢣⢥⢦⢩⢪⢬⢱⢲⢴⢸⣃⣅⣆⣉⣊⣌⣑⣒⣔⣘⣡⣢⣤⣨⣰⠟⠯⠷⠻⠽⠾⡏⡗⡛⡝⡞⡧⡫⡭⡮⡳⡵⡶⡹⡺⡼⢏⢗⢛⢝⢞⢧⢫⢭⢮⢳⢵⢶⢹⢺⢼⣇⣋⣍⣎⣓⣕⣖⣙⣚⣜⣣⣥⣦⣩⣪⣬⣱⣲⣴⣸⠿⡟⡯⡷⡻⡽⡾⢟⢯⢷⢻⢽⢾⣏⣗⣛⣝⣞⣧⣫⣭⣮⣳⣵⣶⣹⣺⣼⡿⢿⣟⣯⣷⣻⣽⣾⣿";

                foreach (var entity in text.Entities)
                {
                    if (entity.Type is TextEntityTypeSpoiler)
                    {
                        rep ??= new StringBuilder(text.Text);
                        ent ??= text.Entities.ToList();

                        for (int i = 0; i < entity.Length; i++)
                        {
                            rep[entity.Offset + i] = chars[text.Text[entity.Offset + i] % chars.Length];
                        }

                        ent.RemoveAll(x => x.Offset <= entity.Offset + entity.Length && entity.Offset <= x.Offset + x.Length);
                    }
                }

                if (rep != null && ent != null)
                {
                    if (singleLine)
                    {
                        return new FormattedText(rep.Replace('\n', ' ').ToString(), ent);
                    }

                    return new FormattedText(rep.ToString(), ent);
                }
            }

            if (singleLine)
            {
                return new FormattedText(text.Text.Replace('\n', ' '), text.Entities);
            }

            return text;
        }

        public static bool HasCaption(this MessageContent content)
        {
            var caption = content.GetCaption();
            return caption != null && !string.IsNullOrEmpty(caption.Text);
        }

        public static Photo ToPhoto(this ChatPhotoInfo chatPhoto)
        {
            return new Photo(false, null, new PhotoSize[] { new PhotoSize("t", chatPhoto.Small, 160, 160, Array.Empty<int>()), new PhotoSize("i", chatPhoto.Big, 640, 640, Array.Empty<int>()) });
        }

        public static Photo ToPhoto(this ChatPhoto chatPhoto)
        {
            return new Photo(false, chatPhoto.Minithumbnail, chatPhoto.Sizes);
        }

        public static bool IsInstantGallery(this WebPage webPage)
        {
            return webPage.InstantViewVersion != 0 &&
                (string.Equals(webPage.SiteName, "twitter", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(webPage.SiteName, "instagram", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(webPage.Type, "telegram_album", StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasText(this WebPage webPage)
        {
            if (!string.IsNullOrWhiteSpace(webPage.SiteName))
            {
                return true;

            }

            if (!string.IsNullOrWhiteSpace(webPage.Title))
            {
                return true;
            }
            else if (!string.IsNullOrWhiteSpace(webPage.Author))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(webPage.Description?.Text))
            {
                return true;
            }

            return false;
        }

        public static bool HasMedia(this WebPage webPage)
        {
            if (string.Equals(webPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return webPage.Animation != null || webPage.Audio != null || webPage.Document != null || webPage.Sticker != null || webPage.Video != null || webPage.VideoNote != null || webPage.VoiceNote != null || webPage.Photo != null;
            return webPage.Animation != null || webPage.Audio != null || webPage.Document != null || webPage.Sticker != null || webPage.Video != null || webPage.VideoNote != null || webPage.VoiceNote != null || webPage.HasPhoto();
        }

        public static bool HasPhoto(this WebPage webPage)
        {
            return webPage.Photo != null && webPage.ShowLargeMedia;

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

        public static bool CanBeSmall(this WebPage webPage)
        {
            if (webPage.Audio != null || webPage.Document != null || webPage.VoiceNote != null)
            {
                return false;
            }

            return webPage.SiteName.Length > 0 || webPage.Title.Length > 0 || webPage.Author.Length > 0 || webPage.Description?.Text.Length > 0;

            if (webPage.Photo != null && (webPage.SiteName.Length > 0 || webPage.Title.Length > 0 || webPage.Author.Length > 0 || webPage.Description?.Text.Length > 0))
            {
                return !webPage.HasMedia();
            }

            return false;
        }

        public static bool IsService(this Message message)
        {
            switch (message.Content)
            {
                case MessageAlbum:
                case MessageAnimatedEmoji:
                case MessageAnimation:
                case MessageAudio:
                case MessageBigEmoji:
                case MessageCall:
                case MessageContact:
                case MessageDice:
                case MessageDocument:
                case MessageGame:
                case MessageInvoice:
                case MessageLocation:
                case MessagePhoto:
                case MessagePoll:
                case MessageSticker:
                case MessageText:
                case MessageUnsupported:
                case MessageVenue:
                case MessageVideo:
                case MessageVideoNote:
                case MessageVoiceNote:
                case MessagePremiumGiveaway:
                    return false;
                case MessageAsyncStory asyncStory:
                    return asyncStory.ViaMention;
                case MessageStory story:
                    return story.ViaMention;
                default:
                    return true;
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
                StickerFormat format = stickerSet.Thumbnail.Format switch
                {
                    ThumbnailFormatWebp => new StickerFormatWebp(),
                    ThumbnailFormatWebm => new StickerFormatWebm(),
                    ThumbnailFormatTgs => new StickerFormatTgs(),
                    _ => default
                };

                StickerFullType fullType = stickerSet.NeedsRepainting
                    ? new StickerFullTypeCustomEmoji(0, true)
                    : new StickerFullTypeRegular();

                if (stickerSet.Thumbnail.Format is ThumbnailFormatTgs)
                {
                    return new Sticker(stickerSet.Id, stickerSet.Id, 512, 512, "\U0001F4A9", format, fullType, stickerSet.ThumbnailOutline, stickerSet.Thumbnail, stickerSet.Thumbnail.File);
                }

                return new Sticker(stickerSet.Id, stickerSet.Id, stickerSet.Thumbnail.Width, stickerSet.Thumbnail.Height, "\U0001F4A9", format, fullType, stickerSet.ThumbnailOutline, stickerSet.Thumbnail, stickerSet.Thumbnail.File);
            }

            if (stickerSet.Covers?.Count > 0)
            {
                return stickerSet.Covers[0];
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

            if (string.IsNullOrEmpty(audio.Performer)
                || string.IsNullOrEmpty(audio.Title))
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
                if (chat.Positions[i].List.AreTheSame(chatList))
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
                if (chat.Positions[i].List.AreTheSame(chatList))
                {
                    Monitor.Exit(chat);
                    return chat.Positions[i].Order;
                }
            }

            Monitor.Exit(chat);
            return 0;
        }

        public static long GetOrder(this IList<ChatPosition> positions, ChatList chatList)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].List.AreTheSame(chatList))
                {
                    return positions[i].Order;
                }
            }

            return 0;
        }

        public static bool AreTheSame(this MessageSender sender, MessageSender compare)
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

        public static bool AreTheSame(this GroupCallParticipant sender, GroupCallParticipant compare)
        {
            if (sender.IsCurrentUser && compare.IsCurrentUser)
            {
                return true;
            }

            return sender.ParticipantId.AreTheSame(compare.ParticipantId);
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
            if (message.ForwardInfo?.Origin is MessageOriginUser)
            {
                return message.ForwardInfo.Source != null;
            }
            else if (message.ForwardInfo?.Origin is MessageOriginChat)
            {
                return message.ForwardInfo.Source != null;
            }
            else if (message.ForwardInfo?.Origin is MessageOriginChannel originChannel)
            {
                // TODO: not fully correct
                if (message.ChatId == savedMessagesId)
                {
                    return message.ForwardInfo.Source != null;
                }

                return message.ForwardInfo.Source != null
                    && message.ForwardInfo.Source.ChatId == originChannel.ChatId
                    && message.ForwardInfo.Source.MessageId == originChannel.MessageId;
            }
            else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser)
            {
                return message.ChatId == savedMessagesId;
            }
            else if (message.ImportInfo != null)
            {
                return true;
            }

            return false;
        }

        // TODO: not fully correct
        public static bool HasSameOrigin(this MessageForwardInfo forwardInfo)
        {
            if (forwardInfo == null)
            {
                return true;
            }

            if (forwardInfo?.Origin is MessageOriginChannel originChannel)
            {
                return forwardInfo.Source != null
                    && forwardInfo.Source.ChatId == originChannel.ChatId
                    && forwardInfo.Source.MessageId == originChannel.MessageId;
            }

            return true;
        }

        public static string FullName(this User user)
        {
            if (user == null || user.Type is UserTypeDeleted)
            {
                return Strings.HiddenName;
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

        public static bool Empty(this ChatFolder folder)
        {
            return folder.IncludedChatIds.Empty() &&
                !folder.IncludeBots &&
                !folder.IncludeGroups &&
                !folder.IncludeContacts &&
                !folder.IncludeNonContacts &&
                !folder.IncludeChannels;
        }

        public static bool Any(this ChatFolder folder)
        {
            return folder.IncludedChatIds.Any() ||
                folder.IncludeBots ||
                folder.IncludeGroups ||
                folder.IncludeContacts ||
                folder.IncludeNonContacts ||
                folder.IncludeChannels;
        }

        public static StickerSetInfo ToInfo(this StickerSet set)
        {
            return new StickerSetInfo(set.Id, set.Title, set.Name, set.Thumbnail, set.ThumbnailOutline, set.IsInstalled, set.IsArchived, set.IsOfficial, set.StickerFormat, set.StickerType, set.NeedsRepainting, set.IsAllowedAsChatEmojiStatus, set.IsViewed, set.Stickers.Count, set.Stickers);
        }

        public static string GetStartsAt(this MessageVideoChatScheduled messageVideoChatScheduled)
        {
            var date = Converters.Formatter.ToLocalTime(messageVideoChatScheduled.StartDate);
            return string.Format(Strings.formatDateAtTime, Converters.Formatter.ShortDate.Format(date), Converters.Formatter.ShortTime.Format(date));
        }

        public static string GetStartsAt(this GroupCall groupCall)
        {
            var date = Converters.Formatter.ToLocalTime(groupCall.ScheduledStartDate);
            if (date.Date == DateTime.Today)
            {
                return string.Format(Strings.TodayAtFormattedWithToday, Converters.Formatter.ShortTime.Format(date));
            }
            else if (date.Date.AddDays(1) == DateTime.Today)
            {
                return string.Format(Strings.YesterdayAtFormatted, Converters.Formatter.ShortTime.Format(date));
            }

            return string.Format(Strings.formatDateAtTime, Converters.Formatter.ShortDate.Format(date), Converters.Formatter.ShortTime.Format(date));
        }

        public static void Discern(this IEnumerable<ReactionType> reactions, out HashSet<string> emoji, out HashSet<long> customEmoji)
        {
            emoji = null;
            customEmoji = null;

            foreach (var reaction in reactions)
            {
                if (reaction is ReactionTypeEmoji emojiItem)
                {
                    emoji ??= new HashSet<string>();
                    emoji.Add(emojiItem.Emoji);
                }
                else if (reaction is ReactionTypeCustomEmoji customEmojiItem)
                {
                    customEmoji ??= new HashSet<long>();
                    customEmoji.Add(customEmojiItem.CustomEmojiId);
                }
            }
        }

        public static bool IsChosen(this MessageReactions reactions, ReactionType type)
        {
            if (reactions == null)
            {
                return false;
            }

            return reactions.Reactions.Where(x => x.IsChosen)
                .Select(x => x.Type)
                .Any(x => x.AreTheSame(type));
        }

        public static string GetStartsIn(this GroupCall groupCall)
        {
            var date = Converters.Formatter.ToLocalTime(groupCall.ScheduledStartDate);
            var duration = date - DateTime.Now;

            if (Math.Abs(duration.TotalDays) >= 7)
            {
                return Locale.Declension(Strings.R.Weeks, 1);
            }
            if (Math.Abs(duration.TotalDays) >= 1)
            {
                return Locale.Declension(Strings.R.Days, (int)duration.TotalDays);
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
                return Locale.Declension(Strings.R.Days, (int)duration.TotalDays);
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

        public static string GetDuration(this MessageExtendedMediaPreview preview)
        {
            var duration = TimeSpan.FromSeconds(preview.Duration);
            if (duration.TotalHours >= 1)
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

        public static string GetDuration(this StoryVideo video)
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
            if (permissions.CanSendVoiceNotes)
            {
                count++;
            }
            if (permissions.CanSendVideoNotes)
            {
                count++;
            }
            if (permissions.CanSendVideos)
            {
                count++;
            }
            if (permissions.CanSendPhotos)
            {
                count++;
            }
            if (permissions.CanSendDocuments)
            {
                count++;
            }
            if (permissions.CanSendAudios)
            {
                count++;
            }
            if (permissions.CanSendBasicMessages)
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
            return 13;
        }

        public static bool CanPinMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPinMessages;
        }

        public static bool CanDeleteMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanDeleteMessages;
        }

        public static bool CanPinMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPinMessages;
        }

        public static bool CanDeleteMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanDeleteMessages;
        }

        public static bool CanChangeInfo(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanChangeInfo;
        }

        public static bool CanChangeInfo(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanChangeInfo;
        }

        public static bool CanManageVideoChats(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanManageVideoChats;
        }

        public static bool CanManageVideoChats(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanManageVideoChats;
        }

        public static bool CanPostMessages(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel)
            {
                return supergroup.Status is ChatMemberStatusCreator
                    || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPostMessages;
            }
            else
            {
                return supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator or ChatMemberStatusMember
                    || supergroup.Status is ChatMemberStatusRestricted restricted && restricted.Permissions.CanSendBasicMessages;
            }
        }


        public static bool CanPostStories(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel)
            {
                return supergroup.Status is ChatMemberStatusCreator
                    || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPostStories;
            }

            return false;
        }

        public static bool CanEditStories(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel)
            {
                return supergroup.Status is ChatMemberStatusCreator
                    || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanEditStories;
            }

            return false;
        }

        public static bool CanDeleteStories(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            if (supergroup.IsChannel)
            {
                return supergroup.Status is ChatMemberStatusCreator
                    || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanDeleteStories;
            }

            return false;
        }

        public static bool CanRestrictMembers(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanRestrictMembers;
        }

        public static bool CanPromoteMembers(this Supergroup supergroup)
        {
            if (supergroup.Status == null)
            {
                return false;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPromoteMembers;
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

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanInviteUsers;
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

            return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanInviteUsers;
        }

        public static bool CanPostMessages(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            return basicGroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator or ChatMemberStatusMember;
        }

        public static bool IsPublic(this Supergroup group)
        {
            if (group.IsChannel || group.IsBroadcastGroup)
            {
                return group.HasActiveUsername();
            }

            return group.HasActiveUsername()
                || group.HasLocation
                || group.HasLinkedChat;
        }

        public static bool CanJoin(this Supergroup group)
        {
            if (group.IsChannel || group.IsBroadcastGroup)
            {
                if ((group.Status is ChatMemberStatusLeft && (group.HasActiveUsername() /*|| ViewModel.ClientService.IsChatAccessible(chat)*/)) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    return true;
                }
            }
            else
            {
                if ((group.Status is ChatMemberStatusLeft && (group.HasActiveUsername() || group.HasLocation || group.HasLinkedChat /*|| ViewModel.ClientService.IsChatAccessible(chat)*/)) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    return true;
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator)
                {
                    return false;
                }
                else if (group.Status is ChatMemberStatusRestricted restrictedSend)
                {
                    if (!restrictedSend.IsMember && group.HasActiveUsername())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void Update(this File file, File update)
        {
            file.ExpectedSize = update.ExpectedSize;
            file.Size = update.Size;
            file.Local = update.Local;
            file.Remote = update.Remote;
        }

        public static File Update(this File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                file.Local.IsDownloadingCompleted = file.Local.Path.Length > 0
                    && NativeUtils.FileExists(file.Local.Path);
            }

            return file;
        }

        public static File GetLocalFile(string path, string uniqueId = "")
        {
            return new File(0, 0, 0, new LocalFile(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, path), false, false, false, true, 0, 0, 0), new RemoteFile(string.Empty, uniqueId, false, false, 0));
        }
    }

    public static class TdBackground
    {
        public static BackgroundFill FromString(string slug)
        {
            var split = slug.Split('?');
            var query = slug.ParseQueryString();

            if (TryGetColors(split[0], '-', 1, 2, out int[] linear))
            {
                if (linear.Length > 1)
                {
                    query.TryGetValue("rotation", out string rotationKey);
                    int.TryParse(rotationKey ?? string.Empty, out int rotation);

                    return new BackgroundFillGradient(linear[0], linear[1], rotation);
                }

                return new BackgroundFillSolid(linear[0]);
            }
            else if (TryGetColors(split[0], '~', 3, 4, out int[] freeform))
            {
                return new BackgroundFillFreeformGradient(freeform);
            }

            return null;
        }

        public static string ToString(BackgroundFill fill)
        {
            if (fill is BackgroundFillSolid solid)
            {
                return string.Format("{0:X6}", solid.Color);
            }
            else if (fill is BackgroundFillGradient gradient)
            {
                return string.Format("{0:X6}-{1:X6}?rotation={2}", gradient.TopColor, gradient.BottomColor, gradient.RotationAngle);
            }
            else if (fill is BackgroundFillFreeformGradient freeformGradient)
            {
                return string.Join('~', freeformGradient.Colors.Select(x => x.ToString("X6")));
            }

            return null;
        }

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

                var modeSplit = modeKey?.ToLower().Split('+') ?? Array.Empty<string>();

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
            if (split != null && split.Length >= minimum && split.Length <= maximum)
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
            Windows.Foundation.Point topPoint;
            Windows.Foundation.Point bottomPoint;

            switch (angle)
            {
                case 0:
                case 360:
                    topPoint = new Windows.Foundation.Point(0.5, 0);
                    bottomPoint = new Windows.Foundation.Point(0.5, 1);
                    break;
                case 45:
                default:
                    topPoint = new Windows.Foundation.Point(1, 0);
                    bottomPoint = new Windows.Foundation.Point(0, 1);
                    break;
                case 90:
                    topPoint = new Windows.Foundation.Point(1, 0.5);
                    bottomPoint = new Windows.Foundation.Point(0, 0.5);
                    break;
                case 135:
                    topPoint = new Windows.Foundation.Point(1, 1);
                    bottomPoint = new Windows.Foundation.Point(0, 0);
                    break;
                case 180:
                    topPoint = new Windows.Foundation.Point(0.5, 1);
                    bottomPoint = new Windows.Foundation.Point(0.5, 0);
                    break;
                case 225:
                    topPoint = new Windows.Foundation.Point(0, 1);
                    bottomPoint = new Windows.Foundation.Point(1, 0);
                    break;
                case 270:
                    topPoint = new Windows.Foundation.Point(0, 0.5);
                    bottomPoint = new Windows.Foundation.Point(1, 0.5);
                    break;
                case 315:
                    topPoint = new Windows.Foundation.Point(0, 0);
                    bottomPoint = new Windows.Foundation.Point(1, 1);
                    break;
            }

            var brush = new LinearGradientBrush();
            brush.GradientStops.Add(new GradientStop { Color = topColor, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = bottomColor, Offset = 1 });
            brush.StartPoint = topPoint;
            brush.EndPoint = bottomPoint;

            return brush;
        }

        public static CanvasLinearGradientBrush GetGradient(ICanvasResourceCreator sender, int topColor, int bottomColor, int angle, uint width, uint height)
        {
            return GetGradient(sender, topColor.ToColor(), bottomColor.ToColor(), angle, width, height);
        }

        public static CanvasLinearGradientBrush GetGradient(ICanvasResourceCreator sender, Color topColor, Color bottomColor, int angle, uint width, uint height)
        {
            Vector2 topPoint;
            Vector2 bottomPoint;

            switch (angle)
            {
                case 0:
                case 360:
                    topPoint = new Vector2(width / 2f, 0);
                    bottomPoint = new Vector2(width / 2f, height);
                    break;
                case 45:
                default:
                    topPoint = new Vector2(width, 0);
                    bottomPoint = new Vector2(0, height);
                    break;
                case 90:
                    topPoint = new Vector2(width, height / 2f);
                    bottomPoint = new Vector2(0, height / 2f);
                    break;
                case 135:
                    topPoint = new Vector2(width, height);
                    bottomPoint = new Vector2(0, 0);
                    break;
                case 180:
                    topPoint = new Vector2(width / 2f, height);
                    bottomPoint = new Vector2(width / 2f, 0);
                    break;
                case 225:
                    topPoint = new Vector2(0, height);
                    bottomPoint = new Vector2(width, 0);
                    break;
                case 270:
                    topPoint = new Vector2(0, height / 2f);
                    bottomPoint = new Vector2(width, height / 2f);
                    break;
                case 315:
                    topPoint = new Vector2(0, 0);
                    bottomPoint = new Vector2(width, height);
                    break;
            }

            var brush = new CanvasLinearGradientBrush(sender, topColor, bottomColor);
            brush.StartPoint = topPoint;
            brush.EndPoint = bottomPoint;

            return brush;
        }
    }
}
