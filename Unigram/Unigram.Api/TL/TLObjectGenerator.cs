using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Telegram.Api.Helpers;
using Telegram.Api.TL.Functions.Messages;

namespace Telegram.Api.TL
{
    class TLObjectGenerator
    {
        private static readonly Dictionary<Type, Func<TLObject>> _baredTypes =
            new Dictionary<Type, Func<TLObject>>
                {
                    {typeof (TLDouble), () => new TLDouble()},
                    {typeof (TLBool), () => new TLBool()},
                    {typeof (TLInt), () => new TLInt()},
                    {typeof (TLLong), () => new TLLong()},
                    {typeof (TLInt128), () => new TLInt128()},
                    {typeof (TLInt256), () => new TLInt256()},
                    {typeof (TLString), () => new TLString()},
                    {typeof (TLNonEncryptedMessage), () => new TLNonEncryptedMessage()},
                    {typeof (TLTransportMessage), () => new TLTransportMessage()},
                    {typeof (TLContainerTransportMessage), () => new TLContainerTransportMessage()},
                };

        private static readonly Dictionary<uint, Func<TLObject>> _clothedTypes =
            new Dictionary<uint, Func<TLObject>>
                {
                    {TLUpdateUserBlocked.Signature, () => new TLUpdateUserBlocked()},
                    {TLUpdateNotifySettings.Signature, () => new TLUpdateNotifySettings()},

                    {TLNotifyPeer.Signature, () => new TLNotifyPeer()},
                    {TLNotifyUsers.Signature, () => new TLNotifyUsers()},
                    {TLNotifyChats.Signature, () => new TLNotifyChats()},
                    {TLNotifyAll.Signature, () => new TLNotifyAll()},

                    {TLDecryptedMessageLayer.Signature, () => new TLDecryptedMessageLayer()},
                    {TLUpdateDCOptions.Signature, () => new TLUpdateDCOptions()},
                    
                    {TLDecryptedMessageMediaAudio.Signature, () => new TLDecryptedMessageMediaAudio()},
                    {TLDecryptedMessageMediaDocument.Signature, () => new TLDecryptedMessageMediaDocument()},

                    {TLInputMediaDocument.Signature, () => new TLInputMediaDocument()},
                    {TLInputMediaUploadedDocument.Signature, () => new TLInputMediaUploadedDocument()},
                    {TLInputMediaUploadedThumbDocument.Signature, () => new TLInputMediaUploadedThumbDocument()},

                    {TLInputMediaAudio.Signature, () => new TLInputMediaAudio()},
                    {TLInputMediaUploadedAudio.Signature, () => new TLInputMediaUploadedAudio()}, 
                
                    {TLInputDocument.Signature, () => new TLInputDocument()},
                    {TLInputDocumentEmpty.Signature, () => new TLInputDocumentEmpty()}, 
                
                    {TLInputAudio.Signature, () => new TLInputAudio()},
                    {TLInputAudioEmpty.Signature, () => new TLInputAudioEmpty()},

                    {TLMessageMediaAudio.Signature, () => new TLMessageMediaAudio()},
                    {TLMessageMediaDocument.Signature, () => new TLMessageMediaDocument()},

                    {TLAudioEmpty.Signature, () => new TLAudioEmpty()},
                    {TLAudio.Signature, () => new TLAudio()},

                    {TLDocumentEmpty.Signature, () => new TLDocumentEmpty()},
                    {TLDocument10.Signature, () => new TLDocument10()},

                    {TLUpdateChatParticipantAdd.Signature, () => new TLUpdateChatParticipantAdd()},
                    {TLUpdateChatParticipantDelete.Signature, () => new TLUpdateChatParticipantDelete()},

                    {TLInputEncryptedFileBigUploaded.Signature, () => new TLInputEncryptedFileBigUploaded()},
                    {TLInputFileBig.Signature, () => new TLInputFileBig()},

                    {TLDecryptedMessageActionSetMessageTTL.Signature, () => new TLDecryptedMessageActionSetMessageTTL()},
                    {TLDecryptedMessageActionReadMessages.Signature, () => new TLDecryptedMessageActionReadMessages()},
                    {TLDecryptedMessageActionDeleteMessages.Signature, () => new TLDecryptedMessageActionDeleteMessages()},
                    {TLDecryptedMessageActionScreenshotMessages.Signature, () => new TLDecryptedMessageActionScreenshotMessages()},
                    {TLDecryptedMessageActionFlushHistory.Signature, () => new TLDecryptedMessageActionFlushHistory()},
                    {TLDecryptedMessageActionNotifyLayer.Signature, () => new TLDecryptedMessageActionNotifyLayer()},

                    {TLDecryptedMessage.Signature, () => new TLDecryptedMessage()},
                    {TLDecryptedMessageService.Signature, () => new TLDecryptedMessageService()},

                    {TLUpdateNewEncryptedMessage.Signature, () => new TLUpdateNewEncryptedMessage()},
                    {TLUpdateEncryptedChatTyping.Signature, () => new TLUpdateEncryptedChatTyping()},
                    {TLUpdateEncryption.Signature, () => new TLUpdateEncryption()},
                    {TLUpdateEncryptedMessagesRead.Signature, () => new TLUpdateEncryptedMessagesRead()},
                    
                    {TLEncryptedChatEmpty.Signature, () => new TLEncryptedChatEmpty()},
                    {TLEncryptedChatWaiting.Signature, () => new TLEncryptedChatWaiting()},
                    {TLEncryptedChatRequested.Signature, () => new TLEncryptedChatRequested()},
                    {TLEncryptedChat.Signature, () => new TLEncryptedChat()},
                    {TLEncryptedChatDiscarded.Signature, () => new TLEncryptedChatDiscarded()},
                    
                    {TLInputEncryptedChat.Signature, () => new TLInputEncryptedChat()},

                    {TLInputEncryptedFileEmpty.Signature, () => new TLInputEncryptedFileEmpty()},
                    {TLInputEncryptedFileUploaded.Signature, () => new TLInputEncryptedFileUploaded()},
                    {TLInputEncryptedFile.Signature, () => new TLInputEncryptedFile()},

                    {TLInputEncryptedFileLocation.Signature, () => new TLInputEncryptedFileLocation()},

                    {TLEncryptedFileEmpty.Signature, () => new TLEncryptedFileEmpty()},
                    {TLEncryptedFile.Signature, () => new TLEncryptedFile()},

                    {TLEncryptedMessage.Signature, () => new TLEncryptedMessage()},
                    {TLEncryptedMessageService.Signature, () => new TLEncryptedMessageService()},

                    {TLDecryptedMessageMediaEmpty.Signature, () => new TLDecryptedMessageMediaEmpty()},
                    {TLDecryptedMessageMediaPhoto.Signature, () => new TLDecryptedMessageMediaPhoto()},
                    {TLDecryptedMessageMediaVideo.Signature, () => new TLDecryptedMessageMediaVideo()},
                    {TLDecryptedMessageMediaGeoPoint.Signature, () => new TLDecryptedMessageMediaGeoPoint()},
                    {TLDecryptedMessageMediaContact.Signature, () => new TLDecryptedMessageMediaContact()},
                    
                    {TLDHConfig.Signature, () => new TLDHConfig()},
                    {TLDHConfigNotModified.Signature, () => new TLDHConfigNotModified()},
                    
                    {TLSentEncryptedMessage.Signature, () => new TLSentEncryptedMessage()},
                    {TLSentEncryptedFile.Signature, () => new TLSentEncryptedFile()},

                    {TLMessageDetailedInfo.Signature, () => new TLMessageDetailedInfo()},
                    {TLMessageNewDetailedInfo.Signature, () => new TLMessageNewDetailedInfo()},
                    {TLMessagesAllInfo.Signature, () => new TLMessagesAllInfo()},

                    {TLUpdateNewMessage.Signature, () => new TLUpdateNewMessage()},
                    {TLUpdateMessageId.Signature, () => new TLUpdateMessageId()},
                    {TLUpdateReadMessages.Signature, () => new TLUpdateReadMessages()},
                    {TLUpdateDeleteMessages.Signature, () => new TLUpdateDeleteMessages()},
                    {TLUpdateRestoreMessages.Signature, () => new TLUpdateRestoreMessages()},
                    {TLUpdateUserTyping.Signature, () => new TLUpdateUserTyping()},
                    {TLUpdateChatUserTyping.Signature, () => new TLUpdateChatUserTyping()},
                    {TLUpdateChatParticipants.Signature, () => new TLUpdateChatParticipants()},
                    {TLUpdateUserStatus.Signature, () => new TLUpdateUserStatus()},
                    {TLUpdateUserName.Signature, () => new TLUpdateUserName()},
                    {TLUpdateUserPhoto.Signature, () => new TLUpdateUserPhoto()},
                    {TLUpdateContactRegistered.Signature, () => new TLUpdateContactRegistered()},
                    {TLUpdateContactLink.Signature, () => new TLUpdateContactLink()},
                    {TLUpdateActivation.Signature, () => new TLUpdateActivation()},
                    {TLUpdateNewAuthorization.Signature, () => new TLUpdateNewAuthorization()},

                    {TLDifferenceEmpty.Signature, () => new TLDifferenceEmpty()},
                    {TLDifference.Signature, () => new TLDifference()},
                    {TLDifferenceSlice.Signature, () => new TLDifferenceSlice()},

                    {TLUpdatesTooLong.Signature, () => new TLUpdatesTooLong()},
                    {TLUpdatesShortMessage.Signature, () => new TLUpdatesShortMessage()},
                    {TLUpdatesShortChatMessage.Signature, () => new TLUpdatesShortChatMessage()},
                    {TLUpdatesShort.Signature, () => new TLUpdatesShort()},
                    {TLUpdatesCombined.Signature, () => new TLUpdatesCombined()},
                    {TLUpdates.Signature, () => new TLUpdates()},

                    {TLFutureSalt.Signature, () => new TLFutureSalt()},
                    {TLFutureSalts.Signature, () => new TLFutureSalts()},

                    {TLGzipPacked.Signature, () => new TLGzipPacked()},
                    {TLState.Signature, () => new TLState()},

                    {TLFileTypeUnknown.Signature, () => new TLFileTypeUnknown()},
                    {TLFileTypeJpeg.Signature, () => new TLFileTypeJpeg()},
                    {TLFileTypeGif.Signature, () => new TLFileTypeGif()},
                    {TLFileTypePng.Signature, () => new TLFileTypePng()},
                    {TLFileTypeMp3.Signature, () => new TLFileTypeMp3()},
                    {TLFileTypeMov.Signature, () => new TLFileTypeMov()},
                    {TLFileTypePartial.Signature, () => new TLFileTypePartial()},
                    {TLFileTypeMp4.Signature, () => new TLFileTypeMp4()},
                    {TLFileTypeWebp.Signature, () => new TLFileTypeWebp()},


                    {TLFile.Signature, () => new TLFile()},
                    
                    {TLInputFileLocation.Signature, () => new TLInputFileLocation()},
                    {TLInputVideoFileLocation.Signature, () => new TLInputVideoFileLocation()},

                    {TLInviteText.Signature, () => new TLInviteText()},

                    {TLDHGenOk.Signature, () => new TLDHGenOk()},
                    {TLDHGenRetry.Signature, () => new TLDHGenRetry()},
                    {TLDHGenFail.Signature, () => new TLDHGenFail()},

                    {TLServerDHInnerData.Signature, () => new TLServerDHInnerData()},
                    {TLServerDHParamsFail.Signature, () => new TLServerDHParamsFail()},
                    {TLServerDHParamsOk.Signature, () => new TLServerDHParamsOk()},
                    {TLPQInnerData.Signature, () => new TLPQInnerData()},
                    {TLResPQ.Signature, () => new TLResPQ()},

                    {TLContactsBlocked.Signature, () => new TLContactsBlocked()},
                    {TLContactsBlockedSlice.Signature, () => new TLContactsBlockedSlice()},
                    {TLContactBlocked.Signature, () => new TLContactBlocked()},
                    
                    {TLImportedContacts.Signature, () => new TLImportedContacts()},
                    {TLImportedContact.Signature, () => new TLImportedContact()},

                    {TLInputContact.Signature, () => new TLInputContact()},

                    {TLContactStatus.Signature, () => new TLContactStatus()},

                    {TLForeignLinkUnknown.Signature, () => new TLForeignLinkUnknown()},
                    {TLForeignLinkRequested.Signature, () => new TLForeignLinkRequested()},
                    {TLForeignLinkMutual.Signature, () => new TLForeignLinkMutual()},

                    {TLMyLinkEmpty.Signature, () => new TLMyLinkEmpty()},
                    {TLMyLinkContact.Signature, () => new TLMyLinkContact()},
                    {TLMyLinkRequested.Signature, () => new TLMyLinkRequested()},

                    {TLLink.Signature, () => new TLLink()},

                    {TLUserFull.Signature, () => new TLUserFull()},
                    
                    {TLPhotos.Signature, () => new TLPhotos()},
                    {TLPhotosSlice.Signature, () => new TLPhotosSlice()},
                    {TLPhotosPhoto.Signature, () => new TLPhotosPhoto()},

                    {TLInputPeerNotifyEventsEmpty.Signature, () => new TLInputPeerNotifyEventsEmpty()},
                    {TLInputPeerNotifyEventsAll.Signature, () => new TLInputPeerNotifyEventsAll()},

                    {TLInputPeerNotifySettings.Signature, () => new TLInputPeerNotifySettings()},

                    {TLInputNotifyPeer.Signature, () => new TLInputNotifyPeer()},
                    {TLInputNotifyUsers.Signature, () => new TLInputNotifyUsers()},
                    {TLInputNotifyChats.Signature, () => new TLInputNotifyChats()},
                    {TLInputNotifyAll.Signature, () => new TLInputNotifyAll()},

                    {TLInputUserEmpty.Signature, () => new TLInputUserEmpty()},
                    {TLInputUserSelf.Signature, () => new TLInputUserSelf()},
                    {TLInputUserContact.Signature, () => new TLInputUserContact()},
                    {TLInputUserForeign.Signature, () => new TLInputUserForeign()},

                    {TLInputPhotoCropAuto.Signature, () => new TLInputPhotoCropAuto()},
                    {TLInputPhotoCrop.Signature, () => new TLInputPhotoCrop()},

                    {TLInputChatPhotoEmpty.Signature, () => new TLInputChatPhotoEmpty()},
                    {TLInputChatUploadedPhoto.Signature, () => new TLInputChatUploadedPhoto()},
                    {TLInputChatPhoto.Signature, () => new TLInputChatPhoto()},

                    {TLMessagesChatFull.Signature, () => new TLMessagesChatFull()},
                    {TLChatFull.Signature, () => new TLChatFull()},

                    {TLChatParticipant.Signature, () => new TLChatParticipant()},

                    {TLChatParticipantsForbidden.Signature, () => new TLChatParticipantsForbidden()},
                    {TLChatParticipants.Signature, () => new TLChatParticipants()},

                    {TLPeerNotifySettingsEmpty.Signature, () => new TLPeerNotifySettingsEmpty()},
                    {TLPeerNotifySettings.Signature, () => new TLPeerNotifySettings()},

                    {TLPeerNotifyEventsEmpty.Signature, () => new TLPeerNotifyEventsEmpty()},
                    {TLPeerNotifyEventsAll.Signature, () => new TLPeerNotifyEventsAll()},

                    {TLChats.Signature, () => new TLChats()},

                    {TLMessages.Signature, () => new TLMessages()},
                    {TLMessagesSlice.Signature, () => new TLMessagesSlice()},

                    {TLExportedAuthorization.Signature, () => new TLExportedAuthorization()},                   

                    {TLInputFile.Signature, () => new TLInputFile()},
                    {TLInputPhotoEmpty.Signature, () => new TLInputPhotoEmpty()},
                    {TLInputPhoto.Signature, () => new TLInputPhoto()},
                    {TLInputGeoPoint.Signature, () => new TLInputGeoPoint()}, 
                    {TLInputGeoPointEmpty.Signature, () => new TLInputGeoPointEmpty()},
                    {TLInputVideo.Signature, () => new TLInputVideo()}, 
                    {TLInputVideoEmpty.Signature, () => new TLInputVideoEmpty()},

                    {TLInputMediaEmpty.Signature, () => new TLInputMediaEmpty()},
                    {TLInputMediaUploadedPhoto.Signature, () => new TLInputMediaUploadedPhoto()},
                    {TLInputMediaPhoto.Signature, () => new TLInputMediaPhoto()},
                    {TLInputMediaGeoPoint.Signature, () => new TLInputMediaGeoPoint()}, 
                    {TLInputMediaContact.Signature, () => new TLInputMediaContact()},
                    {TLInputMediaUploadedVideo.Signature, () => new TLInputMediaUploadedVideo()},
                    {TLInputMediaUploadedThumbVideo.Signature, () => new TLInputMediaUploadedThumbVideo()},
                    {TLInputMediaVideo.Signature, () => new TLInputMediaVideo()},

                    {TLInputMessagesFilterEmpty.Signature, () => new TLInputMessagesFilterEmpty()},
                    {TLInputMessagesFilterPhoto.Signature, () => new TLInputMessagesFilterPhoto()},
                    {TLInputMessagesFilterVideo.Signature, () => new TLInputMessagesFilterVideo()},
                    {TLInputMessagesFilterPhotoVideo.Signature, () => new TLInputMessagesFilterPhotoVideo()},
                    {TLInputMessagesFilterPhotoVideoDocument.Signature, () => new TLInputMessagesFilterPhotoVideoDocument()},
                    {TLInputMessagesFilterDocument.Signature, () => new TLInputMessagesFilterDocument()},
                    {TLInputMessagesFilterAudio.Signature, () => new TLInputMessagesFilterAudio()}, 
                    {TLInputMessagesFilterAudioDocuments.Signature, () => new TLInputMessagesFilterAudioDocuments()}, 
                    {TLInputMessagesFilterUrl.Signature, () => new TLInputMessagesFilterUrl()}, 

                    {TLSentMessageLink.Signature, () => new TLSentMessageLink()},
                    {TLStatedMessage.Signature, () => new TLStatedMessage()},
                    {TLStatedMessageLink.Signature, () => new TLStatedMessageLink()},
                    {TLStatedMessages.Signature, () => new TLStatedMessages()},
                    {TLStatedMessagesLinks.Signature, () => new TLStatedMessagesLinks()},

                    {TLAffectedHistory.Signature, () => new TLAffectedHistory()},

                    {TLNull.Signature, () => new TLNull()},               

                    {TLBool.BoolTrue, () => new TLBool()},
                    {TLBool.BoolFalse, () => new TLBool()},
                    
                    {TLChatEmpty.Signature, () => new TLChatEmpty()},
                    {TLChat.Signature, () => new TLChat()},
                    {TLChatForbidden.Signature, () => new TLChatForbidden()},

                    {TLSentMessage.Signature, () => new TLSentMessage()},

                    {TLMessageEmpty.Signature, () => new TLMessageEmpty()},
                    {TLMessage.Signature, () => new TLMessage()},
                    {TLMessageForwarded.Signature, () => new TLMessageForwarded()},
                    {TLMessageService.Signature, () => new TLMessageService()},                   

                    {TLMessageMediaEmpty.Signature, () => new TLMessageMediaEmpty()},
                    {TLMessageMediaPhoto.Signature, () => new TLMessageMediaPhoto()},
                    {TLMessageMediaVideo.Signature, () => new TLMessageMediaVideo()},
                    {TLMessageMediaGeo.Signature, () => new TLMessageMediaGeo()},
                    {TLMessageMediaContact.Signature, () => new TLMessageMediaContact()},
                    {TLMessageMediaUnsupported.Signature, () => new TLMessageMediaUnsupported()},

                    {TLMessageActionEmpty.Signature, () => new TLMessageActionEmpty()},
                    {TLMessageActionChatCreate.Signature, () => new TLMessageActionChatCreate()},
                    {TLMessageActionChatEditTitle.Signature, () => new TLMessageActionChatEditTitle()},
                    {TLMessageActionChatEditPhoto.Signature, () => new TLMessageActionChatEditPhoto()},
                    {TLMessageActionChatDeletePhoto.Signature, () => new TLMessageActionChatDeletePhoto()},
                    {TLMessageActionChatAddUser.Signature, () => new TLMessageActionChatAddUser()},
                    {TLMessageActionChatDeleteUser.Signature, () => new TLMessageActionChatDeleteUser()},

                    {TLPhoto.Signature, () => new TLPhoto()},
                    {TLPhotoEmpty.Signature, () => new TLPhotoEmpty()},

                    {TLPhotoSize.Signature, () => new TLPhotoSize()},
                    {TLPhotoSizeEmpty.Signature, () => new TLPhotoSizeEmpty()},
                    {TLPhotoCachedSize.Signature, () => new TLPhotoCachedSize()},

                    {TLVideoEmpty.Signature, () => new TLVideoEmpty()},
                    {TLVideo.Signature, () => new TLVideo()},

                    {TLGeoPointEmpty.Signature, () => new TLGeoPointEmpty()},
                    {TLGeoPoint.Signature, () => new TLGeoPoint()},

                    {TLDialog.Signature, () => new TLDialog()},
                    {TLDialogs.Signature, () => new TLDialogs()},
                    {TLDialogsSlice.Signature, () => new TLDialogsSlice()},

                    {TLInputPeerEmpty.Signature, () => new TLInputPeerEmpty()},
                    {TLInputPeerSelf.Signature, () => new TLInputPeerSelf()},
                    {TLInputPeerContact.Signature, () => new TLInputPeerContact()},
                    {TLInputPeerForeign.Signature, () => new TLInputPeerForeign()},
                    {TLInputPeerChat.Signature, () => new TLInputPeerChat()},
                    
                    {TLPeerUser.Signature, () => new TLPeerUser()},
                    {TLPeerChat.Signature, () => new TLPeerChat()},

                    {TLUserStatusEmpty.Signature, () => new TLUserStatusEmpty()},
                    {TLUserStatusOnline.Signature, () => new TLUserStatusOnline()},
                    {TLUserStatusOffline.Signature, () => new TLUserStatusOffline()},

                    {TLChatPhotoEmpty.Signature, () => new TLChatPhotoEmpty()},
                    {TLChatPhoto.Signature, () => new TLChatPhoto()},
                    {TLUserProfilePhotoEmpty.Signature, () => new TLUserProfilePhotoEmpty()},
                    {TLUserProfilePhoto.Signature, () => new TLUserProfilePhoto()},
                    
                    {TLUserEmpty.Signature, () => new TLUserEmpty()},
                    {TLUserSelf.Signature, () => new TLUserSelf()},
                    {TLUserContact.Signature, () => new TLUserContact()},
                    {TLUserRequest.Signature, () => new TLUserRequest()},
                    {TLUserForeign.Signature, () => new TLUserForeign()},
                    {TLUserDeleted.Signature, () => new TLUserDeleted()},

                    {TLSentCode.Signature, () => new TLSentCode()},

                    {TLRPCResult.Signature, () => new TLRPCResult()},

                    {TLRPCError.Signature, () => new TLRPCError()},
                    {TLRPCReqError.Signature, () => new TLRPCReqError()},

                    {TLNewSessionCreated.Signature, () => new TLNewSessionCreated()},

                    {TLNearestDC.Signature, () => new TLNearestDC()},

                    {TLMessagesAcknowledgment.Signature, () => new TLMessagesAcknowledgment()},

                    {TLContainer.Signature, () => new TLContainer()},

                    {TLFileLocationUnavailable.Signature, () => new TLFileLocationUnavailable()},
                    {TLFileLocation.Signature, () => new TLFileLocation()},

                    {TLDCOption.Signature, () => new TLDCOption()},

                    {TLContacts.Signature, () => new TLContacts()},
                    {TLContactsNotModified.Signature, () => new TLContactsNotModified()},

                    {TLContact.Signature, () => new TLContact()},

                    {TLConfig.Signature, () => new TLConfig()},

                    {TLCheckedPhone.Signature, () => new TLCheckedPhone()},

                    {TLBadServerSalt.Signature, () => new TLBadServerSalt()},
                    {TLBadMessageNotification.Signature, () => new TLBadMessageNotification()},

                    {TLAuthorization.Signature, () => new TLAuthorization()},

                    {TLPong.Signature, () => new TLPong()},
                    {TLWallPaper.Signature, () => new TLWallPaper()},
                    {TLWallPaperSolid.Signature, () => new TLWallPaperSolid()},
                    
                    {TLSupport.Signature, () => new TLSupport()},

                    //16 layer
                    {TLSentAppCode.Signature, () => new TLSentAppCode()},

                    //17 layer
                    {TLSendMessageTypingAction.Signature, () => new TLSendMessageTypingAction()},
                    {TLSendMessageCancelAction.Signature, () => new TLSendMessageCancelAction()},
                    {TLSendMessageRecordVideoAction.Signature, () => new TLSendMessageRecordVideoAction()},
                    {TLSendMessageUploadVideoAction.Signature, () => new TLSendMessageUploadVideoAction()},
                    {TLSendMessageRecordAudioAction.Signature, () => new TLSendMessageRecordAudioAction()},
                    {TLSendMessageUploadAudioAction.Signature, () => new TLSendMessageUploadAudioAction()},
                    {TLSendMessageUploadPhotoAction.Signature, () => new TLSendMessageUploadPhotoAction()},
                    {TLSendMessageUploadDocumentAction.Signature, () => new TLSendMessageUploadDocumentAction()},
                    {TLSendMessageGeoLocationAction.Signature, () => new TLSendMessageGeoLocationAction()},
                    {TLSendMessageChooseContactAction.Signature, () => new TLSendMessageChooseContactAction()},                    
                    {TLUpdateUserTyping17.Signature, () => new TLUpdateUserTyping17()},
                    {TLUpdateChatUserTyping17.Signature, () => new TLUpdateChatUserTyping17()},                    
                    {TLMessage17.Signature, () => new TLMessage17()},
                    {TLMessageForwarded17.Signature, () => new TLMessageForwarded17()},
                    {TLMessageService17.Signature, () => new TLMessageService17()},

                    //17 layer encrypted
                    {TLDecryptedMessage17.Signature, () => new TLDecryptedMessage17()},          
                    {TLDecryptedMessageService17.Signature, () => new TLDecryptedMessageService17()},
                    {TLDecryptedMessageMediaAudio17.Signature, () => new TLDecryptedMessageMediaAudio17()},
                    {TLDecryptedMessageMediaVideo17.Signature, () => new TLDecryptedMessageMediaVideo17()},
                    {TLDecryptedMessageLayer17.Signature, () => new TLDecryptedMessageLayer17()},
                    {TLDecryptedMessageActionResend.Signature, () => new TLDecryptedMessageActionResend()},
                    {TLDecryptedMessageActionTyping.Signature, () => new TLDecryptedMessageActionTyping()},

                    //18 layer
                    {TLUpdateServiceNotification.Signature, () => new TLUpdateServiceNotification()},
                    {TLContactFound.Signature, () => new TLContactFound()},
                    {TLContactsFound.Signature, () => new TLContactsFound()},
                    {TLUserSelf18.Signature, () => new TLUserSelf18()},
                    {TLUserContact18.Signature, () => new TLUserContact18()},
                    {TLUserRequest18.Signature, () => new TLUserRequest18()},
                    {TLUserForeign18.Signature, () => new TLUserForeign18()},
                    {TLUserDeleted18.Signature, () => new TLUserDeleted18()},

                    //19 layer
                    {TLUserStatusRecently.Signature, () => new TLUserStatusRecently()},
                    {TLUserStatusLastWeek.Signature, () => new TLUserStatusLastWeek()},
                    {TLUserStatusLastMonth.Signature, () => new TLUserStatusLastMonth()},                    
                    {TLContactStatus19.Signature, () => new TLContactStatus19()},
                    {TLUpdatePrivacy.Signature, () => new TLUpdatePrivacy()},
                    {TLInputPrivacyKeyStatusTimestamp.Signature, () => new TLInputPrivacyKeyStatusTimestamp()},                   
                    {TLPrivacyKeyStatusTimestamp.Signature, () => new TLPrivacyKeyStatusTimestamp()},
                    {TLInputPrivacyValueAllowContacts.Signature, () => new TLInputPrivacyValueAllowContacts()},
                    {TLInputPrivacyValueAllowAll.Signature, () => new TLInputPrivacyValueAllowAll()},
                    {TLInputPrivacyValueAllowUsers.Signature, () => new TLInputPrivacyValueAllowUsers()},
                    {TLInputPrivacyValueDisallowContacts.Signature, () => new TLInputPrivacyValueDisallowContacts()},
                    {TLInputPrivacyValueDisallowAll.Signature, () => new TLInputPrivacyValueDisallowAll()},
                    {TLInputPrivacyValueDisallowUsers.Signature, () => new TLInputPrivacyValueDisallowUsers()},
                    {TLPrivacyValueAllowContacts.Signature, () => new TLPrivacyValueAllowContacts()},
                    {TLPrivacyValueAllowAll.Signature, () => new TLPrivacyValueAllowAll()},
                    {TLPrivacyValueAllowUsers.Signature, () => new TLPrivacyValueAllowUsers()},
                    {TLPrivacyValueDisallowContacts.Signature, () => new TLPrivacyValueDisallowContacts()},
                    {TLPrivacyValueDisallowAll.Signature, () => new TLPrivacyValueDisallowAll()},
                    {TLPrivacyValueDisallowUsers.Signature, () => new TLPrivacyValueDisallowUsers()},                   
                    {TLPrivacyRules.Signature, () => new TLPrivacyRules()},                  
                    {TLAccountDaysTTL.Signature, () => new TLAccountDaysTTL()},

                    //20 layer
                    {TLSentChangePhoneCode.Signature, () => new TLSentChangePhoneCode()},
                    {TLUpdateUserPhone.Signature, () => new TLUpdateUserPhone()},
                    
                    //20 layer encrypted
                    {TLEncryptedChat20.Signature, () => new TLEncryptedChat20()},
                    {TLDecryptedMessageActionRequestKey.Signature, () => new TLDecryptedMessageActionRequestKey()},
                    {TLDecryptedMessageActionAcceptKey.Signature, () => new TLDecryptedMessageActionAcceptKey()},
                    {TLDecryptedMessageActionAbortKey.Signature, () => new TLDecryptedMessageActionAbortKey()},
                    {TLDecryptedMessageActionCommitKey.Signature, () => new TLDecryptedMessageActionCommitKey()},
                    {TLDecryptedMessageActionNoop.Signature, () => new TLDecryptedMessageActionNoop()},
                    
                    //21 layer

                    //22 layer
                    {TLInputMediaUploadedDocument22.Signature, () => new TLInputMediaUploadedDocument22()},
                    {TLInputMediaUploadedThumbDocument22.Signature, () => new TLInputMediaUploadedThumbDocument22()},                 
                    {TLDocument22.Signature, () => new TLDocument22()},                  
                    {TLDocumentAttributeImageSize.Signature, () => new TLDocumentAttributeImageSize()},
                    {TLDocumentAttributeAnimated.Signature, () => new TLDocumentAttributeAnimated()},
                    {TLDocumentAttributeSticker.Signature, () => new TLDocumentAttributeSticker()},
                    {TLDocumentAttributeVideo.Signature, () => new TLDocumentAttributeVideo()},
                    {TLDocumentAttributeAudio.Signature, () => new TLDocumentAttributeAudio()},
                    {TLDocumentAttributeFileName.Signature, () => new TLDocumentAttributeFileName()},                  
                    {TLStickersNotModified.Signature, () => new TLStickersNotModified()},
                    {TLStickers.Signature, () => new TLStickers()},                
                    {TLStickerPack.Signature, () => new TLStickerPack()},                    
                    {TLAllStickersNotModified.Signature, () => new TLAllStickersNotModified()},
                    {TLAllStickers.Signature, () => new TLAllStickers()},

                    //23 layer
                    {TLDisabledFeature.Signature, () => new TLDisabledFeature()},
                    {TLConfig23.Signature, () => new TLConfig23()},
                    
                    //23 layer encrypted
                    {TLDecryptedMessageMediaExternalDocument.Signature, () => new TLDecryptedMessageMediaExternalDocument()},

                    //24 layer
                    {TLUpdateNewMessage24.Signature, () => new TLUpdateNewMessage24()},
                    {TLUpdateReadMessages24.Signature, () => new TLUpdateReadMessages24()},
                    {TLUpdateDeleteMessages24.Signature, () => new TLUpdateDeleteMessages24()},
                    {TLUpdatesShortMessage24.Signature, () => new TLUpdatesShortMessage24()},
                    {TLUpdatesShortChatMessage24.Signature, () => new TLUpdatesShortChatMessage24()},                    
                    {TLUpdateReadHistoryInbox.Signature, () => new TLUpdateReadHistoryInbox()},
                    {TLUpdateReadHistoryOutbox.Signature, () => new TLUpdateReadHistoryOutbox()},                  
                    {TLDialog24.Signature, () => new TLDialog24()},                    
                    {TLStatedMessages24.Signature, () => new TLStatedMessages24()},
                    {TLStatedMessagesLinks24.Signature, () => new TLStatedMessagesLinks24()},                    
                    {TLStatedMessage24.Signature, () => new TLStatedMessage24()},
                    {TLStatedMessageLink24.Signature, () => new TLStatedMessageLink24()},                    
                    {TLSentMessage24.Signature, () => new TLSentMessage24()},
                    {TLSentMessageLink24.Signature, () => new TLSentMessageLink24()},                   
                    {TLAffectedMessages.Signature, () => new TLAffectedMessages()},
                    {TLAffectedHistory24.Signature, () => new TLAffectedHistory24()},       
                    {TLMessageMediaUnsupported24.Signature, () => new TLMessageMediaUnsupported24()},                 
                    {TLChats24.Signature, () => new TLChats24()},                
                    {TLUserSelf24.Signature, () => new TLUserSelf24()},                 
                    {TLCheckedPhone24.Signature, () => new TLCheckedPhone24()},                   
                    {TLContactLinkUnknown.Signature, () => new TLContactLinkUnknown()},
                    {TLContactLinkNone.Signature, () => new TLContactLinkNone()},
                    {TLContactLinkHasPhone.Signature, () => new TLContactLinkHasPhone()},
                    {TLContactLinkContact.Signature, () => new TLContactLinkContact()},                   
                    {TLUpdateContactLink24.Signature, () => new TLUpdateContactLink24()},
                    {TLLink24.Signature, () => new TLLink24()},
                    {TLConfig24.Signature, () => new TLConfig24()},

                    //25 layer
                    {TLMessage25.Signature, () => new TLMessage25()},
                    {TLDocumentAttributeSticker25.Signature, () => new TLDocumentAttributeSticker25()},
                    {TLUpdatesShortMessage25.Signature, () => new TLUpdatesShortMessage25()},
                    {TLUpdatesShortChatMessage25.Signature, () => new TLUpdatesShortChatMessage25()},

                    //26 layer
                    {TLSentMessage26.Signature, () => new TLSentMessage26()},
                    {TLSentMessageLink26.Signature, () => new TLSentMessageLink26()},
                    {TLConfig26.Signature, () => new TLConfig26()},
                    {TLUpdateWebPage.Signature, () => new TLUpdateWebPage()},
                    {TLWebPageEmpty.Signature, () => new TLWebPageEmpty()},
                    {TLWebPagePending.Signature, () => new TLWebPagePending()},
                    {TLWebPage.Signature, () => new TLWebPage()},
                    {TLMessageMediaWebPage.Signature, () => new TLMessageMediaWebPage()},
                    {TLAccountAuthorization.Signature, () => new TLAccountAuthorization()},
                    {TLAccountAuthorizations.Signature, () => new TLAccountAuthorizations()},

                    //27 layer
                    {TLPassword.Signature, () => new TLPassword()},
                    {TLNoPassword.Signature, () => new TLNoPassword()},
                    {TLPasswordSettings.Signature, () => new TLPasswordSettings()},
                    {TLPasswordInputSettings.Signature, () => new TLPasswordInputSettings()},
                    {TLPasswordRecovery.Signature, () => new TLPasswordRecovery()},

                    //layer 28
                    {TLInputMediaUploadedPhoto28.Signature, () => new TLInputMediaUploadedPhoto28()},
                    {TLInputMediaPhoto28.Signature, () => new TLInputMediaPhoto28()},
                    {TLInputMediaUploadedVideo28.Signature, () => new TLInputMediaUploadedVideo28()},
                    {TLInputMediaUploadedThumbVideo28.Signature, () => new TLInputMediaUploadedThumbVideo28()},
                    {TLInputMediaVideo28.Signature, () => new TLInputMediaVideo28()},
                    {TLSendMessageUploadVideoAction28.Signature, () => new TLSendMessageUploadVideoAction28()},
                    {TLSendMessageUploadAudioAction28.Signature, () => new TLSendMessageUploadAudioAction28()},
                    {TLSendMessageUploadPhotoAction28.Signature, () => new TLSendMessageUploadPhotoAction28()},
                    {TLSendMessageUploadDocumentAction28.Signature, () => new TLSendMessageUploadDocumentAction28()},
                    {TLInputMediaVenue.Signature, () => new TLInputMediaVenue()},
                    {TLMessageMediaVenue.Signature, () => new TLMessageMediaVenue()},
                    {TLChatInviteEmpty.Signature, () => new TLChatInviteEmpty()},
                    {TLChatInviteExported.Signature, () => new TLChatInviteExported()},
                    {TLChatInviteAlready.Signature, () => new TLChatInviteAlready()},
                    {TLChatInvite.Signature, () => new TLChatInvite()},
                    {TLUpdateReadMessagesContents.Signature, () => new TLUpdateReadMessagesContents()},
                    {TLConfig28.Signature, () => new TLConfig28()},
                    {TLChatFull28.Signature, () => new TLChatFull28()},
                    {TLReceivedNotifyMessage.Signature, () => new TLReceivedNotifyMessage()},
                    {TLMessageActionChatJoinedByLink.Signature, () => new TLMessageActionChatJoinedByLink()},
                    {TLPhoto28.Signature, () => new TLPhoto28()},
                    {TLVideo28.Signature, () => new TLVideo28()},
                    {TLMessageMediaPhoto28.Signature, () => new TLMessageMediaPhoto28()},
                    {TLMessageMediaVideo28.Signature, () => new TLMessageMediaVideo28()},

                    //layer 29
                    {TLDocumentAttributeSticker29.Signature, () => new TLDocumentAttributeSticker29()},
                    {TLAllStickers29.Signature, () => new TLAllStickers29()},
                    {TLInputStickerSetEmpty.Signature, () => new TLInputStickerSetEmpty()},
                    {TLInputStickerSetId.Signature, () => new TLInputStickerSetId()},
                    {TLInputStickerSetShortName.Signature, () => new TLInputStickerSetShortName()},
                    {TLStickerSet.Signature, () => new TLStickerSet()},
                    {TLMessagesStickerSet.Signature, () => new TLMessagesStickerSet()},
                    
                    //layer 30
                    {TLDCOption30.Signature, () => new TLDCOption30()},

                    //layer 31
                    {TLChatFull31.Signature, () => new TLChatFull31()},
                    {TLMessage31.Signature, () => new TLMessage31()},
                    {TLAuthorization31.Signature, () => new TLAuthorization31()},
                    {TLUserFull31.Signature, () => new TLUserFull31()},
                    {TLUser.Signature, () => new TLUser()},
                    {TLBotCommand.Signature, () => new TLBotCommand()},
                    {TLBotInfoEmpty.Signature, () => new TLBotInfoEmpty()},
                    {TLBotInfo.Signature, () => new TLBotInfo()},
                    {TLKeyboardButton.Signature, () => new TLKeyboardButton()},
                    {TLKeyboardButtonRow.Signature, () => new TLKeyboardButtonRow()},
                    {TLReplyKeyboardMarkup.Signature, () => new TLReplyKeyboardMarkup()},
                    {TLReplyKeyboardHide.Signature, () => new TLReplyKeyboardHide()},
                    {TLReplyKeyboardForceReply.Signature, () => new TLReplyKeyboardForceReply()},

                    //layer 32
                    {TLAllStickers32.Signature, () => new TLAllStickers32()},
                    {TLStickerSet32.Signature, () => new TLStickerSet32()},
                    {TLDocumentAttributeAudio32.Signature, () => new TLDocumentAttributeAudio32()},

                    //layer 33
                    {TLInputPeerUser.Signature, () => new TLInputPeerUser()},
                    {TLInputUser.Signature, () => new TLInputUser()},
                    {TLPhoto33.Signature, () => new TLPhoto33()},
                    {TLVideo33.Signature, () => new TLVideo33()},
                    {TLAudio33.Signature, () => new TLAudio33()},
                    {TLAppChangelogEmpty.Signature, () => new TLAppChangelogEmpty()},
                    {TLAppChangelog.Signature, () => new TLAppChangelog()},

                    //layer 34
                    {TLMessageEntityUnknown.Signature, () => new TLMessageEntityUnknown()},
                    {TLMessageEntityMention.Signature, () => new TLMessageEntityMention()},
                    {TLMessageEntityHashtag.Signature, () => new TLMessageEntityHashtag()},
                    {TLMessageEntityBotCommand.Signature, () => new TLMessageEntityBotCommand()},
                    {TLMessageEntityUrl.Signature, () => new TLMessageEntityUrl()},
                    {TLMessageEntityEmail.Signature, () => new TLMessageEntityEmail()},
                    {TLMessageEntityBold.Signature, () => new TLMessageEntityBold()},
                    {TLMessageEntityItalic.Signature, () => new TLMessageEntityItalic()},
                    {TLMessageEntityCode.Signature, () => new TLMessageEntityCode()},
                    {TLMessageEntityPre.Signature, () => new TLMessageEntityPre()},
                    {TLMessageEntityTextUrl.Signature, () => new TLMessageEntityTextUrl()},
                    {TLMessage34.Signature, () => new TLMessage34()},
                    {TLSentMessage34.Signature, () => new TLSentMessage34()},
                    {TLUpdatesShortMessage34.Signature, () => new TLUpdatesShortMessage34()},
                    {TLUpdatesShortChatMessage34.Signature, () => new TLUpdatesShortChatMessage34()},

                    //layer 35
                    {TLWebPage35.Signature, () => new TLWebPage35()},

                    //layer 36
                    {TLInputMediaUploadedVideo36.Signature, () => new TLInputMediaUploadedVideo36()},
                    {TLInputMediaUploadedThumbVideo36.Signature, () => new TLInputMediaUploadedThumbVideo36()},
                    {TLMessage36.Signature, () => new TLMessage36()},
                    {TLUpdatesShortSentMessage.Signature, () => new TLUpdatesShortSentMessage()},

                    //layer 37
                    {TLChatParticipantsForbidden37.Signature, () => new TLChatParticipantsForbidden37()},
                    {TLUpdateChatParticipantAdd37.Signature, () => new TLUpdateChatParticipantAdd37()},
                    {TLUpdateWebPage37.Signature, () => new TLUpdateWebPage37()},

                    //layer 40
                    {TLInputPeerChannel.Signature, () => new TLInputPeerChannel()},
                    {TLPeerChannel.Signature, () => new TLPeerChannel()},
                    {TLChat40.Signature, () => new TLChat40()},
                    {TLChatForbidden40.Signature, () => new TLChatForbidden40()},
                    {TLChannel.Signature, () => new TLChannel()},
                    {TLChannelForbidden.Signature, () => new TLChannelForbidden()},
                    {TLChannelFull.Signature, () => new TLChannelFull()},
                    {TLChannelParticipants.Signature, () => new TLChannelParticipants()},
                    {TLMessage40.Signature, () => new TLMessage40()},
                    {TLMessageService40.Signature, () => new TLMessageService40()},
                    {TLMessageActionChannelCreate.Signature, () => new TLMessageActionChannelCreate()},
                    {TLDialogChannel.Signature, () => new TLDialogChannel()},
                    {TLChannelMessages.Signature, () => new TLChannelMessages()},
                    {TLUpdateChannelTooLong.Signature, () => new TLUpdateChannelTooLong()},
                    {TLUpdateChannel.Signature, () => new TLUpdateChannel()},
                    {TLUpdateChannelGroup.Signature, () => new TLUpdateChannelGroup()},
                    {TLUpdateNewChannelMessage.Signature, () => new TLUpdateNewChannelMessage()},
                    {TLUpdateReadChannelInbox.Signature, () => new TLUpdateReadChannelInbox()},
                    {TLUpdateDeleteChannelMessages.Signature, () => new TLUpdateDeleteChannelMessages()},
                    {TLUpdateChannelMessageViews.Signature, () => new TLUpdateChannelMessageViews()},                
                    {TLUpdatesShortMessage40.Signature, () => new TLUpdatesShortMessage40()},
                    {TLUpdatesShortChatMessage40.Signature, () => new TLUpdatesShortChatMessage40()},
                    {TLContactsFound40.Signature, () => new TLContactsFound40()},
                    //{TLInputChatEmpty.Signature, () => new TLInputChatEmpty()},     // delete
                    //{TLInputChat.Signature, () => new TLInputChat()},   // delete
                    {TLInputChannel.Signature, () => new TLInputChannel()}, 
                    {TLInputChannelEmpty.Signature, () => new TLInputChannelEmpty()},
                    {TLMessageRange.Signature, () => new TLMessageRange()},
                    {TLMessageGroup.Signature, () => new TLMessageGroup()},
                    {TLChannelDifferenceEmpty.Signature, () => new TLChannelDifferenceEmpty()},
                    {TLChannelDifferenceTooLong.Signature, () => new TLChannelDifferenceTooLong()},
                    {TLChannelDifference.Signature, () => new TLChannelDifference()},
                    {TLChannelMessagesFilterEmpty.Signature, () => new TLChannelMessagesFilterEmpty()},
                    {TLChannelMessagesFilter.Signature, () => new TLChannelMessagesFilter()},
                    {TLChannelMessagesFilterCollapsed.Signature, () => new TLChannelMessagesFilterCollapsed()},
                    {TLResolvedPeer.Signature, () => new TLResolvedPeer()},
                    {TLChannelParticipant.Signature, () => new TLChannelParticipant()},
                    {TLChannelParticipantSelf.Signature, () => new TLChannelParticipantSelf()},
                    {TLChannelParticipantModerator.Signature, () => new TLChannelParticipantModerator()},
                    {TLChannelParticipantEditor.Signature, () => new TLChannelParticipantEditor()},
                    {TLChannelParticipantKicked.Signature, () => new TLChannelParticipantKicked()},
                    {TLChannelParticipantCreator.Signature, () => new TLChannelParticipantCreator()},
                    {TLChannelParticipantsRecent.Signature, () => new TLChannelParticipantsRecent()},
                    {TLChannelParticipantsAdmins.Signature, () => new TLChannelParticipantsAdmins()},
                    {TLChannelParticipantsKicked.Signature, () => new TLChannelParticipantsKicked()},        
                    {TLChannelRoleEmpty.Signature, () => new TLChannelRoleEmpty()},
                    {TLChannelRoleModerator.Signature, () => new TLChannelRoleModerator()},
                    {TLChannelRoleEditor.Signature, () => new TLChannelRoleEditor()},
                    {TLChannelsChannelParticipants.Signature, () => new TLChannelsChannelParticipants()},
                    {TLChannelsChannelParticipant.Signature, () => new TLChannelsChannelParticipant()},
                    {TLChatInvite40.Signature, () => new TLChatInvite40()},
                    
                    {TLChatParticipantCreator.Signature, () => new TLChatParticipantCreator()},
                    {TLChatParticipantAdmin.Signature, () => new TLChatParticipantAdmin()},
                    {TLChatParticipants40.Signature, () => new TLChatParticipants40()},
                    {TLUpdateChatAdmins.Signature, () => new TLUpdateChatAdmins()},
                    {TLUpdateChatParticipantAdmin.Signature, () => new TLUpdateChatParticipantAdmin()},

                    // layer 41
                    {TLConfig41.Signature, () => new TLConfig41()},
                    
                    {TLMessageActionChatMigrateTo.Signature, () => new TLMessageActionChatMigrateTo()},
                    {TLMessageActionChatDeactivate.Signature, () => new TLMessageActionChatDeactivate()},
                    {TLMessageActionChatActivate.Signature, () => new TLMessageActionChatActivate()},
                    {TLMessageActionChannelMigrateFrom.Signature, () => new TLMessageActionChannelMigrateFrom()},

                    {TLChannelParticipantsBots.Signature, () => new TLChannelParticipantsBots()},
                    {TLChat41.Signature, () => new TLChat41()},
                    {TLChannelFull41.Signature, () => new TLChannelFull41()},

                    // functions
                    {TLSendMessage.Signature, () => new TLSendMessage()},
                    {TLSendMedia.Signature, () => new TLSendMedia()},
                    {TLForwardMessage.Signature, () => new TLForwardMessage()},
                    {TLForwardMessages.Signature, () => new TLForwardMessages()},
                    {TLStartBot.Signature, () => new TLStartBot()},
                    {TLReadHistory.Signature, () => new TLReadHistory()},
                    {TLReadMessageContents.Signature, () => new TLReadMessageContents()},
                    {TLInitConnection.Signature, () => new TLInitConnection()},
                    
                    {TLSendEncrypted.Signature, () => new TLSendEncrypted()},
                    {TLSendEncryptedFile.Signature, () => new TLSendEncryptedFile()},
                    {TLSendEncryptedService.Signature, () => new TLSendEncryptedService()},
                    {TLReadEncryptedHistory.Signature, () => new TLReadEncryptedHistory()},

                    // additional sigantures
                    {TLEncryptedDialog.Signature, () => new TLEncryptedDialog()},                   
                    {TLUserExtendedInfo.Signature, () => new TLUserExtendedInfo()},                   
                    {TLDecryptedMessageActionEmpty.Signature, () => new TLDecryptedMessageActionEmpty()},
                    {TLPeerEncryptedChat.Signature, () => new TLPeerEncryptedChat()},
                    {TLBroadcastChat.Signature, () => new TLBroadcastChat()},
                    {TLPeerBroadcast.Signature, () => new TLPeerBroadcast()},
                    {TLBroadcastDialog.Signature, () => new TLBroadcastDialog()},
                    {TLInputPeerBroadcast.Signature, () => new TLInputPeerBroadcast()},
                    {TLServerFile.Signature, () => new TLServerFile()},
                    {TLEncryptedChat17.Signature, () => new TLEncryptedChat17()},
                    {TLMessageActionUnreadMessages.Signature, () => new TLMessageActionUnreadMessages()},
                    {TLMessagesContainter.Signature, () => new TLMessagesContainter()},
                    {TLHashtagItem.Signature, () => new TLHashtagItem()},
                    {TLMessageActionContactRegistered.Signature, () => new TLMessageActionContactRegistered()},
                    {TLPasscodeParams.Signature, () => new TLPasscodeParams()},
                    {TLRecentlyUsedSticker.Signature, () => new TLRecentlyUsedSticker()},
                    {TLActionInfo.Signature, () => new TLActionInfo()},
                    {TLResultInfo.Signature, () => new TLResultInfo()},
                    {TLMessageActionMessageGroup.Signature, () => new TLMessageActionMessageGroup()},
                    {TLMessageActionChannelJoined.Signature, () => new TLMessageActionChannelJoined()}
                };

