using Telegram.Td.Api;

namespace Unigram.Services
{
    public partial class ProtoService
    {
        public void MapFiles(object target)
        {
            if (target is PhotoSize photoSize)
            {
                if (photoSize.Photo != null)
                {
                    _files[photoSize.Photo.Id] = photoSize.Photo;
                }
            }
            else if (target is Thumbnail thumbnail)
            {
                if (thumbnail.File != null)
                {
                    _files[thumbnail.File.Id] = thumbnail.File;
                }
            }
            else if (target is Animation animation)
            {
                if (animation.Thumbnail != null)
                {
                    MapFiles(animation.Thumbnail);
                }
                if (animation.AnimationValue != null)
                {
                    _files[animation.AnimationValue.Id] = animation.AnimationValue;
                }
            }
            else if (target is Audio audio)
            {
                if (audio.AlbumCoverThumbnail != null)
                {
                    MapFiles(audio.AlbumCoverThumbnail);
                }
                if (audio.AudioValue != null)
                {
                    _files[audio.AudioValue.Id] = audio.AudioValue;
                }
            }
            else if (target is Document document)
            {
                if (document.Thumbnail != null)
                {
                    MapFiles(document.Thumbnail);
                }
                if (document.DocumentValue != null)
                {
                    _files[document.DocumentValue.Id] = document.DocumentValue;
                }
            }
            else if (target is Photo photo)
            {
                if (photo.Sizes != null)
                {
                    MapFiles(photo.Sizes);
                }
            }
            else if (target is Sticker sticker)
            {
                if (sticker.Thumbnail != null)
                {
                    MapFiles(sticker.Thumbnail);
                }
                if (sticker.StickerValue != null)
                {
                    _files[sticker.StickerValue.Id] = sticker.StickerValue;
                }
            }
            else if (target is Video video)
            {
                if (video.Thumbnail != null)
                {
                    MapFiles(video.Thumbnail);
                }
                if (video.VideoValue != null)
                {
                    _files[video.VideoValue.Id] = video.VideoValue;
                }
            }
            else if (target is VideoNote videoNote)
            {
                if (videoNote.Thumbnail != null)
                {
                    MapFiles(videoNote.Thumbnail);
                }
                if (videoNote.Video != null)
                {
                    _files[videoNote.Video.Id] = videoNote.Video;
                }
            }
            else if (target is VoiceNote voiceNote)
            {
                if (voiceNote.Voice != null)
                {
                    _files[voiceNote.Voice.Id] = voiceNote.Voice;
                }
            }
            else if (target is AnimatedEmoji animatedEmoji)
            {
                if (animatedEmoji.Sticker != null)
                {
                    MapFiles(animatedEmoji.Sticker);
                }
                if (animatedEmoji.Sound != null)
                {
                    _files[animatedEmoji.Sound.Id] = animatedEmoji.Sound;
                }
            }
            else if (target is Game game)
            {
                if (game.Photo != null)
                {
                    MapFiles(game.Photo);
                }
                if (game.Animation != null)
                {
                    MapFiles(game.Animation);
                }
            }
            else if (target is ProfilePhoto profilePhoto)
            {
                if (profilePhoto.Small != null)
                {
                    _files[profilePhoto.Small.Id] = profilePhoto.Small;
                }
                if (profilePhoto.Big != null)
                {
                    _files[profilePhoto.Big.Id] = profilePhoto.Big;
                }
            }
            else if (target is ChatPhotoInfo chatPhotoInfo)
            {
                if (chatPhotoInfo.Small != null)
                {
                    _files[chatPhotoInfo.Small.Id] = chatPhotoInfo.Small;
                }
                if (chatPhotoInfo.Big != null)
                {
                    _files[chatPhotoInfo.Big.Id] = chatPhotoInfo.Big;
                }
            }
            else if (target is AnimatedChatPhoto animatedChatPhoto)
            {
                if (animatedChatPhoto.File != null)
                {
                    _files[animatedChatPhoto.File.Id] = animatedChatPhoto.File;
                }
            }
            else if (target is ChatPhoto chatPhoto)
            {
                if (chatPhoto.Sizes != null)
                {
                    MapFiles(chatPhoto.Sizes);
                }
                if (chatPhoto.Animation != null)
                {
                    MapFiles(chatPhoto.Animation);
                }
            }
            else if (target is ChatPhotos chatPhotos)
            {
                if (chatPhotos.Photos != null)
                {
                    MapFiles(chatPhotos.Photos);
                }
            }
            else if (target is User user)
            {
                if (user.ProfilePhoto != null)
                {
                    MapFiles(user.ProfilePhoto);
                }
            }
            else if (target is UserFullInfo userFullInfo)
            {
                if (userFullInfo.Photo != null)
                {
                    MapFiles(userFullInfo.Photo);
                }
            }
            else if (target is ChatInviteLinkInfo chatInviteLinkInfo)
            {
                if (chatInviteLinkInfo.Photo != null)
                {
                    MapFiles(chatInviteLinkInfo.Photo);
                }
            }
            else if (target is BasicGroupFullInfo basicGroupFullInfo)
            {
                if (basicGroupFullInfo.Photo != null)
                {
                    MapFiles(basicGroupFullInfo.Photo);
                }
            }
            else if (target is SupergroupFullInfo supergroupFullInfo)
            {
                if (supergroupFullInfo.Photo != null)
                {
                    MapFiles(supergroupFullInfo.Photo);
                }
            }
            else if (target is Chat chat)
            {
                if (chat.Photo != null)
                {
                    MapFiles(chat.Photo);
                }
            }
            else if (target is RichTextIcon richTextIcon)
            {
                if (richTextIcon.Document != null)
                {
                    MapFiles(richTextIcon.Document);
                }
            }
            else if (target is PageBlockRelatedArticle pageBlockRelatedArticle)
            {
                if (pageBlockRelatedArticle.Photo != null)
                {
                    MapFiles(pageBlockRelatedArticle.Photo);
                }
            }
            else if (target is PageBlockAnimation pageBlockAnimation)
            {
                if (pageBlockAnimation.Animation != null)
                {
                    MapFiles(pageBlockAnimation.Animation);
                }
            }
            else if (target is PageBlockAudio pageBlockAudio)
            {
                if (pageBlockAudio.Audio != null)
                {
                    MapFiles(pageBlockAudio.Audio);
                }
            }
            else if (target is PageBlockPhoto pageBlockPhoto)
            {
                if (pageBlockPhoto.Photo != null)
                {
                    MapFiles(pageBlockPhoto.Photo);
                }
            }
            else if (target is PageBlockVideo pageBlockVideo)
            {
                if (pageBlockVideo.Video != null)
                {
                    MapFiles(pageBlockVideo.Video);
                }
            }
            else if (target is PageBlockVoiceNote pageBlockVoiceNote)
            {
                if (pageBlockVoiceNote.VoiceNote != null)
                {
                    MapFiles(pageBlockVoiceNote.VoiceNote);
                }
            }
            else if (target is PageBlockEmbedded pageBlockEmbedded)
            {
                if (pageBlockEmbedded.PosterPhoto != null)
                {
                    MapFiles(pageBlockEmbedded.PosterPhoto);
                }
            }
            else if (target is PageBlockEmbeddedPost pageBlockEmbeddedPost)
            {
                if (pageBlockEmbeddedPost.AuthorPhoto != null)
                {
                    MapFiles(pageBlockEmbeddedPost.AuthorPhoto);
                }
            }
            else if (target is PageBlockChatLink pageBlockChatLink)
            {
                if (pageBlockChatLink.Photo != null)
                {
                    MapFiles(pageBlockChatLink.Photo);
                }
            }
            else if (target is PageBlockRelatedArticles pageBlockRelatedArticles)
            {
                if (pageBlockRelatedArticles.Articles != null)
                {
                    MapFiles(pageBlockRelatedArticles.Articles);
                }
            }
            else if (target is WebPage webPage)
            {
                if (webPage.Photo != null)
                {
                    MapFiles(webPage.Photo);
                }
                if (webPage.Animation != null)
                {
                    MapFiles(webPage.Animation);
                }
                if (webPage.Audio != null)
                {
                    MapFiles(webPage.Audio);
                }
                if (webPage.Document != null)
                {
                    MapFiles(webPage.Document);
                }
                if (webPage.Sticker != null)
                {
                    MapFiles(webPage.Sticker);
                }
                if (webPage.Video != null)
                {
                    MapFiles(webPage.Video);
                }
                if (webPage.VideoNote != null)
                {
                    MapFiles(webPage.VideoNote);
                }
                if (webPage.VoiceNote != null)
                {
                    MapFiles(webPage.VoiceNote);
                }
            }
            else if (target is PaymentReceipt paymentReceipt)
            {
                if (paymentReceipt.Photo != null)
                {
                    MapFiles(paymentReceipt.Photo);
                }
            }
            else if (target is DatedFile datedFile)
            {
                if (datedFile.File != null)
                {
                    _files[datedFile.File.Id] = datedFile.File;
                }
            }
            else if (target is IdentityDocument identityDocument)
            {
                if (identityDocument.FrontSide != null)
                {
                    MapFiles(identityDocument.FrontSide);
                }
                if (identityDocument.ReverseSide != null)
                {
                    MapFiles(identityDocument.ReverseSide);
                }
                if (identityDocument.Selfie != null)
                {
                    MapFiles(identityDocument.Selfie);
                }
                if (identityDocument.Translation != null)
                {
                    MapFiles(identityDocument.Translation);
                }
            }
            else if (target is PersonalDocument personalDocument)
            {
                if (personalDocument.Files != null)
                {
                    MapFiles(personalDocument.Files);
                }
                if (personalDocument.Translation != null)
                {
                    MapFiles(personalDocument.Translation);
                }
            }
            else if (target is PassportElementPassport passportElementPassport)
            {
                if (passportElementPassport.Passport != null)
                {
                    MapFiles(passportElementPassport.Passport);
                }
            }
            else if (target is PassportElementDriverLicense passportElementDriverLicense)
            {
                if (passportElementDriverLicense.DriverLicense != null)
                {
                    MapFiles(passportElementDriverLicense.DriverLicense);
                }
            }
            else if (target is PassportElementIdentityCard passportElementIdentityCard)
            {
                if (passportElementIdentityCard.IdentityCard != null)
                {
                    MapFiles(passportElementIdentityCard.IdentityCard);
                }
            }
            else if (target is PassportElementInternalPassport passportElementInternalPassport)
            {
                if (passportElementInternalPassport.InternalPassport != null)
                {
                    MapFiles(passportElementInternalPassport.InternalPassport);
                }
            }
            else if (target is PassportElementUtilityBill passportElementUtilityBill)
            {
                if (passportElementUtilityBill.UtilityBill != null)
                {
                    MapFiles(passportElementUtilityBill.UtilityBill);
                }
            }
            else if (target is PassportElementBankStatement passportElementBankStatement)
            {
                if (passportElementBankStatement.BankStatement != null)
                {
                    MapFiles(passportElementBankStatement.BankStatement);
                }
            }
            else if (target is PassportElementRentalAgreement passportElementRentalAgreement)
            {
                if (passportElementRentalAgreement.RentalAgreement != null)
                {
                    MapFiles(passportElementRentalAgreement.RentalAgreement);
                }
            }
            else if (target is PassportElementPassportRegistration passportElementPassportRegistration)
            {
                if (passportElementPassportRegistration.PassportRegistration != null)
                {
                    MapFiles(passportElementPassportRegistration.PassportRegistration);
                }
            }
            else if (target is PassportElementTemporaryRegistration passportElementTemporaryRegistration)
            {
                if (passportElementTemporaryRegistration.TemporaryRegistration != null)
                {
                    MapFiles(passportElementTemporaryRegistration.TemporaryRegistration);
                }
            }
            else if (target is EncryptedPassportElement encryptedPassportElement)
            {
                if (encryptedPassportElement.FrontSide != null)
                {
                    MapFiles(encryptedPassportElement.FrontSide);
                }
                if (encryptedPassportElement.ReverseSide != null)
                {
                    MapFiles(encryptedPassportElement.ReverseSide);
                }
                if (encryptedPassportElement.Selfie != null)
                {
                    MapFiles(encryptedPassportElement.Selfie);
                }
                if (encryptedPassportElement.Translation != null)
                {
                    MapFiles(encryptedPassportElement.Translation);
                }
                if (encryptedPassportElement.Files != null)
                {
                    MapFiles(encryptedPassportElement.Files);
                }
            }
            else if (target is MessageText messageText)
            {
                if (messageText.WebPage != null)
                {
                    MapFiles(messageText.WebPage);
                }
            }
            else if (target is MessageAnimation messageAnimation)
            {
                if (messageAnimation.Animation != null)
                {
                    MapFiles(messageAnimation.Animation);
                }
            }
            else if (target is MessageAudio messageAudio)
            {
                if (messageAudio.Audio != null)
                {
                    MapFiles(messageAudio.Audio);
                }
            }
            else if (target is MessageDocument messageDocument)
            {
                if (messageDocument.Document != null)
                {
                    MapFiles(messageDocument.Document);
                }
            }
            else if (target is MessagePhoto messagePhoto)
            {
                if (messagePhoto.Photo != null)
                {
                    MapFiles(messagePhoto.Photo);
                }
            }
            else if (target is MessageSticker messageSticker)
            {
                if (messageSticker.Sticker != null)
                {
                    MapFiles(messageSticker.Sticker);
                }
            }
            else if (target is MessageVideo messageVideo)
            {
                if (messageVideo.Video != null)
                {
                    MapFiles(messageVideo.Video);
                }
            }
            else if (target is MessageVideoNote messageVideoNote)
            {
                if (messageVideoNote.VideoNote != null)
                {
                    MapFiles(messageVideoNote.VideoNote);
                }
            }
            else if (target is MessageVoiceNote messageVoiceNote)
            {
                if (messageVoiceNote.VoiceNote != null)
                {
                    MapFiles(messageVoiceNote.VoiceNote);
                }
            }
            else if (target is MessageAnimatedEmoji messageAnimatedEmoji)
            {
                if (messageAnimatedEmoji.AnimatedEmoji != null)
                {
                    MapFiles(messageAnimatedEmoji.AnimatedEmoji);
                }
            }
            else if (target is MessageGame messageGame)
            {
                if (messageGame.Game != null)
                {
                    MapFiles(messageGame.Game);
                }
            }
            else if (target is MessageInvoice messageInvoice)
            {
                if (messageInvoice.Photo != null)
                {
                    MapFiles(messageInvoice.Photo);
                }
            }
            else if (target is MessageChatChangePhoto messageChatChangePhoto)
            {
                if (messageChatChangePhoto.Photo != null)
                {
                    MapFiles(messageChatChangePhoto.Photo);
                }
            }
            else if (target is MessagePassportDataReceived messagePassportDataReceived)
            {
                if (messagePassportDataReceived.Elements != null)
                {
                    MapFiles(messagePassportDataReceived.Elements);
                }
            }
            else if (target is Stickers stickers)
            {
                if (stickers.StickersValue != null)
                {
                    MapFiles(stickers.StickersValue);
                }
            }
            else if (target is StickerSet stickerSet)
            {
                if (stickerSet.Thumbnail != null)
                {
                    MapFiles(stickerSet.Thumbnail);
                }
                if (stickerSet.Stickers != null)
                {
                    MapFiles(stickerSet.Stickers);
                }
            }
            else if (target is StickerSetInfo stickerSetInfo)
            {
                if (stickerSetInfo.Thumbnail != null)
                {
                    MapFiles(stickerSetInfo.Thumbnail);
                }
                if (stickerSetInfo.Covers != null)
                {
                    MapFiles(stickerSetInfo.Covers);
                }
            }
            else if (target is StickerSets stickerSets)
            {
                if (stickerSets.Sets != null)
                {
                    MapFiles(stickerSets.Sets);
                }
            }
            else if (target is Animations animations)
            {
                if (animations.AnimationsValue != null)
                {
                    MapFiles(animations.AnimationsValue);
                }
            }
            else if (target is DiceStickersRegular diceStickersRegular)
            {
                if (diceStickersRegular.Sticker != null)
                {
                    MapFiles(diceStickersRegular.Sticker);
                }
            }
            else if (target is DiceStickersSlotMachine diceStickersSlotMachine)
            {
                if (diceStickersSlotMachine.Background != null)
                {
                    MapFiles(diceStickersSlotMachine.Background);
                }
                if (diceStickersSlotMachine.Lever != null)
                {
                    MapFiles(diceStickersSlotMachine.Lever);
                }
                if (diceStickersSlotMachine.LeftReel != null)
                {
                    MapFiles(diceStickersSlotMachine.LeftReel);
                }
                if (diceStickersSlotMachine.CenterReel != null)
                {
                    MapFiles(diceStickersSlotMachine.CenterReel);
                }
                if (diceStickersSlotMachine.RightReel != null)
                {
                    MapFiles(diceStickersSlotMachine.RightReel);
                }
            }
            else if (target is InlineQueryResultArticle inlineQueryResultArticle)
            {
                if (inlineQueryResultArticle.Thumbnail != null)
                {
                    MapFiles(inlineQueryResultArticle.Thumbnail);
                }
            }
            else if (target is InlineQueryResultContact inlineQueryResultContact)
            {
                if (inlineQueryResultContact.Thumbnail != null)
                {
                    MapFiles(inlineQueryResultContact.Thumbnail);
                }
            }
            else if (target is InlineQueryResultLocation inlineQueryResultLocation)
            {
                if (inlineQueryResultLocation.Thumbnail != null)
                {
                    MapFiles(inlineQueryResultLocation.Thumbnail);
                }
            }
            else if (target is InlineQueryResultVenue inlineQueryResultVenue)
            {
                if (inlineQueryResultVenue.Thumbnail != null)
                {
                    MapFiles(inlineQueryResultVenue.Thumbnail);
                }
            }
            else if (target is InlineQueryResultGame inlineQueryResultGame)
            {
                if (inlineQueryResultGame.Game != null)
                {
                    MapFiles(inlineQueryResultGame.Game);
                }
            }
            else if (target is InlineQueryResultAnimation inlineQueryResultAnimation)
            {
                if (inlineQueryResultAnimation.Animation != null)
                {
                    MapFiles(inlineQueryResultAnimation.Animation);
                }
            }
            else if (target is InlineQueryResultAudio inlineQueryResultAudio)
            {
                if (inlineQueryResultAudio.Audio != null)
                {
                    MapFiles(inlineQueryResultAudio.Audio);
                }
            }
            else if (target is InlineQueryResultDocument inlineQueryResultDocument)
            {
                if (inlineQueryResultDocument.Document != null)
                {
                    MapFiles(inlineQueryResultDocument.Document);
                }
            }
            else if (target is InlineQueryResultPhoto inlineQueryResultPhoto)
            {
                if (inlineQueryResultPhoto.Photo != null)
                {
                    MapFiles(inlineQueryResultPhoto.Photo);
                }
            }
            else if (target is InlineQueryResultSticker inlineQueryResultSticker)
            {
                if (inlineQueryResultSticker.Sticker != null)
                {
                    MapFiles(inlineQueryResultSticker.Sticker);
                }
            }
            else if (target is InlineQueryResultVideo inlineQueryResultVideo)
            {
                if (inlineQueryResultVideo.Video != null)
                {
                    MapFiles(inlineQueryResultVideo.Video);
                }
            }
            else if (target is InlineQueryResultVoiceNote inlineQueryResultVoiceNote)
            {
                if (inlineQueryResultVoiceNote.VoiceNote != null)
                {
                    MapFiles(inlineQueryResultVoiceNote.VoiceNote);
                }
            }
            else if (target is ChatEventPhotoChanged chatEventPhotoChanged)
            {
                if (chatEventPhotoChanged.OldPhoto != null)
                {
                    MapFiles(chatEventPhotoChanged.OldPhoto);
                }
                if (chatEventPhotoChanged.NewPhoto != null)
                {
                    MapFiles(chatEventPhotoChanged.NewPhoto);
                }
            }
            else if (target is Background background)
            {
                if (background.Document != null)
                {
                    MapFiles(background.Document);
                }
            }
            else if (target is Backgrounds backgrounds)
            {
                if (backgrounds.BackgroundsValue != null)
                {
                    MapFiles(backgrounds.BackgroundsValue);
                }
            }
            else if (target is ThemeSettings themeSettings)
            {
                if (themeSettings.Background != null)
                {
                    MapFiles(themeSettings.Background);
                }
            }
            else if (target is ChatTheme chatTheme)
            {
                if (chatTheme.LightSettings != null)
                {
                    MapFiles(chatTheme.LightSettings);
                }
                if (chatTheme.DarkSettings != null)
                {
                    MapFiles(chatTheme.DarkSettings);
                }
            }
            else if (target is PushMessageContentAnimation pushMessageContentAnimation)
            {
                if (pushMessageContentAnimation.Animation != null)
                {
                    MapFiles(pushMessageContentAnimation.Animation);
                }
            }
            else if (target is PushMessageContentAudio pushMessageContentAudio)
            {
                if (pushMessageContentAudio.Audio != null)
                {
                    MapFiles(pushMessageContentAudio.Audio);
                }
            }
            else if (target is PushMessageContentDocument pushMessageContentDocument)
            {
                if (pushMessageContentDocument.Document != null)
                {
                    MapFiles(pushMessageContentDocument.Document);
                }
            }
            else if (target is PushMessageContentPhoto pushMessageContentPhoto)
            {
                if (pushMessageContentPhoto.Photo != null)
                {
                    MapFiles(pushMessageContentPhoto.Photo);
                }
            }
            else if (target is PushMessageContentSticker pushMessageContentSticker)
            {
                if (pushMessageContentSticker.Sticker != null)
                {
                    MapFiles(pushMessageContentSticker.Sticker);
                }
            }
            else if (target is PushMessageContentVideo pushMessageContentVideo)
            {
                if (pushMessageContentVideo.Video != null)
                {
                    MapFiles(pushMessageContentVideo.Video);
                }
            }
            else if (target is PushMessageContentVideoNote pushMessageContentVideoNote)
            {
                if (pushMessageContentVideoNote.VideoNote != null)
                {
                    MapFiles(pushMessageContentVideoNote.VideoNote);
                }
            }
            else if (target is PushMessageContentVoiceNote pushMessageContentVoiceNote)
            {
                if (pushMessageContentVoiceNote.VoiceNote != null)
                {
                    MapFiles(pushMessageContentVoiceNote.VoiceNote);
                }
            }
            else if (target is TMeUrlTypeChatInvite tMeUrlTypeChatInvite)
            {
                if (tMeUrlTypeChatInvite.Info != null)
                {
                    MapFiles(tMeUrlTypeChatInvite.Info);
                }
            }
            else if (target is UpdateNewChat updateNewChat)
            {
                if (updateNewChat.Chat != null)
                {
                    MapFiles(updateNewChat.Chat);
                }
            }
            else if (target is UpdateChatPhoto updateChatPhoto)
            {
                if (updateChatPhoto.Photo != null)
                {
                    MapFiles(updateChatPhoto.Photo);
                }
            }
            else if (target is UpdateUser updateUser)
            {
                if (updateUser.User != null)
                {
                    MapFiles(updateUser.User);
                }
            }
            else if (target is UpdateUserFullInfo updateUserFullInfo)
            {
                if (updateUserFullInfo.UserFullInfo != null)
                {
                    MapFiles(updateUserFullInfo.UserFullInfo);
                }
            }
            else if (target is UpdateBasicGroupFullInfo updateBasicGroupFullInfo)
            {
                if (updateBasicGroupFullInfo.BasicGroupFullInfo != null)
                {
                    MapFiles(updateBasicGroupFullInfo.BasicGroupFullInfo);
                }
            }
            else if (target is UpdateSupergroupFullInfo updateSupergroupFullInfo)
            {
                if (updateSupergroupFullInfo.SupergroupFullInfo != null)
                {
                    MapFiles(updateSupergroupFullInfo.SupergroupFullInfo);
                }
            }
            else if (target is UpdateFile updateFile)
            {
                if (updateFile.File != null)
                {
                    _files[updateFile.File.Id] = updateFile.File;
                }
            }
            else if (target is UpdateStickerSet updateStickerSet)
            {
                if (updateStickerSet.StickerSet != null)
                {
                    MapFiles(updateStickerSet.StickerSet);
                }
            }
            else if (target is UpdateTrendingStickerSets updateTrendingStickerSets)
            {
                if (updateTrendingStickerSets.StickerSets != null)
                {
                    MapFiles(updateTrendingStickerSets.StickerSets);
                }
            }
            else if (target is UpdateSelectedBackground updateSelectedBackground)
            {
                if (updateSelectedBackground.Background != null)
                {
                    MapFiles(updateSelectedBackground.Background);
                }
            }
            else if (target is UpdateChatThemes updateChatThemes)
            {
                if (updateChatThemes.ChatThemes != null)
                {
                    MapFiles(updateChatThemes.ChatThemes);
                }
            }
            else if (target is UpdateAnimatedEmojiMessageClicked updateAnimatedEmojiMessageClicked)
            {
                if (updateAnimatedEmojiMessageClicked.Sticker != null)
                {
                    MapFiles(updateAnimatedEmojiMessageClicked.Sticker);
                }
            }
        }
    }
}
