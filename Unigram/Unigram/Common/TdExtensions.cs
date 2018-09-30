using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Selectors;
using Unigram.Services;
using Unigram.ViewModels.Settings;

namespace Unigram.Common
{
    public static class TdExtensions
    {
        #region Passport

        public static PersonalDocument GetPersonalDocument(this PassportElement element)
        {
            switch (element)
            {
                case PassportElementBankStatement bankStatement:
                    return bankStatement.BankStatement;
                case PassportElementPassportRegistration passportRegistration:
                    return passportRegistration.PassportRegistration;
                case PassportElementRentalAgreement rentalAgreement:
                    return rentalAgreement.RentalAgreement;
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return temporaryRegistration.TemporaryRegistration;
                case PassportElementUtilityBill utilityBill:
                    return utilityBill.UtilityBill;
                default:
                    return null;
            }
        }

        public static void SetPersonalDocument(this InputPassportElement element, InputPersonalDocument document)
        {
            switch (element)
            {
                case InputPassportElementBankStatement bankStatement:
                    bankStatement.BankStatement = document;
                    break;
                case InputPassportElementPassportRegistration passportRegistration:
                    passportRegistration.PassportRegistration = document;
                    break;
                case InputPassportElementRentalAgreement rentalAgreement:
                    rentalAgreement.RentalAgreement = document;
                    break;
                case InputPassportElementTemporaryRegistration temporaryRegistration:
                    temporaryRegistration.TemporaryRegistration = document;
                    break;
                case InputPassportElementUtilityBill utilityBill:
                    utilityBill.UtilityBill = document;
                    break;
            }
        }

        public static IdentityDocument GetIdentityDocument(this PassportElement element)
        {
            switch (element)
            {
                case PassportElementDriverLicense driverLicense:
                    return driverLicense.DriverLicense;
                case PassportElementIdentityCard identityCard:
                    return identityCard.IdentityCard;
                case PassportElementInternalPassport internalPassport:
                    return internalPassport.InternalPassport;
                case PassportElementPassport passport:
                    return passport.Passport;
                default:
                    return null;
            }
        }

        public static InputPassportElement ToInputElement(this PassportElement element)
        {
            switch (element)
            {
                case PassportElementAddress address:
                    return new InputPassportElementAddress();
                case PassportElementPersonalDetails personalDetails:
                    return new InputPassportElementPersonalDetails();
                case PassportElementEmailAddress emailAddress:
                    return new InputPassportElementEmailAddress();
                case PassportElementPhoneNumber phoneNumber:
                    return new InputPassportElementPhoneNumber();
                case PassportElementBankStatement bankStatement:
                    return new InputPassportElementBankStatement();
                case PassportElementPassportRegistration passportRegistration:
                    return new InputPassportElementPassportRegistration();
                case PassportElementRentalAgreement rentalAgreement:
                    return new InputPassportElementRentalAgreement();
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return new InputPassportElementTemporaryRegistration();
                case PassportElementUtilityBill utilityBill:
                    return new InputPassportElementUtilityBill();
                case PassportElementDriverLicense driverLicense:
                    return new InputPassportElementDriverLicense();
                case PassportElementIdentityCard identityCard:
                    return new InputPassportElementIdentityCard();
                case PassportElementInternalPassport internalPassport:
                    return new InputPassportElementInternalPassport();
                case PassportElementPassport passport:
                    return new InputPassportElementPassport();
                default:
                    return null;
            }
        }