        public static TimeSpan ElapsedClothedTypes;

        public static TimeSpan ElapsedBaredTypes;

        public static TimeSpan ElapsedVectorTypes;

        public static T GetObject<T>(byte[] bytes, int position) where T : TLObject
        {

            //var stopwatch = Stopwatch.StartNew();

            // bared types


            var stopwatch2 = Stopwatch.StartNew();
            try
            {

                if (_baredTypes.ContainsKey(typeof (T)))
                {
                    return (T) _baredTypes[typeof (T)].Invoke();
                }
            }
            finally
            {
                ElapsedBaredTypes += stopwatch2.Elapsed;
            }

            var stopwatch = Stopwatch.StartNew();
            uint signature;
            try
            {
                // clothed types
                //var signatureBytes = bytes.SubArray(position, 4);
                //Array.Reverse(signatureBytes);
                signature = BitConverter.ToUInt32(bytes, position);
                Func<TLObject> getInstance;


                // exact matching
                if (_clothedTypes.TryGetValue(signature, out getInstance))
                {
                    return (T)getInstance.Invoke();
                }


                //// matching with removed leading 0
                //while (signature.StartsWith("0"))
                //{
                //    signature = signature.Remove(0, 1);
                //    if (_clothedTypes.TryGetValue("#" + signature, out getInstance))
                //    {
                //        return (T)getInstance.Invoke();
                //    }
                //}
            }
            finally
            {
                ElapsedClothedTypes += stopwatch.Elapsed;
            }




            var stopwatch3 = Stopwatch.StartNew();
            //throw new Exception("Signature exception");
            try
            {
                // TLVector
                if (bytes.StartsWith(position, TLConstructors.TLVector))
                {
                    

                    //TODO: remove workaround for TLRPCRESULT: TLVECTOR<TLINT>
                    if (typeof (T) == typeof (TLObject))
                    {
                        Func<TLObject> getObject;
                        var internalSignature = BitConverter.ToUInt32(bytes, position + 8);
                        var length = BitConverter.ToInt32(bytes, position + 4);
                        if (length > 0)
                        {
                            if (_clothedTypes.TryGetValue(internalSignature, out getObject))
                            {
                                var obj = getObject.Invoke();
                                if (obj is TLUserBase)
                                {
                                    return (T)Activator.CreateInstance(typeof(TLVector<TLUserBase>));
                                }
                            }
                        }


                        if (bytes.StartsWith(position + 8, TLConstructors.TLContactStatus19))
                        {
                            return (T)Activator.CreateInstance(typeof(TLVector<TLContactStatusBase>));
                        }
                        else if (bytes.StartsWith(position + 8, TLConstructors.TLContactStatus))
                        {
                            return (T)Activator.CreateInstance(typeof(TLVector<TLContactStatusBase>));
                        }
                        else if (bytes.StartsWith(position + 8, TLConstructors.TLWallPaper)
                            || bytes.StartsWith(position + 8, TLConstructors.TLWallPaperSolid))
                        {
                            return (T)Activator.CreateInstance(typeof(TLVector<TLWallPaperBase>));
                        }


                        TLUtils.WriteLine("TLVecto<TLInt>  hack ", LogSeverity.Error);
                        return (T) Activator.CreateInstance(typeof(TLVector<TLInt>));
                    }
                    else
                    {
                        return (T) Activator.CreateInstance(typeof (T));
                    }
                }

            }
            finally
            {
                ElapsedVectorTypes += stopwatch3.Elapsed;
            }

            var signatureBytes = BitConverter.GetBytes(signature);
            Array.Reverse(signatureBytes);
            var signatureString = BitConverter.ToString(signatureBytes).Replace("-", string.Empty).ToLowerInvariant();
            if (typeof (T) == typeof (TLObject))
            {
                var error = string.Format("  ERROR TLObjectGenerator: Cannot find signature #{0} ({1})", signatureString, signature);
                TLUtils.WriteLine(error, LogSeverity.Error);
                Execute.ShowDebugMessage(error);
            }
            else
            {
                var error = string.Format("  ERROR TLObjectGenerator: Incorrect signature #{0} ({1}) for type {2}", signatureString, signature, typeof(T));
                TLUtils.WriteLine(error, LogSeverity.Error);
                Execute.ShowDebugMessage(error);
            }

            return null;
        }

