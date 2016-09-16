using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public interface IMTProtoService
    {
        event EventHandler<TransportCheckedEventArgs> TransportChecked;

        string Message { get; }
        void SetMessageOnTime(double seconds, string message);

        ITransport GetActiveTransport();
        Tuple<int, int, int> GetCurrentPacketInfo();
        string GetTransportInfo();

        string Country { get; }
        event EventHandler<CountryEventArgs> GotUserCountry;

        // To remove multiple UpdateStatusAsync calls, it's prefer to invoke this method instead
        void RaiseSendStatus(SendStatusEventArgs e);

        int? CurrentUserId { get; set; }

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
        void GetDifferenceAsync(int? pts, int? date, int? qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceWithoutUpdatesAsync(int? pts, int? date, int? qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        void RegisterDeviceAsync(int? tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnregisterDeviceAsync(int? tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        

        void MessageAcknowledgments(TLVector<long> ids);

        // auth
        void SendCodeAsync(string phoneNumber, string currentNumber, Action<TLSentCodeBase> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null);
        void ResendCodeAsync(string phoneNumber, string phoneCodeHash, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);
        void CancelCodeAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void CancelSignInAsync();
        void LogOutAsync(Action callback);
        void LogOutAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void LogOutTransportsAsync(Action callback, Action<List<TLRPCError>> faultCallback = null);
        void SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void SendCallAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
       
        void SearchAsync(TLInputPeerBase peer, string query, TLInputMessagesFilterBase filter, int? minDate, int? maxDate, int? offset, int? maxId, int? limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDialogsAsync(Stopwatch timer, int? offsetDate, int? offsetId, TLInputPeerBase offsetPeer, int? limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetHistoryAsync(Stopwatch timer, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int? offset, int? maxId, int? limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLVector<int> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, int? offset, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void DeleteContactAsync(TLInputUserBase id, Action<TLLinkBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLInputPeerBase peer, int? maxId, int? offset, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ReadMessageContentsAsync(TLVector<int> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChatAsync(int? chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void GetFullUserAsync(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null);
        void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);

        void SetTypingAsync(TLInputPeerBase peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetContactsAsync(string hash, Action<TLContactsBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportContactsAsync(TLVector<TLInputContactBase> contacts, bool replace, Action<TLImportedContacts> callback, Action<TLRPCError> faultCallback = null);

        void BlockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnblockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null); 
        void GetBlockedAsync(int? offset, int? limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateProfileAsync(string firstName, string lastName, string about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateStatusAsync(bool offline, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetFileAsync(int? dcId, TLInputFileLocationBase location, int? offset, int? limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null);
        void GetFileAsync(TLInputFileLocationBase location, int? offset, int? limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null);
        void SaveFilePartAsync(long? fileId, int? filePart, string bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SaveBigFilePartAsync(long? fileId, int? filePart, int? fileTotalParts, string bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetNotifySettingsAsync(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> settings, Action<TLRPCError> faultCallback = null);
        void UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ResetNotifySettingsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // didn't work
        //void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        void UploadProfilePhotoAsync(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null);
        void UpdateProfilePhotoAsync(TLInputPhotoBase id, Action<TLPhotoBase> callback, Action<TLRPCError> faultCallback = null);

        void GetDHConfigAsync(int? version, int? randomLength, Action<TLDHConfigBase> result, Action<TLRPCError> faultCallback = null);
        void RequestEncryptionAsync(TLInputUserBase userId, int? randomId, string g_a, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        void AcceptEncryptionAsync(TLInputEncryptedChat peer, string gb, long? keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedAsync(TLInputEncryptedChat peer, long? randomId, string data, Action<TLSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedFileAsync(TLInputEncryptedChat peer, long? randomId, string data, TLInputEncryptedFileBase file, Action<TLSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int? maxDate, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendEncryptedServiceAsync(TLInputEncryptedChat peer, long? randomId, string data, Action<TLSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);
        void DiscardEncryptionAsync(int? chatId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetConfigInformationAsync(Action<string> callback);
        void GetTransportInformationAsync(Action<string> callback);
        void GetUserPhotosAsync(TLInputUserBase userId, int? offset, long? maxId, int? limit, Action<TLPhotosBase> callback, Action<TLRPCError> faultCallback = null);
        void GetNearestDCAsync(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null);
        void GetSupportAsync(Action<TLSupport> callback, Action<TLRPCError> faultCallback = null);

        void ResetAuthorizationsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetInitState();

        void PingAsync(long? pingId, Action<TLPong> callback, Action<TLRPCError> faultCallback = null); 
        void PingDelayDisconnectAsync(long? pingId, int? disconnectDelay, Action<TLPong> callback, Action<TLRPCError> faultCallback = null);

        void SearchAsync(string q, int? limit, Action<TLContactsFoundBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(string username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAccountTTLAsync(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null);
        void SetAccountTTLAsync(TLAccountDaysTTL ttl, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountTTLAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPrivacyAsync(TLInputPrivacyKeyBase key, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void GetStatusesAsync(Action<TLVector<TLContactStatusBase>> callback, Action<TLRPCError> faultCallback = null);
        void UpdateTransportInfoAsync(int dcId, string dcIpAddress, int dcPort, Action<bool> callback);

        void ResolveUsernameAsync(string username, Action<TLResolvedPeer> callback, Action<TLRPCError> faultCallback = null);
        void SendChangePhoneCodeAsync(string phoneNumber, string currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);
        void ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWallpapersAsync(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAllStickersAsync(string hash, Action<TLAllStickersBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateDeviceLockedAsync(int? period, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetSendingQueueInfoAsync(Action<string> callback);
        void GetSyncErrorsAsync(Action<ExceptionInfo, IList<ExceptionInfo>> callback);
        void GetMessagesAsync(TLVector<int> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);

        // messages
        void GetFeaturedStickersAsync(bool full, int? hash, Action<TLFeaturedStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetArchivedStickersAsync(bool full, long? offsetId, int? limit, Action<TLArchivedStickers> callback, Action<TLRPCError> faultCallback = null);
#if LAYER_42
        void ReadFeaturedStickersAsync(TLVector<long> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
#else
        void ReadFeaturedStickersAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
#endif
        void GetAllDraftsAsync(Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset, Action<TLBotResults> callback, Action<TLRPCError> faultCallback = null);
        void SetInlineBotResultsAsync(bool gallery, bool pr, long? queryId, TLVector<TLInputBotInlineResult> results, int? cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendInlineBotResultAsync(TLMessage message, Action<TLMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void GetDocumentByHashAsync(string sha256, int? size, string mimeType, Action<TLDocumentBase> callback, Action<TLRPCError> faultCallback = null);
        void SearchGifsAsync(string q, int? offset, Action<TLFoundGifs> callback, Action<TLRPCError> faultCallback = null);
        void GetSavedGifsAsync(int? hash, Action<TLSavedGifsBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveGifAsync(TLInputDocumentBase id, bool unsave, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderStickerSetsAsync(bool masks, TLVector<long> order, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SearchGlobalAsync(string query, int? offsetDate, TLInputPeerBase offsetPeer, int? offsetId, int? limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendMessageAsync(TLMessage message, Action<TLMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SendBroadcastAsync(TLVector<TLInputUserBase> contacts, TLInputMediaBase inputMedia, TLMessage message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void ForwardMessageAsync(TLInputPeerBase peer, int? fwdMessageId, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void CreateChatAsync(TLVector<TLInputUserBase> users, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatTitleAsync(int? chatId, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatPhotoAsync(int? chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void AddChatUserAsync(int? chatId, TLInputUserBase userId, int? fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChatUserAsync(int? chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPagePreviewAsync(string message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportChatInviteAsync(int? chatId, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null);
        void CheckChatInviteAsync(string hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportChatInviteAsync(string hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null);
        void InstallStickerSetAsync(TLInputStickerSetBase stickerset, bool archived, Action<TLStickerSetInstallResultBase> callback, Action<TLRPCError> faultCallback = null);
        void UninstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void HideReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerSettingsAsync(TLInputPeerBase peer, Action<TLPeerSettings> callback, Action<TLRPCError> faultCallback = null);
        void GetBotCallbackAnswerAsync(TLInputPeerBase peer, int? messageId, string data, int? gameId, Action<TLBotCallbackAnswer> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers, Action<TLPeerDialogs> callback, Action<TLRPCError> faultCallback = null);
        void GetRecentStickersAsync(bool attached, int? hash, Action<TLRecentStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void ClearRecentStickersAsync(bool attached, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetUnusedStickersAsync(int? limit, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAttachedStickersAsync(TLInputStickeredMediaBase media, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);

        // contacts
        void GetTopPeersAsync(GetTopPeersFlags flags, int? offset, int? limit, int? hash, Action<TLTopPeersBase> callback, Action<TLRPCError> faultCallback = null);
        void ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // channels
        void GetChannelHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int? offset, int? maxId, int? limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateChannelAsync(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, bool kicked, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int? offset, int? limit, Action<TLChannelsChannelParticipants> callback, Action<TLRPCError> faultCallback = null);
        void EditTitleAsync(TLChannel channel, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditAboutAsync(TLChannel channel, string about, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void JoinChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void LeaveChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChannelAsync(TLInputChannelBase channel, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void CreateChannelAsync(int? flags, string title, string about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportInviteAsync(TLInputChannelBase channel, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetChannelDialogsAsync(int? offset, int? limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, int? offsetId, int? addOffset, int? limit, int? maxId, int? minId, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLChannel channel, int? maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ToggleInvitesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportMessageLinkAsync(TLInputChannelBase channel, int? id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null);
        void ToggleSignaturesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessageEditDataAsync(TLInputPeerBase peer, int? id, Action<TLMessageEditData> callback, Action<TLRPCError> faultCallback = null);
        void EditMessageAsync(TLInputPeerBase peer, int? id, string message, TLVector<TLMessageEntityBase> entities, TLReplyKeyboardBase replyMarkup, bool noWebPage, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, int? id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputChannelBase channel, int? userId, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void GetAdminedPublicChannelsAsync(Action<TLChatsBase> callback, Action<TLRPCError> faultCallback = null);

        // updates
        void GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilerBase filter, int? pts, int? limit, Action<TLChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        // admins
        void ToggleChatAdminsAsync(int? chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatAdminAsync(int? chatId, TLInputUserBase userId, bool isAdmin, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeactivateChatAsync(int? chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void MigrateChatAsync(int? chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);


        // account
        void ReportPeerAsync(TLInputPeerBase peer, TLInputReportReasonBase reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAuthorizationsAsync(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null);
        void ResetAuthorizationAsync(long? hash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordAsync(Action<TLPasswordBase> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordSettingsAsync(string currentPasswordHash, Action<TLPasswordSettings> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePasswordSettingsAsync(string currentPasswordHash, TLPasswordInputSettings newSettings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void CheckPasswordAsync(string passwordHash, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void RequestPasswordRecoveryAsync(Action<TLPasswordRecovery> callback, Action<TLRPCError> faultCallback = null);
        void RecoverPasswordAsync(string code, Action<TLAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void ConfirmPhoneAsync(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendConfirmPhoneCodeAsync(string hash, bool currentNumber, Action<TLSentCodeBase> callback, Action<TLRPCError> faultCallback = null);

        // help
        void GetAppChangelogAsync(string deviceModel, string systemVersion, string appVersion, string langCode, Action<TLAppChangelogBase> callback, Action<TLRPCError> faultCallback = null); 
        void GetTermsOfServiceAsync(string langCode, Action<TLTermsOfService> callback, Action<TLRPCError> faultCallback = null);


        // encrypted chats
        void RekeyAsync(TLEncryptedChatBase chat, Action<long> callback);

        // background task
        void SendActionsAsync(List<TLObject> actions, Action<TLObject, TLObject> callback, Action<TLRPCError> faultCallback = null);
        void ClearQueue();
    }
}