        public static PassportElementType ToElementType(this PassportElement element)
        {
            switch (element)
            {
                case PassportElementAddress address:
                    return new PassportElementTypeAddress();
                case PassportElementPersonalDetails personalDetails:
                    return new PassportElementTypePersonalDetails();
                case PassportElementEmailAddress emailAddress:
                    return new PassportElementTypeEmailAddress();
                case PassportElementPhoneNumber phoneNumber:
                    return new PassportElementTypePhoneNumber();
                case PassportElementBankStatement bankStatement:
                    return new PassportElementTypeBankStatement();
                case PassportElementPassportRegistration passportRegistration:
                    return new PassportElementTypePassportRegistration();
                case PassportElementRentalAgreement rentalAgreement:
                    return new PassportElementTypeRentalAgreement();
                case PassportElementTemporaryRegistration temporaryRegistration:
                    return new PassportElementTypeTemporaryRegistration();
                case PassportElementUtilityBill utilityBill:
                    return new PassportElementTypeUtilityBill();
                case PassportElementDriverLicense driverLicense:
                    return new PassportElementTypeDriverLicense();
                case PassportElementIdentityCard identityCard:
                    return new PassportElementTypeIdentityCard();
                case PassportElementInternalPassport internalPassport:
                    return new PassportElementTypeInternalPassport();
                case PassportElementPassport passport:
                    return new PassportElementTypePassport();
                default:
                    return null;
            }
        }

        public static PassportElement GetElementForType(this PassportElementsWithErrors authorizationForm, PassportElementType type)
        {
            foreach (var element in authorizationForm.Elements)
            {
                if (element is PassportElementAddress && type is PassportElementTypeAddress)
                {
                    return element;
                }
                else if (element is PassportElementPersonalDetails && type is PassportElementTypePersonalDetails)
                {
                    return element;
                }
                else if (element is PassportElementEmailAddress && type is PassportElementTypeEmailAddress)
                {
                    return element;
                }
                else if (element is PassportElementPhoneNumber && type is PassportElementTypePhoneNumber)
                {
                    return element;
                }
                else if (element is PassportElementBankStatement && type is PassportElementTypeBankStatement)
                {
                    return element;
                }
                else if (element is PassportElementPassportRegistration && type is PassportElementTypePassportRegistration)
                {
                    return element;
                }
                else if (element is PassportElementRentalAgreement && type is PassportElementTypeRentalAgreement)
                {
                    return element;
                }
                else if (element is PassportElementTemporaryRegistration && type is PassportElementTypeTemporaryRegistration)
                {
                    return element;
                }
                else if (element is PassportElementUtilityBill && type is PassportElementTypeUtilityBill)
                {
                    return element;
                }
                else if (element is PassportElementDriverLicense && type is PassportElementTypeDriverLicense)
                {
                    return element;
                }
                else if (element is PassportElementIdentityCard && type is PassportElementTypeIdentityCard)
                {
                    return element;
                }
                else if (element is PassportElementInternalPassport && type is PassportElementTypeInternalPassport)
                {
                    return element;
                }
                else if (element is PassportElementPassport && type is PassportElementTypePassport)
                {
                    return element;
                }
            }

            return null;
        }

