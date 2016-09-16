using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Auth;
using Telegram.Api.TL.Functions.Contacts;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public interface IMTProtoService
    {
        event EventHandler<TransportCheckedEventArgs> TransportChecked;

        string Message { get; }
        void SetMessageOnTime(double seconds, string message);

        ITransport GetActiveTransport();
        WindowsPhone.Tuple<int, int, int> GetCurrentPacketInfo();
        string GetTransportInfo();

        string Country { get; }
        event EventHandler<CountryEventArgs> GotUserCountry;

        // To remove multiple UpdateStatusAsync calls, it's prefer to invoke this method instead
        void RaiseSendStatus(SendStatusEventArgs e);

        TLInt CurrentUserId { get; set; }

        IList<HistoryItem> History { get; }

        void ClearHistory(string caption, bool createNewSession, Exception e = null);

        long ClientTicksDelta { get; }

        /// <summary>
        /// Indicates that service has authKey
        /// </summary>
        //bool IsInitialized { get; }
        event EventHandler Initialized;
        event EventHandler<AuthorizationRequiredEventArgs> AuthorizationRequired;
        event EventHandler CheckDeviceLocked;

        void SaveConfig();
        TLConfig LoadConfig();

        void GetStateAsync(Action<TLState> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceAsync(TLInt pts, TLInt date, TLInt qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceWithoutUpdatesAsync(TLInt pts, TLInt date, TLInt qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        void RegisterDeviceAsync(TLInt tokenType, TLString token, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void UnregisterDeviceAsync(TLInt tokenType, TLString token, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        

        void MessageAcknowledgments(TLVector<TLLong> ids);

        // auth
        void SendCodeAsync(TLString phoneNumber, TLString currentNumber, Action<TLSentCodeBase> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null);
        void ResendCodeAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);
        void CancelCodeAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SignInAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void CancelSignInAsync();
        void LogOutAsync(Action callback);
        void LogOutAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void LogOutTransportsAsync(Action callback, Action<List<TLRPCError>> faultCallback = null);
        void SignUpAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, TLString firstName, TLString lastName, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void SendCallAsync(TLString phoneNumber, TLString phoneCodeHash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
       
        void SearchAsync(TLInputPeerBase peer, TLString query, TLInputMessagesFilterBase filter, TLInt minDate, TLInt maxDate, TLInt offset, TLInt maxId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDialogsAsync(Stopwatch timer, TLInt offsetDate, TLInt offsetId, TLInputPeerBase offsetPeer, TLInt limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetHistoryAsync(Stopwatch timer, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, TLInt offset, TLInt maxId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, TLInt offset, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void DeleteContactAsync(TLInputUserBase id, Action<TLLinkBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLInputPeerBase peer, TLInt maxId, TLInt offset, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ReadMessageContentsAsync(TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChatAsync(TLInt chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void GetFullUserAsync(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null);
        void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);

        void SetTypingAsync(TLInputPeerBase peer, TLBool typing, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        void GetContactsAsync(TLString hash, Action<TLContactsBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportContactsAsync(TLVector<TLInputContactBase> contacts, TLBool replace, Action<TLImportedContacts> callback, Action<TLRPCError> faultCallback = null);

        void BlockAsync(TLInputUserBase id, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void UnblockAsync(TLInputUserBase id, Action<TLBool> callback, Action<TLRPCError> faultCallback = null); 
        void GetBlockedAsync(TLInt offset, TLInt limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateProfileAsync(TLString firstName, TLString lastName, TLString about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateStatusAsync(TLBool offline, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        void GetFileAsync(TLInt dcId, TLInputFileLocationBase location, TLInt offset, TLInt limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null);
        void GetFileAsync(TLInputFileLocationBase location, TLInt offset, TLInt limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null);
        void SaveFilePartAsync(TLLong fileId, TLInt filePart, TLString bytes, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SaveBigFilePartAsync(TLLong fileId, TLInt filePart, TLInt fileTotalParts, TLString bytes, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        void GetNotifySettingsAsync(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> settings, Action<TLRPCError> faultCallback = null);
        void UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void ResetNotifySettingsAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        // didn't work
        //void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        void UploadProfilePhotoAsync(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null);
        void UpdateProfilePhotoAsync(TLInputPhotoBase id, Action<TLPhotoBase> callback, Action<TLRPCError> faultCallback = null);

        void GetDHConfigAsync(TLInt version, TLInt randomLength, Action<TLDHConfigBase> result, Action<TLRPCError> faultCallback = null);
        void RequestEncryptionAsync(TLInputUserBase userId, TLInt randomId, TLString g_a, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        void AcceptEncryptionAsync(TLInputEncryptedChat peer, TLString gb, TLLong keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, Action<TLSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedFileAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, TLInputEncryptedFileBase file, Action<TLSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, TLInt maxDate, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedServiceAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, Action<TLSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);
        void DiscardEncryptionAsync(TLInt chatId, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SetEncryptedTypingAsync(TLInputEncryptedChat peer, TLBool typing, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        void GetConfigInformationAsync(Action<string> callback);
        void GetTransportInformationAsync(Action<string> callback);
        void GetUserPhotosAsync(TLInputUserBase userId, TLInt offset, TLLong maxId, TLInt limit, Action<TLPhotosBase> callback, Action<TLRPCError> faultCallback = null);
        void GetNearestDCAsync(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null);
        void GetSupportAsync(Action<TLSupport> callback, Action<TLRPCError> faultCallback = null);

        void ResetAuthorizationsAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SetInitState();

        void PingAsync(TLLong pingId, Action<TLPong> callback, Action<TLRPCError> faultCallback = null); 
        void PingDelayDisconnectAsync(TLLong pingId, TLInt disconnectDelay, Action<TLPong> callback, Action<TLRPCError> faultCallback = null);

        void SearchAsync(TLString q, TLInt limit, Action<TLContactsFoundBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(TLString username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAccountTTLAsync(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null);
        void SetAccountTTLAsync(TLAccountDaysTTL ttl, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountTTLAsync(TLString reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetPrivacyAsync(TLInputPrivacyKeyBase key, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void GetStatusesAsync(Action<TLVector<TLContactStatusBase>> callback, Action<TLRPCError> faultCallback = null);
        void UpdateTransportInfoAsync(int dcId, string dcIpAddress, int dcPort, Action<bool> callback);

        void ResolveUsernameAsync(TLString username, Action<TLResolvedPeer> callback, Action<TLRPCError> faultCallback = null);
        void SendChangePhoneCodeAsync(TLString phoneNumber, TLString currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);
        void ChangePhoneAsync(TLString phoneNumber, TLString phoneCodeHash, TLString phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWallpapersAsync(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAllStickersAsync(TLString hash, Action<TLAllStickersBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateDeviceLockedAsync(TLInt period, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        void GetSendingQueueInfoAsync(Action<string> callback);
        void GetSyncErrorsAsync(Action<ExceptionInfo, IList<ExceptionInfo>> callback);
        void GetMessagesAsync(TLVector<TLInt> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);

        // messages
        void GetFeaturedStickersAsync(bool full, TLInt hash, Action<TLFeaturedStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetArchivedStickersAsync(bool full, TLLong offsetId, TLInt limit, Action<TLArchivedStickers> callback, Action<TLRPCError> faultCallback = null);
#if LAYER_42
        void ReadFeaturedStickersAsync(TLVector<TLLong> id, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
#else
        void ReadFeaturedStickersAsync(Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
#endif
        void GetAllDraftsAsync(Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, TLString query, TLString offset, Action<TLBotResults> callback, Action<TLRPCError> faultCallback = null);
        void SetInlineBotResultsAsync(TLBool gallery, TLBool pr, TLLong queryId, TLVector<TLInputBotInlineResult> results, TLInt cacheTime, TLString nextOffset, TLInlineBotSwitchPM switchPM, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SendInlineBotResultAsync(TLMessage45 message, Action<TLMessageCommon> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void GetDocumentByHashAsync(TLString sha256, TLInt size, TLString mimeType, Action<TLDocumentBase> callback, Action<TLRPCError> faultCallback = null);
        void SearchGifsAsync(TLString q, TLInt offset, Action<TLFoundGifs> callback, Action<TLRPCError> faultCallback = null);
        void GetSavedGifsAsync(TLInt hash, Action<TLSavedGifsBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveGifAsync(TLInputDocumentBase id, TLBool unsave, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderStickerSetsAsync(bool masks, TLVector<TLLong> order, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SearchGlobalAsync(TLString query, TLInt offsetDate, TLInputPeerBase offsetPeer, TLInt offsetId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputPeerBase peer, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SendMessageAsync(TLMessage36 message, Action<TLMessageCommon> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void StartBotAsync(TLInputUserBase bot, TLString startParam, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SendBroadcastAsync(TLVector<TLInputUserBase> contacts, TLInputMediaBase inputMedia, TLMessage25 message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void ForwardMessageAsync(TLInputPeerBase peer, TLInt fwdMessageId, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<TLInt> id, IList<TLMessage25> messages, bool withMyScore, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void CreateChatAsync(TLVector<TLInputUserBase> users, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatTitleAsync(TLInt chatId, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatPhotoAsync(TLInt chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void AddChatUserAsync(TLInt chatId, TLInputUserBase userId, TLInt fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChatUserAsync(TLInt chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPagePreviewAsync(TLString message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportChatInviteAsync(TLInt chatId, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null);
        void CheckChatInviteAsync(TLString hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportChatInviteAsync(TLString hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null);
        void InstallStickerSetAsync(TLInputStickerSetBase stickerset, TLBool archived, Action<TLStickerSetInstallResultBase> callback, Action<TLRPCError> faultCallback = null);
        void UninstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void HideReportSpamAsync(TLInputPeerBase peer, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerSettingsAsync(TLInputPeerBase peer, Action<TLPeerSettings> callback, Action<TLRPCError> faultCallback = null);
        void GetBotCallbackAnswerAsync(TLInputPeerBase peer, TLInt messageId, TLString data, TLInt gameId, Action<TLBotCallbackAnswer> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers, Action<TLPeerDialogs> callback, Action<TLRPCError> faultCallback = null);
        void GetRecentStickersAsync(bool attached, TLInt hash, Action<TLRecentStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void ClearRecentStickersAsync(bool attached, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetUnusedStickersAsync(TLInt limit, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAttachedStickersAsync(TLInputStickeredMediaBase media, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);

        // contacts
        void GetTopPeersAsync(GetTopPeersFlags flags, TLInt offset, TLInt limit, TLInt hash, Action<TLTopPeersBase> callback, Action<TLRPCError> faultCallback = null);
        void ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);

        // channels
        void GetChannelHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, TLInt offset, TLInt maxId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<TLInt> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateChannelAsync(TLInt channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, TLBool kicked, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, TLInt offset, TLInt limit, Action<TLChannelsChannelParticipants> callback, Action<TLRPCError> faultCallback = null);
        void EditTitleAsync(TLChannel channel, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditAboutAsync(TLChannel channel, TLString about, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void JoinChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void LeaveChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChannelAsync(TLInputChannelBase channel, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void CreateChannelAsync(TLInt flags, TLString title, TLString about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportInviteAsync(TLInputChannelBase channel, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(TLInputChannelBase channel, TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(TLInputChannelBase channel, TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetChannelDialogsAsync(TLInt offset, TLInt limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, TLInt offsetId, TLInt addOffset, TLInt limit, TLInt maxId, TLInt minId, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLChannel channel, TLInt maxId, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLInputChannelBase channel, TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ToggleInvitesAsync(TLInputChannelBase channel, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportMessageLinkAsync(TLInputChannelBase channel, TLInt id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null);
        void ToggleSignaturesAsync(TLInputChannelBase channel, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessageEditDataAsync(TLInputPeerBase peer, TLInt id, Action<TLMessageEditData> callback, Action<TLRPCError> faultCallback = null);
        void EditMessageAsync(TLInputPeerBase peer, TLInt id, TLString message, TLVector<TLMessageEntityBase> entities, TLReplyKeyboardBase replyMarkup, TLBool noWebPage, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, TLInt id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputChannelBase channel, TLInt userId, TLVector<TLInt> id, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void GetAdminedPublicChannelsAsync(Action<TLChatsBase> callback, Action<TLRPCError> faultCallback = null);

        // updates
        void GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilerBase filter, TLInt pts, TLInt limit, Action<TLChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        // admins
        void ToggleChatAdminsAsync(TLInt chatId, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatAdminAsync(TLInt chatId, TLInputUserBase userId, TLBool isAdmin, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void DeactivateChatAsync(TLInt chatId, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void MigrateChatAsync(TLInt chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);


        // account
        void ReportPeerAsync(TLInputPeerBase peer, TLInputReportReasonBase reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountAsync(TLString reason, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetAuthorizationsAsync(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null);
        void ResetAuthorizationAsync(TLLong hash, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordAsync(Action<TLPasswordBase> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordSettingsAsync(TLString currentPasswordHash, Action<TLPasswordSettings> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePasswordSettingsAsync(TLString currentPasswordHash, TLPasswordInputSettings newSettings, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void CheckPasswordAsync(TLString passwordHash, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void RequestPasswordRecoveryAsync(Action<TLPasswordRecovery> callback, Action<TLRPCError> faultCallback = null);
        void RecoverPasswordAsync(TLString code, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void ConfirmPhoneAsync(TLString phoneCodeHash, TLString phoneCode, Action<TLBool> callback, Action<TLRPCError> faultCallback = null);
        void SendConfirmPhoneCodeAsync(TLString hash, TLBool currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);

        // help
        void GetAppChangelogAsync(TLString deviceModel, TLString systemVersion, TLString appVersion, TLString langCode, Action<TLAppChangelogBase> callback, Action<TLRPCError> faultCallback = null); 
        void GetTermsOfServiceAsync(TLString langCode, Action<TLTermsOfService> callback, Action<TLRPCError> faultCallback = null);


        // encrypted chats
        void RekeyAsync(TLEncryptedChatBase chat, Action<TLLong> callback);

        // background task
        void SendActionsAsync(List<TLObject> actions, Action<TLObject, TLObject> callback, Action<TLRPCError> faultCallback = null);
        void ClearQueue();
    }
}