        public static T GetNullableObject<T>(Stream input) where T : TLObject
        {
            // clothed types
            var signatureBytes = new byte[4];
            input.Read(signatureBytes, 0, 4);
            uint signature = BitConverter.ToUInt32(signatureBytes, 0);

            if (signature == TLNull.Signature) return null;

            input.Position = input.Position - 4;
            return GetObject<T>(input);
        }

        public static T GetObject<T>(Stream input) where T : TLObject
        {

            //var stopwatch = Stopwatch.StartNew();

            // bared types


            var stopwatch2 = Stopwatch.StartNew();
            try
            {

                if (_baredTypes.ContainsKey(typeof(T)))
                {
                    return (T)_baredTypes[typeof(T)].Invoke();
                }
            }
            finally
            {
                ElapsedBaredTypes += stopwatch2.Elapsed;
            }

            var stopwatch = Stopwatch.StartNew();
            uint signature;
            try
            {
                // clothed types
                var signatureBytes = new byte[4];
                input.Read(signatureBytes, 0, 4);
                signature = BitConverter.ToUInt32(signatureBytes, 0);
                Func<TLObject> getInstance;


                // exact matching
                if (_clothedTypes.TryGetValue(signature, out getInstance))
                {
                    return (T)getInstance.Invoke();
                }
            }
            finally
            {

                ElapsedClothedTypes += stopwatch.Elapsed;
            }




            var stopwatch3 = Stopwatch.StartNew();
            //throw new Exception("Signature exception");
            try
            {
                // TLVector
                if (signature == TLConstructors.TLVector)
                {
                    //TODO: remove workaround for TLRPCRESULT: TLVECTOR<TLINT>
                    if (typeof(T) == typeof(TLObject))
                    {
                        TLUtils.WriteLine("TLVecto<TLInt>  hack ", LogSeverity.Error);
                        return (T)Activator.CreateInstance(typeof(TLVector<TLInt>));
                    }
                    else
                    {
                        return (T)Activator.CreateInstance(typeof(T));
                    }
                }

            }
            finally
            {
                ElapsedVectorTypes += stopwatch3.Elapsed;
            }

            var bytes = BitConverter.GetBytes(signature);
            Array.Reverse(bytes);
            var signatureString = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            if (typeof(T) == typeof(TLObject))
            {
                var error = string.Format("  ERROR TLObjectGenerator FromStream: Cannot find signature #{0} ({1})", signatureString, signature);
                TLUtils.WriteLine(error, LogSeverity.Error);
                Execute.ShowDebugMessage(error);
            }
            else
            {
                var error = string.Format("  ERROR TLObjectGenerator FromStream: Incorrect signature #{0} ({1}) for type {2}", signatureString, signature, typeof(T));
                TLUtils.WriteLine(error, LogSeverity.Error);
                Execute.ShowDebugMessage(error);
            }

            return null;
        }
    }
}