        public static IEnumerable<PassportElementError> GetErrorsForType(this PassportElementsWithErrors authorizationForm, PassportElementType type)
        {
            foreach (var error in authorizationForm.Errors)
            {
                if (error.Type is PassportElementTypeAddress && type is PassportElementTypeAddress)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypePersonalDetails && type is PassportElementTypePersonalDetails)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeEmailAddress && type is PassportElementTypeEmailAddress)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypePhoneNumber && type is PassportElementTypePhoneNumber)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeBankStatement && type is PassportElementTypeBankStatement)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypePassportRegistration && type is PassportElementTypePassportRegistration)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeRentalAgreement && type is PassportElementTypeRentalAgreement)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeTemporaryRegistration && type is PassportElementTypeTemporaryRegistration)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeUtilityBill && type is PassportElementTypeUtilityBill)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeDriverLicense && type is PassportElementTypeDriverLicense)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeIdentityCard && type is PassportElementTypeIdentityCard)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypeInternalPassport && type is PassportElementTypeInternalPassport)
                {
                    yield return error;
                }
                else if (error.Type is PassportElementTypePassport && type is PassportElementTypePassport)
                {
                    yield return error;
                }
            }
        }

        #endregion

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

        public static File GetThumbnail(this Message message)
        {
            switch (message.Content)
            {
                case MessageAnimation animation:
                    return animation.Animation.Thumbnail?.Photo;
                case MessageAudio audio:
                    return audio.Audio.AudioValue;
                case MessageDocument document:
                    return document.Document.Thumbnail?.Photo;
                case MessageGame game:
                    return game.Game.Animation?.Thumbnail?.Photo;
                case MessageSticker sticker:
                    return sticker.Sticker.Thumbnail?.Photo;
                case MessageText text:
                    if (text.WebPage != null && text.WebPage.Animation != null)
                    {
                        return text.WebPage.Animation.Thumbnail?.Photo;
                    }
                    else if (text.WebPage != null && text.WebPage.Audio != null)
                    {
                        return text.WebPage.Audio.AlbumCoverThumbnail?.Photo;
                    }
                    else if (text.WebPage != null && text.WebPage.Document != null)
                    {
                        return text.WebPage.Document.Thumbnail?.Photo;
                    }
                    else if (text.WebPage != null && text.WebPage.Sticker != null)
                    {
                        return text.WebPage.Sticker.Thumbnail?.Photo;
                    }
                    else if (text.WebPage != null && text.WebPage.Video != null)
                    {
                        return text.WebPage.Video.Thumbnail?.Photo;
                    }
                    else if (text.WebPage != null && text.WebPage.VideoNote != null)
                    {
                        return text.WebPage.VideoNote.Thumbnail?.Photo;
                    }
                    break;
                case MessageVideo video:
                    return video.Video.Thumbnail?.Photo;
                case MessageVideoNote videoNote:
                    return videoNote.VideoNote.Thumbnail?.Photo;
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

        public static Photo ToPhoto(this ChatPhoto chatPhoto)
        {
            return new Photo(false, new PhotoSize[] { new PhotoSize("t", chatPhoto.Small, 160, 160), new PhotoSize("i", chatPhoto.Big, 640, 640) });
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

                    return big != null && big.Width > 400 && webPage.HasInstantView;
                }
            }

            return false;
        }

        public static bool IsSmallPhoto(this WebPage webPage)
        {
            if (webPage.Photo != null)
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
                    if (chatEvent.IsFull)
                    {
                        switch (chatEvent.Event.Action)
                        {
                            case ChatEventMessageDeleted messageDeleted:
                                return messageDeleted.Message.IsService();
                            case ChatEventMessageEdited messageEdited:
                                return messageEdited.NewMessage.IsService();
                            case ChatEventMessagePinned messagePinned:
                                return messagePinned.Message.IsService();
                        }
                    }
                    return true;
                case MessageHeaderDate headerDate:
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
                    return true;
                    return string.IsNullOrEmpty(animation.Title);
                case InlineQueryResultLocation location:
                    return string.IsNullOrEmpty(location.Title);
                case InlineQueryResultPhoto photo:
                    return true;
                    return string.IsNullOrEmpty(photo.Title);
                case InlineQueryResultSticker sticker:
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
            return restricted.RestrictedUntilDate == 0 || Math.Abs(restricted.RestrictedUntilDate - DateTime.Now.ToTimestamp() / 1000) > 5 * 365 * 24 * 60 * 60;
        }

        public static bool IsForever(this ChatMemberStatusBanned banned)
        {
            return banned.BannedUntilDate == 0 || Math.Abs(banned.BannedUntilDate - DateTime.Now.ToTimestamp() / 1000) > 5 * 365 * 24 * 60 * 60;
        }


        public static string GetTitle(this Audio audio)
        {
            var performer = string.IsNullOrEmpty(audio.Performer) ? null : audio.Performer;
            var title = string.IsNullOrEmpty(audio.Title) ? null : audio.Title;

            if (performer == null && title == null)
            {
                return audio.FileName;
            }
            else
            {
                return $"{performer ?? Strings.Resources.AudioUnknownArtist} - {title ?? Strings.Resources.AudioUnknownTitle}";
            }
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
                        return TdNetworkType.Other;
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
                        return TdNetworkType.Other;
                    case NetworkTypeWiFi wifi:
                        return TdNetworkType.WiFi;
                }
            }

            return TdNetworkType.Other;
        }

        public static bool IsSaved(this Message message)
        {
            if (message.ForwardInfo is MessageForwardedFromUser fromUser)
            {
                return fromUser.ForwardedFromChatId != 0;
            }
            else if (message.ForwardInfo is MessageForwardedPost post)
            {
                return post.ForwardedFromChatId != 0;
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

        public static PhotoSize GetSize(this Wallpaper wallpaper, bool thumbnail)
        {
            return thumbnail ? wallpaper.GetSmall() : wallpaper.GetBig();
        }

        public static PhotoSize GetSize(this Photo photo, bool thumbnail)
        {
            return thumbnail ? photo.GetSmall() : photo.GetBig();
        }

        public static PhotoSize GetSmall(this Wallpaper wallpaper)
        {
            return wallpaper.Sizes.OrderBy(x => x.Width).FirstOrDefault();

            PhotoSize thumb = null;
            int thumbLevel = -1;

            foreach (var i in wallpaper.Sizes)
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

        public static PhotoSize GetBig(this Wallpaper wallpaper)
        {
            return wallpaper.Sizes.OrderByDescending(x => x.Width).FirstOrDefault();

            PhotoSize full = null;
            int fullLevel = -1;

            foreach (var i in wallpaper.Sizes)
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

        public static PhotoSize GetSmall(this UserProfilePhoto photo)
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

        public static PhotoSize GetBig(this UserProfilePhoto photo)
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

            if (supergroup.AnyoneCanInvite && supergroup.Status is ChatMemberStatusMember)
            {
                return true;
            }

            return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers;
        }

        public static bool CanInviteUsers(this BasicGroup basicGroup)
        {
            if (basicGroup.Status == null)
            {
                return false;
            }

            if (basicGroup.EveryoneIsAdministrator)
            {
                return true;
            }

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
            if (animation.Thumbnail != null && animation.Thumbnail.Photo.Id == file.Id)
            {
                animation.Thumbnail.Photo = file;
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
            if (audio.AlbumCoverThumbnail != null && audio.AlbumCoverThumbnail.Photo.Id == file.Id)
            {
                audio.AlbumCoverThumbnail.Photo = file;
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
            if (document.Thumbnail != null && document.Thumbnail.Photo.Id == file.Id)
            {
                document.Thumbnail.Photo = file;
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




        public static bool UpdateFile(this UserProfilePhoto photo, File file)
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




        public static bool UpdateFile(this MessageSticker sticker, File file)
        {
            return sticker.Sticker.UpdateFile(file);
        }

        public static bool UpdateFile(this Sticker sticker, File file)
        {
            if (sticker.Thumbnail != null && sticker.Thumbnail.Photo.Id == file.Id)
            {
                sticker.Thumbnail.Photo = file;
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
            if (video.Thumbnail != null && video.Thumbnail.Photo.Id == file.Id)
            {
                video.Thumbnail.Photo = file;
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
            if (videoNote.Thumbnail != null && videoNote.Thumbnail.Photo.Id == file.Id)
            {
                videoNote.Thumbnail.Photo = file;
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



        public static void Update(this File file, File update)
        {
            file.ExpectedSize = update.ExpectedSize;
            file.Size = update.Size;
            file.Local = update.Local;
            file.Remote = update.Remote;
        }
    }
}

namespace Telegram.Td.Api
{
    public class MessageChatEvent : MessageContent
    {
        public ChatEvent Event { get; set; }
        public bool IsFull { get; set; }

        public MessageChatEvent(ChatEvent chatEvent, bool isFull)
        {
            Event = chatEvent;
            IsFull = isFull;
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
}
