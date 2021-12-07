using Telegram.Td.Api;
using Unigram.Common;

namespace Unigram.Services
{
    public partial class ProtoService
    {
        private File ProcessFile(File file)
        {
            if (_files.TryGetValue(file.Id, out File singleton))
            {
                singleton.Update(file);
                return singleton;
            }
            else
            {
                _files[file.Id] = file;
                return file;
            }
        }

        public void ProcessFiles(object target)
        {
            if (target is PhotoSize photoSize)
            {
                if (photoSize.Photo != null)
                {
                    photoSize.Photo = ProcessFile(photoSize.Photo);
                }
            }
            else if (target is Thumbnail thumbnail)
            {
                if (thumbnail.File != null)
                {
                    thumbnail.File = ProcessFile(thumbnail.File);
                }
            }
            else if (target is Animation animation)
            {
                if (animation.Thumbnail != null)
                {
                    ProcessFiles(animation.Thumbnail);
                }
                if (animation.AnimationValue != null)
                {
                    animation.AnimationValue = ProcessFile(animation.AnimationValue);
                }
            }
            else if (target is Audio audio)
            {
                if (audio.AlbumCoverThumbnail != null)
                {
                    ProcessFiles(audio.AlbumCoverThumbnail);
                }
                if (audio.AudioValue != null)
                {
                    audio.AudioValue = ProcessFile(audio.AudioValue);
                }
            }
            else if (target is Document document)
            {
                if (document.Thumbnail != null)
                {
                    ProcessFiles(document.Thumbnail);
                }
                if (document.DocumentValue != null)
                {
                    document.DocumentValue = ProcessFile(document.DocumentValue);
                }
            }
            else if (target is Photo photo)
            {
                foreach (var item in photo.Sizes)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Sticker sticker)
            {
                if (sticker.Thumbnail != null)
                {
                    ProcessFiles(sticker.Thumbnail);
                }
                if (sticker.StickerValue != null)
                {
                    sticker.StickerValue = ProcessFile(sticker.StickerValue);
                }
            }
            else if (target is Video video)
            {
                if (video.Thumbnail != null)
                {
                    ProcessFiles(video.Thumbnail);
                }
                if (video.VideoValue != null)
                {
                    video.VideoValue = ProcessFile(video.VideoValue);
                }
            }
            else if (target is VideoNote videoNote)
            {
                if (videoNote.Thumbnail != null)
                {
                    ProcessFiles(videoNote.Thumbnail);
                }
                if (videoNote.Video != null)
                {
                    videoNote.Video = ProcessFile(videoNote.Video);
                }
            }
            else if (target is VoiceNote voiceNote)
            {
                if (voiceNote.Voice != null)
                {
                    voiceNote.Voice = ProcessFile(voiceNote.Voice);
                }
            }
            else if (target is AnimatedEmoji animatedEmoji)
            {
                if (animatedEmoji.Sticker != null)
                {
                    ProcessFiles(animatedEmoji.Sticker);
                }
                if (animatedEmoji.Sound != null)
                {
                    animatedEmoji.Sound = ProcessFile(animatedEmoji.Sound);
                }
            }
            else if (target is Game game)
            {
                if (game.Photo != null)
                {
                    ProcessFiles(game.Photo);
                }
                if (game.Animation != null)
                {
                    ProcessFiles(game.Animation);
                }
            }
            else if (target is ProfilePhoto profilePhoto)
            {
                if (profilePhoto.Small != null)
                {
                    profilePhoto.Small = ProcessFile(profilePhoto.Small);
                }
                if (profilePhoto.Big != null)
                {
                    profilePhoto.Big = ProcessFile(profilePhoto.Big);
                }
            }
            else if (target is ChatPhotoInfo chatPhotoInfo)
            {
                if (chatPhotoInfo.Small != null)
                {
                    chatPhotoInfo.Small = ProcessFile(chatPhotoInfo.Small);
                }
                if (chatPhotoInfo.Big != null)
                {
                    chatPhotoInfo.Big = ProcessFile(chatPhotoInfo.Big);
                }
            }
            else if (target is AnimatedChatPhoto animatedChatPhoto)
            {
                if (animatedChatPhoto.File != null)
                {
                    animatedChatPhoto.File = ProcessFile(animatedChatPhoto.File);
                }
            }
            else if (target is ChatPhoto chatPhoto)
            {
                foreach (var item in chatPhoto.Sizes)
                {
                    ProcessFiles(item);
                }
                if (chatPhoto.Animation != null)
                {
                    ProcessFiles(chatPhoto.Animation);
                }
            }
            else if (target is ChatPhotos chatPhotos)
            {
                foreach (var item in chatPhotos.Photos)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is User user)
            {
                if (user.ProfilePhoto != null)
                {
                    ProcessFiles(user.ProfilePhoto);
                }
            }
            else if (target is UserFullInfo userFullInfo)
            {
                if (userFullInfo.Photo != null)
                {
                    ProcessFiles(userFullInfo.Photo);
                }
            }
            else if (target is ChatInviteLinkInfo chatInviteLinkInfo)
            {
                if (chatInviteLinkInfo.Photo != null)
                {
                    ProcessFiles(chatInviteLinkInfo.Photo);
                }
            }
            else if (target is BasicGroupFullInfo basicGroupFullInfo)
            {
                if (basicGroupFullInfo.Photo != null)
                {
                    ProcessFiles(basicGroupFullInfo.Photo);
                }
            }
            else if (target is SupergroupFullInfo supergroupFullInfo)
            {
                if (supergroupFullInfo.Photo != null)
                {
                    ProcessFiles(supergroupFullInfo.Photo);
                }
            }
            else if (target is Chat chat)
            {
                if (chat.Photo != null)
                {
                    ProcessFiles(chat.Photo);
                }
            }
            else if (target is RichTextIcon richTextIcon)
            {
                if (richTextIcon.Document != null)
                {
                    ProcessFiles(richTextIcon.Document);
                }
            }
            else if (target is RichTextReference richTextReference)
            {
                if (richTextReference.Text != null)
                {
                    ProcessFiles(richTextReference.Text);
                }
            }
            else if (target is RichTextAnchorLink richTextAnchorLink)
            {
                if (richTextAnchorLink.Text != null)
                {
                    ProcessFiles(richTextAnchorLink.Text);
                }
            }
            else if (target is RichTexts richTexts)
            {
                foreach (var item in richTexts.Texts)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockCaption pageBlockCaption)
            {
                if (pageBlockCaption.Text != null)
                {
                    ProcessFiles(pageBlockCaption.Text);
                }
                if (pageBlockCaption.Credit != null)
                {
                    ProcessFiles(pageBlockCaption.Credit);
                }
            }
            else if (target is PageBlockTableCell pageBlockTableCell)
            {
                if (pageBlockTableCell.Text != null)
                {
                    ProcessFiles(pageBlockTableCell.Text);
                }
            }
            else if (target is PageBlockRelatedArticle pageBlockRelatedArticle)
            {
                if (pageBlockRelatedArticle.Photo != null)
                {
                    ProcessFiles(pageBlockRelatedArticle.Photo);
                }
            }
            else if (target is PageBlockTitle pageBlockTitle)
            {
                if (pageBlockTitle.Title != null)
                {
                    ProcessFiles(pageBlockTitle.Title);
                }
            }
            else if (target is PageBlockSubtitle pageBlockSubtitle)
            {
                if (pageBlockSubtitle.Subtitle != null)
                {
                    ProcessFiles(pageBlockSubtitle.Subtitle);
                }
            }
            else if (target is PageBlockAuthorDate pageBlockAuthorDate)
            {
                if (pageBlockAuthorDate.Author != null)
                {
                    ProcessFiles(pageBlockAuthorDate.Author);
                }
            }
            else if (target is PageBlockHeader pageBlockHeader)
            {
                if (pageBlockHeader.Header != null)
                {
                    ProcessFiles(pageBlockHeader.Header);
                }
            }
            else if (target is PageBlockSubheader pageBlockSubheader)
            {
                if (pageBlockSubheader.Subheader != null)
                {
                    ProcessFiles(pageBlockSubheader.Subheader);
                }
            }
            else if (target is PageBlockKicker pageBlockKicker)
            {
                if (pageBlockKicker.Kicker != null)
                {
                    ProcessFiles(pageBlockKicker.Kicker);
                }
            }
            else if (target is PageBlockParagraph pageBlockParagraph)
            {
                if (pageBlockParagraph.Text != null)
                {
                    ProcessFiles(pageBlockParagraph.Text);
                }
            }
            else if (target is PageBlockPreformatted pageBlockPreformatted)
            {
                if (pageBlockPreformatted.Text != null)
                {
                    ProcessFiles(pageBlockPreformatted.Text);
                }
            }
            else if (target is PageBlockFooter pageBlockFooter)
            {
                if (pageBlockFooter.Footer != null)
                {
                    ProcessFiles(pageBlockFooter.Footer);
                }
            }
            else if (target is PageBlockBlockQuote pageBlockBlockQuote)
            {
                if (pageBlockBlockQuote.Text != null)
                {
                    ProcessFiles(pageBlockBlockQuote.Text);
                }
                if (pageBlockBlockQuote.Credit != null)
                {
                    ProcessFiles(pageBlockBlockQuote.Credit);
                }
            }
            else if (target is PageBlockPullQuote pageBlockPullQuote)
            {
                if (pageBlockPullQuote.Text != null)
                {
                    ProcessFiles(pageBlockPullQuote.Text);
                }
                if (pageBlockPullQuote.Credit != null)
                {
                    ProcessFiles(pageBlockPullQuote.Credit);
                }
            }
            else if (target is PageBlockAnimation pageBlockAnimation)
            {
                if (pageBlockAnimation.Animation != null)
                {
                    ProcessFiles(pageBlockAnimation.Animation);
                }
                if (pageBlockAnimation.Caption != null)
                {
                    ProcessFiles(pageBlockAnimation.Caption);
                }
            }
            else if (target is PageBlockAudio pageBlockAudio)
            {
                if (pageBlockAudio.Audio != null)
                {
                    ProcessFiles(pageBlockAudio.Audio);
                }
                if (pageBlockAudio.Caption != null)
                {
                    ProcessFiles(pageBlockAudio.Caption);
                }
            }
            else if (target is PageBlockPhoto pageBlockPhoto)
            {
                if (pageBlockPhoto.Photo != null)
                {
                    ProcessFiles(pageBlockPhoto.Photo);
                }
                if (pageBlockPhoto.Caption != null)
                {
                    ProcessFiles(pageBlockPhoto.Caption);
                }
            }
            else if (target is PageBlockVideo pageBlockVideo)
            {
                if (pageBlockVideo.Video != null)
                {
                    ProcessFiles(pageBlockVideo.Video);
                }
                if (pageBlockVideo.Caption != null)
                {
                    ProcessFiles(pageBlockVideo.Caption);
                }
            }
            else if (target is PageBlockVoiceNote pageBlockVoiceNote)
            {
                if (pageBlockVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(pageBlockVoiceNote.VoiceNote);
                }
                if (pageBlockVoiceNote.Caption != null)
                {
                    ProcessFiles(pageBlockVoiceNote.Caption);
                }
            }
            else if (target is PageBlockCover pageBlockCover)
            {
                if (pageBlockCover.Cover != null)
                {
                    ProcessFiles(pageBlockCover.Cover);
                }
            }
            else if (target is PageBlockEmbedded pageBlockEmbedded)
            {
                if (pageBlockEmbedded.PosterPhoto != null)
                {
                    ProcessFiles(pageBlockEmbedded.PosterPhoto);
                }
                if (pageBlockEmbedded.Caption != null)
                {
                    ProcessFiles(pageBlockEmbedded.Caption);
                }
            }
            else if (target is PageBlockEmbeddedPost pageBlockEmbeddedPost)
            {
                if (pageBlockEmbeddedPost.AuthorPhoto != null)
                {
                    ProcessFiles(pageBlockEmbeddedPost.AuthorPhoto);
                }
                foreach (var item in pageBlockEmbeddedPost.PageBlocks)
                {
                    ProcessFiles(item);
                }
                if (pageBlockEmbeddedPost.Caption != null)
                {
                    ProcessFiles(pageBlockEmbeddedPost.Caption);
                }
            }
            else if (target is PageBlockCollage pageBlockCollage)
            {
                foreach (var item in pageBlockCollage.PageBlocks)
                {
                    ProcessFiles(item);
                }
                if (pageBlockCollage.Caption != null)
                {
                    ProcessFiles(pageBlockCollage.Caption);
                }
            }
            else if (target is PageBlockSlideshow pageBlockSlideshow)
            {
                foreach (var item in pageBlockSlideshow.PageBlocks)
                {
                    ProcessFiles(item);
                }
                if (pageBlockSlideshow.Caption != null)
                {
                    ProcessFiles(pageBlockSlideshow.Caption);
                }
            }
            else if (target is PageBlockChatLink pageBlockChatLink)
            {
                if (pageBlockChatLink.Photo != null)
                {
                    ProcessFiles(pageBlockChatLink.Photo);
                }
            }
            else if (target is PageBlockTable pageBlockTable)
            {
                if (pageBlockTable.Caption != null)
                {
                    ProcessFiles(pageBlockTable.Caption);
                }
            }
            else if (target is PageBlockDetails pageBlockDetails)
            {
                if (pageBlockDetails.Header != null)
                {
                    ProcessFiles(pageBlockDetails.Header);
                }
                foreach (var item in pageBlockDetails.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockRelatedArticles pageBlockRelatedArticles)
            {
                if (pageBlockRelatedArticles.Header != null)
                {
                    ProcessFiles(pageBlockRelatedArticles.Header);
                }
                foreach (var item in pageBlockRelatedArticles.Articles)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockMap pageBlockMap)
            {
                if (pageBlockMap.Caption != null)
                {
                    ProcessFiles(pageBlockMap.Caption);
                }
            }
            else if (target is WebPageInstantView webPageInstantView)
            {
                foreach (var item in webPageInstantView.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is WebPage webPage)
            {
                if (webPage.Photo != null)
                {
                    ProcessFiles(webPage.Photo);
                }
                if (webPage.Animation != null)
                {
                    ProcessFiles(webPage.Animation);
                }
                if (webPage.Audio != null)
                {
                    ProcessFiles(webPage.Audio);
                }
                if (webPage.Document != null)
                {
                    ProcessFiles(webPage.Document);
                }
                if (webPage.Sticker != null)
                {
                    ProcessFiles(webPage.Sticker);
                }
                if (webPage.Video != null)
                {
                    ProcessFiles(webPage.Video);
                }
                if (webPage.VideoNote != null)
                {
                    ProcessFiles(webPage.VideoNote);
                }
                if (webPage.VoiceNote != null)
                {
                    ProcessFiles(webPage.VoiceNote);
                }
            }
            else if (target is PaymentReceipt paymentReceipt)
            {
                if (paymentReceipt.Photo != null)
                {
                    ProcessFiles(paymentReceipt.Photo);
                }
            }
            else if (target is DatedFile datedFile)
            {
                if (datedFile.File != null)
                {
                    datedFile.File = ProcessFile(datedFile.File);
                }
            }
            else if (target is IdentityDocument identityDocument)
            {
                if (identityDocument.FrontSide != null)
                {
                    ProcessFiles(identityDocument.FrontSide);
                }
                if (identityDocument.ReverseSide != null)
                {
                    ProcessFiles(identityDocument.ReverseSide);
                }
                if (identityDocument.Selfie != null)
                {
                    ProcessFiles(identityDocument.Selfie);
                }
                foreach (var item in identityDocument.Translation)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PersonalDocument personalDocument)
            {
                foreach (var item in personalDocument.Files)
                {
                    ProcessFiles(item);
                }
                foreach (var item in personalDocument.Translation)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PassportElementPassport passportElementPassport)
            {
                if (passportElementPassport.Passport != null)
                {
                    ProcessFiles(passportElementPassport.Passport);
                }
            }
            else if (target is PassportElementDriverLicense passportElementDriverLicense)
            {
                if (passportElementDriverLicense.DriverLicense != null)
                {
                    ProcessFiles(passportElementDriverLicense.DriverLicense);
                }
            }
            else if (target is PassportElementIdentityCard passportElementIdentityCard)
            {
                if (passportElementIdentityCard.IdentityCard != null)
                {
                    ProcessFiles(passportElementIdentityCard.IdentityCard);
                }
            }
            else if (target is PassportElementInternalPassport passportElementInternalPassport)
            {
                if (passportElementInternalPassport.InternalPassport != null)
                {
                    ProcessFiles(passportElementInternalPassport.InternalPassport);
                }
            }
            else if (target is PassportElementUtilityBill passportElementUtilityBill)
            {
                if (passportElementUtilityBill.UtilityBill != null)
                {
                    ProcessFiles(passportElementUtilityBill.UtilityBill);
                }
            }
            else if (target is PassportElementBankStatement passportElementBankStatement)
            {
                if (passportElementBankStatement.BankStatement != null)
                {
                    ProcessFiles(passportElementBankStatement.BankStatement);
                }
            }
            else if (target is PassportElementRentalAgreement passportElementRentalAgreement)
            {
                if (passportElementRentalAgreement.RentalAgreement != null)
                {
                    ProcessFiles(passportElementRentalAgreement.RentalAgreement);
                }
            }
            else if (target is PassportElementPassportRegistration passportElementPassportRegistration)
            {
                if (passportElementPassportRegistration.PassportRegistration != null)
                {
                    ProcessFiles(passportElementPassportRegistration.PassportRegistration);
                }
            }
            else if (target is PassportElementTemporaryRegistration passportElementTemporaryRegistration)
            {
                if (passportElementTemporaryRegistration.TemporaryRegistration != null)
                {
                    ProcessFiles(passportElementTemporaryRegistration.TemporaryRegistration);
                }
            }
            else if (target is PassportElements passportElements)
            {
                foreach (var item in passportElements.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PassportElementsWithErrors passportElementsWithErrors)
            {
                foreach (var item in passportElementsWithErrors.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is EncryptedPassportElement encryptedPassportElement)
            {
                if (encryptedPassportElement.FrontSide != null)
                {
                    ProcessFiles(encryptedPassportElement.FrontSide);
                }
                if (encryptedPassportElement.ReverseSide != null)
                {
                    ProcessFiles(encryptedPassportElement.ReverseSide);
                }
                if (encryptedPassportElement.Selfie != null)
                {
                    ProcessFiles(encryptedPassportElement.Selfie);
                }
                foreach (var item in encryptedPassportElement.Translation)
                {
                    ProcessFiles(item);
                }
                foreach (var item in encryptedPassportElement.Files)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageText messageText)
            {
                if (messageText.WebPage != null)
                {
                    ProcessFiles(messageText.WebPage);
                }
            }
            else if (target is MessageAnimation messageAnimation)
            {
                if (messageAnimation.Animation != null)
                {
                    ProcessFiles(messageAnimation.Animation);
                }
            }
            else if (target is MessageAudio messageAudio)
            {
                if (messageAudio.Audio != null)
                {
                    ProcessFiles(messageAudio.Audio);
                }
            }
            else if (target is MessageDocument messageDocument)
            {
                if (messageDocument.Document != null)
                {
                    ProcessFiles(messageDocument.Document);
                }
            }
            else if (target is MessagePhoto messagePhoto)
            {
                if (messagePhoto.Photo != null)
                {
                    ProcessFiles(messagePhoto.Photo);
                }
            }
            else if (target is MessageSticker messageSticker)
            {
                if (messageSticker.Sticker != null)
                {
                    ProcessFiles(messageSticker.Sticker);
                }
            }
            else if (target is MessageVideo messageVideo)
            {
                if (messageVideo.Video != null)
                {
                    ProcessFiles(messageVideo.Video);
                }
            }
            else if (target is MessageVideoNote messageVideoNote)
            {
                if (messageVideoNote.VideoNote != null)
                {
                    ProcessFiles(messageVideoNote.VideoNote);
                }
            }
            else if (target is MessageVoiceNote messageVoiceNote)
            {
                if (messageVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(messageVoiceNote.VoiceNote);
                }
            }
            else if (target is MessageAnimatedEmoji messageAnimatedEmoji)
            {
                if (messageAnimatedEmoji.AnimatedEmoji != null)
                {
                    ProcessFiles(messageAnimatedEmoji.AnimatedEmoji);
                }
            }
            else if (target is MessageGame messageGame)
            {
                if (messageGame.Game != null)
                {
                    ProcessFiles(messageGame.Game);
                }
            }
            else if (target is MessageInvoice messageInvoice)
            {
                if (messageInvoice.Photo != null)
                {
                    ProcessFiles(messageInvoice.Photo);
                }
            }
            else if (target is MessageChatChangePhoto messageChatChangePhoto)
            {
                if (messageChatChangePhoto.Photo != null)
                {
                    ProcessFiles(messageChatChangePhoto.Photo);
                }
            }
            else if (target is MessagePassportDataReceived messagePassportDataReceived)
            {
                foreach (var item in messagePassportDataReceived.Elements)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Stickers stickers)
            {
                foreach (var item in stickers.StickersValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StickerSet stickerSet)
            {
                if (stickerSet.Thumbnail != null)
                {
                    ProcessFiles(stickerSet.Thumbnail);
                }
                foreach (var item in stickerSet.Stickers)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StickerSetInfo stickerSetInfo)
            {
                if (stickerSetInfo.Thumbnail != null)
                {
                    ProcessFiles(stickerSetInfo.Thumbnail);
                }
                foreach (var item in stickerSetInfo.Covers)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is StickerSets stickerSets)
            {
                foreach (var item in stickerSets.Sets)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Animations animations)
            {
                foreach (var item in animations.AnimationsValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is DiceStickersRegular diceStickersRegular)
            {
                if (diceStickersRegular.Sticker != null)
                {
                    ProcessFiles(diceStickersRegular.Sticker);
                }
            }
            else if (target is DiceStickersSlotMachine diceStickersSlotMachine)
            {
                if (diceStickersSlotMachine.Background != null)
                {
                    ProcessFiles(diceStickersSlotMachine.Background);
                }
                if (diceStickersSlotMachine.Lever != null)
                {
                    ProcessFiles(diceStickersSlotMachine.Lever);
                }
                if (diceStickersSlotMachine.LeftReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.LeftReel);
                }
                if (diceStickersSlotMachine.CenterReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.CenterReel);
                }
                if (diceStickersSlotMachine.RightReel != null)
                {
                    ProcessFiles(diceStickersSlotMachine.RightReel);
                }
            }
            else if (target is InlineQueryResultArticle inlineQueryResultArticle)
            {
                if (inlineQueryResultArticle.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultArticle.Thumbnail);
                }
            }
            else if (target is InlineQueryResultContact inlineQueryResultContact)
            {
                if (inlineQueryResultContact.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultContact.Thumbnail);
                }
            }
            else if (target is InlineQueryResultLocation inlineQueryResultLocation)
            {
                if (inlineQueryResultLocation.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultLocation.Thumbnail);
                }
            }
            else if (target is InlineQueryResultVenue inlineQueryResultVenue)
            {
                if (inlineQueryResultVenue.Thumbnail != null)
                {
                    ProcessFiles(inlineQueryResultVenue.Thumbnail);
                }
            }
            else if (target is InlineQueryResultGame inlineQueryResultGame)
            {
                if (inlineQueryResultGame.Game != null)
                {
                    ProcessFiles(inlineQueryResultGame.Game);
                }
            }
            else if (target is InlineQueryResultAnimation inlineQueryResultAnimation)
            {
                if (inlineQueryResultAnimation.Animation != null)
                {
                    ProcessFiles(inlineQueryResultAnimation.Animation);
                }
            }
            else if (target is InlineQueryResultAudio inlineQueryResultAudio)
            {
                if (inlineQueryResultAudio.Audio != null)
                {
                    ProcessFiles(inlineQueryResultAudio.Audio);
                }
            }
            else if (target is InlineQueryResultDocument inlineQueryResultDocument)
            {
                if (inlineQueryResultDocument.Document != null)
                {
                    ProcessFiles(inlineQueryResultDocument.Document);
                }
            }
            else if (target is InlineQueryResultPhoto inlineQueryResultPhoto)
            {
                if (inlineQueryResultPhoto.Photo != null)
                {
                    ProcessFiles(inlineQueryResultPhoto.Photo);
                }
            }
            else if (target is InlineQueryResultSticker inlineQueryResultSticker)
            {
                if (inlineQueryResultSticker.Sticker != null)
                {
                    ProcessFiles(inlineQueryResultSticker.Sticker);
                }
            }
            else if (target is InlineQueryResultVideo inlineQueryResultVideo)
            {
                if (inlineQueryResultVideo.Video != null)
                {
                    ProcessFiles(inlineQueryResultVideo.Video);
                }
            }
            else if (target is InlineQueryResultVoiceNote inlineQueryResultVoiceNote)
            {
                if (inlineQueryResultVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(inlineQueryResultVoiceNote.VoiceNote);
                }
            }
            else if (target is InlineQueryResults inlineQueryResults)
            {
                foreach (var item in inlineQueryResults.Results)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is ChatEventPhotoChanged chatEventPhotoChanged)
            {
                if (chatEventPhotoChanged.OldPhoto != null)
                {
                    ProcessFiles(chatEventPhotoChanged.OldPhoto);
                }
                if (chatEventPhotoChanged.NewPhoto != null)
                {
                    ProcessFiles(chatEventPhotoChanged.NewPhoto);
                }
            }
            else if (target is ChatEvent chatEvent)
            {
                if (chatEvent.Action != null)
                {
                    ProcessFiles(chatEvent.Action);
                }
            }
            else if (target is ChatEvents chatEvents)
            {
                foreach (var item in chatEvents.Events)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is Background background)
            {
                if (background.Document != null)
                {
                    ProcessFiles(background.Document);
                }
            }
            else if (target is Backgrounds backgrounds)
            {
                foreach (var item in backgrounds.BackgroundsValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is ThemeSettings themeSettings)
            {
                if (themeSettings.Background != null)
                {
                    ProcessFiles(themeSettings.Background);
                }
            }
            else if (target is ChatTheme chatTheme)
            {
                if (chatTheme.LightSettings != null)
                {
                    ProcessFiles(chatTheme.LightSettings);
                }
                if (chatTheme.DarkSettings != null)
                {
                    ProcessFiles(chatTheme.DarkSettings);
                }
            }
            else if (target is PushMessageContentAnimation pushMessageContentAnimation)
            {
                if (pushMessageContentAnimation.Animation != null)
                {
                    ProcessFiles(pushMessageContentAnimation.Animation);
                }
            }
            else if (target is PushMessageContentAudio pushMessageContentAudio)
            {
                if (pushMessageContentAudio.Audio != null)
                {
                    ProcessFiles(pushMessageContentAudio.Audio);
                }
            }
            else if (target is PushMessageContentDocument pushMessageContentDocument)
            {
                if (pushMessageContentDocument.Document != null)
                {
                    ProcessFiles(pushMessageContentDocument.Document);
                }
            }
            else if (target is PushMessageContentPhoto pushMessageContentPhoto)
            {
                if (pushMessageContentPhoto.Photo != null)
                {
                    ProcessFiles(pushMessageContentPhoto.Photo);
                }
            }
            else if (target is PushMessageContentSticker pushMessageContentSticker)
            {
                if (pushMessageContentSticker.Sticker != null)
                {
                    ProcessFiles(pushMessageContentSticker.Sticker);
                }
            }
            else if (target is PushMessageContentVideo pushMessageContentVideo)
            {
                if (pushMessageContentVideo.Video != null)
                {
                    ProcessFiles(pushMessageContentVideo.Video);
                }
            }
            else if (target is PushMessageContentVideoNote pushMessageContentVideoNote)
            {
                if (pushMessageContentVideoNote.VideoNote != null)
                {
                    ProcessFiles(pushMessageContentVideoNote.VideoNote);
                }
            }
            else if (target is PushMessageContentVoiceNote pushMessageContentVoiceNote)
            {
                if (pushMessageContentVoiceNote.VoiceNote != null)
                {
                    ProcessFiles(pushMessageContentVoiceNote.VoiceNote);
                }
            }
            else if (target is NotificationTypeNewPushMessage notificationTypeNewPushMessage)
            {
                if (notificationTypeNewPushMessage.Content != null)
                {
                    ProcessFiles(notificationTypeNewPushMessage.Content);
                }
            }
            else if (target is Notification notification)
            {
                if (notification.Type != null)
                {
                    ProcessFiles(notification.Type);
                }
            }
            else if (target is NotificationGroup notificationGroup)
            {
                foreach (var item in notificationGroup.Notifications)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is TMeUrlTypeChatInvite tMeUrlTypeChatInvite)
            {
                if (tMeUrlTypeChatInvite.Info != null)
                {
                    ProcessFiles(tMeUrlTypeChatInvite.Info);
                }
            }
            else if (target is TMeUrl tMeUrl)
            {
                if (tMeUrl.Type != null)
                {
                    ProcessFiles(tMeUrl.Type);
                }
            }
            else if (target is TMeUrls tMeUrls)
            {
                foreach (var item in tMeUrls.Urls)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateMessageContent updateMessageContent)
            {
                if (updateMessageContent.NewContent != null)
                {
                    ProcessFiles(updateMessageContent.NewContent);
                }
            }
            else if (target is UpdateNewChat updateNewChat)
            {
                if (updateNewChat.Chat != null)
                {
                    ProcessFiles(updateNewChat.Chat);
                }
            }
            else if (target is UpdateChatPhoto updateChatPhoto)
            {
                if (updateChatPhoto.Photo != null)
                {
                    ProcessFiles(updateChatPhoto.Photo);
                }
            }
            else if (target is UpdateNotification updateNotification)
            {
                if (updateNotification.Notification != null)
                {
                    ProcessFiles(updateNotification.Notification);
                }
            }
            else if (target is UpdateNotificationGroup updateNotificationGroup)
            {
                foreach (var item in updateNotificationGroup.AddedNotifications)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateActiveNotifications updateActiveNotifications)
            {
                foreach (var item in updateActiveNotifications.Groups)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateUser updateUser)
            {
                if (updateUser.User != null)
                {
                    ProcessFiles(updateUser.User);
                }
            }
            else if (target is UpdateUserFullInfo updateUserFullInfo)
            {
                if (updateUserFullInfo.UserFullInfo != null)
                {
                    ProcessFiles(updateUserFullInfo.UserFullInfo);
                }
            }
            else if (target is UpdateBasicGroupFullInfo updateBasicGroupFullInfo)
            {
                if (updateBasicGroupFullInfo.BasicGroupFullInfo != null)
                {
                    ProcessFiles(updateBasicGroupFullInfo.BasicGroupFullInfo);
                }
            }
            else if (target is UpdateSupergroupFullInfo updateSupergroupFullInfo)
            {
                if (updateSupergroupFullInfo.SupergroupFullInfo != null)
                {
                    ProcessFiles(updateSupergroupFullInfo.SupergroupFullInfo);
                }
            }
            else if (target is UpdateServiceNotification updateServiceNotification)
            {
                if (updateServiceNotification.Content != null)
                {
                    ProcessFiles(updateServiceNotification.Content);
                }
            }
            else if (target is UpdateFile updateFile)
            {
                if (updateFile.File != null)
                {
                    updateFile.File = ProcessFile(updateFile.File);
                }
            }
            else if (target is UpdateStickerSet updateStickerSet)
            {
                if (updateStickerSet.StickerSet != null)
                {
                    ProcessFiles(updateStickerSet.StickerSet);
                }
            }
            else if (target is UpdateTrendingStickerSets updateTrendingStickerSets)
            {
                if (updateTrendingStickerSets.StickerSets != null)
                {
                    ProcessFiles(updateTrendingStickerSets.StickerSets);
                }
            }
            else if (target is UpdateSelectedBackground updateSelectedBackground)
            {
                if (updateSelectedBackground.Background != null)
                {
                    ProcessFiles(updateSelectedBackground.Background);
                }
            }
            else if (target is UpdateChatThemes updateChatThemes)
            {
                foreach (var item in updateChatThemes.ChatThemes)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is UpdateAnimatedEmojiMessageClicked updateAnimatedEmojiMessageClicked)
            {
                if (updateAnimatedEmojiMessageClicked.Sticker != null)
                {
                    ProcessFiles(updateAnimatedEmojiMessageClicked.Sticker);
                }
            }
            else if (target is Message message)
            {
                if (message.Content != null)
                {
                    ProcessFiles(message.Content);
                }
            }
            else if (target is Messages messages)
            {
                foreach (var item in messages.MessagesValue)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is FoundMessages foundMessages)
            {
                foreach (var item in foundMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageCalendarDay messageCalendarDay)
            {
                if (messageCalendarDay.Message != null)
                {
                    ProcessFiles(messageCalendarDay.Message);
                }
            }
            else if (target is MessageCalendar messageCalendar)
            {
                foreach (var item in messageCalendar.Days)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is SponsoredMessage sponsoredMessage)
            {
                if (sponsoredMessage.Content != null)
                {
                    ProcessFiles(sponsoredMessage.Content);
                }
            }
            else if (target is SponsoredMessages sponsoredMessages)
            {
                foreach (var item in sponsoredMessages.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageThreadInfo messageThreadInfo)
            {
                foreach (var item in messageThreadInfo.Messages)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is RichTextBold richTextBold)
            {
                if (richTextBold.Text != null)
                {
                    ProcessFiles(richTextBold.Text);
                }
            }
            else if (target is RichTextItalic richTextItalic)
            {
                if (richTextItalic.Text != null)
                {
                    ProcessFiles(richTextItalic.Text);
                }
            }
            else if (target is RichTextUnderline richTextUnderline)
            {
                if (richTextUnderline.Text != null)
                {
                    ProcessFiles(richTextUnderline.Text);
                }
            }
            else if (target is RichTextStrikethrough richTextStrikethrough)
            {
                if (richTextStrikethrough.Text != null)
                {
                    ProcessFiles(richTextStrikethrough.Text);
                }
            }
            else if (target is RichTextFixed richTextFixed)
            {
                if (richTextFixed.Text != null)
                {
                    ProcessFiles(richTextFixed.Text);
                }
            }
            else if (target is RichTextUrl richTextUrl)
            {
                if (richTextUrl.Text != null)
                {
                    ProcessFiles(richTextUrl.Text);
                }
            }
            else if (target is RichTextEmailAddress richTextEmailAddress)
            {
                if (richTextEmailAddress.Text != null)
                {
                    ProcessFiles(richTextEmailAddress.Text);
                }
            }
            else if (target is RichTextSubscript richTextSubscript)
            {
                if (richTextSubscript.Text != null)
                {
                    ProcessFiles(richTextSubscript.Text);
                }
            }
            else if (target is RichTextSuperscript richTextSuperscript)
            {
                if (richTextSuperscript.Text != null)
                {
                    ProcessFiles(richTextSuperscript.Text);
                }
            }
            else if (target is RichTextMarked richTextMarked)
            {
                if (richTextMarked.Text != null)
                {
                    ProcessFiles(richTextMarked.Text);
                }
            }
            else if (target is RichTextPhoneNumber richTextPhoneNumber)
            {
                if (richTextPhoneNumber.Text != null)
                {
                    ProcessFiles(richTextPhoneNumber.Text);
                }
            }
            else if (target is PageBlockListItem pageBlockListItem)
            {
                foreach (var item in pageBlockListItem.PageBlocks)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is PageBlockList pageBlockList)
            {
                foreach (var item in pageBlockList.Items)
                {
                    ProcessFiles(item);
                }
            }
            else if (target is MessageDice messageDice)
            {
                if (messageDice.InitialState != null)
                {
                    ProcessFiles(messageDice.InitialState);
                }
                if (messageDice.FinalState != null)
                {
                    ProcessFiles(messageDice.FinalState);
                }
            }
            else if (target is ChatEventMessageEdited chatEventMessageEdited)
            {
                if (chatEventMessageEdited.OldMessage != null)
                {
                    ProcessFiles(chatEventMessageEdited.OldMessage);
                }
                if (chatEventMessageEdited.NewMessage != null)
                {
                    ProcessFiles(chatEventMessageEdited.NewMessage);
                }
            }
            else if (target is ChatEventMessageDeleted chatEventMessageDeleted)
            {
                if (chatEventMessageDeleted.Message != null)
                {
                    ProcessFiles(chatEventMessageDeleted.Message);
                }
            }
            else if (target is ChatEventPollStopped chatEventPollStopped)
            {
                if (chatEventPollStopped.Message != null)
                {
                    ProcessFiles(chatEventPollStopped.Message);
                }
            }
            else if (target is ChatEventMessagePinned chatEventMessagePinned)
            {
                if (chatEventMessagePinned.Message != null)
                {
                    ProcessFiles(chatEventMessagePinned.Message);
                }
            }
            else if (target is ChatEventMessageUnpinned chatEventMessageUnpinned)
            {
                if (chatEventMessageUnpinned.Message != null)
                {
                    ProcessFiles(chatEventMessageUnpinned.Message);
                }
            }
            else if (target is NotificationTypeNewMessage notificationTypeNewMessage)
            {
                if (notificationTypeNewMessage.Message != null)
                {
                    ProcessFiles(notificationTypeNewMessage.Message);
                }
            }
            else if (target is MessageLinkInfo messageLinkInfo)
            {
                if (messageLinkInfo.Message != null)
                {
                    ProcessFiles(messageLinkInfo.Message);
                }
            }
            else if (target is UpdateNewMessage updateNewMessage)
            {
                if (updateNewMessage.Message != null)
                {
                    ProcessFiles(updateNewMessage.Message);
                }
            }
            else if (target is UpdateMessageSendSucceeded updateMessageSendSucceeded)
            {
                if (updateMessageSendSucceeded.Message != null)
                {
                    ProcessFiles(updateMessageSendSucceeded.Message);
                }
            }
            else if (target is UpdateMessageSendFailed updateMessageSendFailed)
            {
                if (updateMessageSendFailed.Message != null)
                {
                    ProcessFiles(updateMessageSendFailed.Message);
                }
            }
            else if (target is UpdateChatLastMessage updateChatLastMessage)
            {
                if (updateChatLastMessage.LastMessage != null)
                {
                    ProcessFiles(updateChatLastMessage.LastMessage);
                }
            }
        }
    }
}
