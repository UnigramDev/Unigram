using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Channels;
using Telegram.Api.TL.Methods.Contacts;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
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

        int CurrentUserId { get; set; }

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

        void GetStateCallback(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceCallback(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceWithoutUpdatesCallback(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        void RegisterDeviceCallback(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnregisterDeviceCallback(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        

        void MessageAcknowledgments(TLVector<long> ids);

        // auth
        void SendCodeCallback(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null);
        void ResendCodeCallback(string phoneNumber, string phoneCodeHash, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void CancelCodeCallback(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SignInCallback(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void CancelSignInAsync();
        void LogOutAsync(Action callback);
        void LogOutCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void LogOutTransportsAsync(Action callback, Action<List<TLRPCError>> faultCallback = null);
        void SignUpCallback(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Deprecated void SendCallAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
       
        void SearchCallback(TLInputPeerBase peer, string query, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDialogsCallback(int offsetDate, int offsetId, TLInputPeerBase offsetPeer, int limit, Action<TLMessagesDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetHistoryCallback(TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesCallback(TLVector<int> id, bool revoke, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void DeleteHistoryCallback(bool justClear, TLInputPeerBase peer, int offset, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void DeleteContactCallback(TLInputUserBase id, Action<TLContactsLink> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryCallback(TLInputPeerBase peer, int maxId, int offset, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ReadMessageContentsCallback(TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChatCallback(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void GetFullUserCallback(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null);
        void GetUsersCallback(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);

        void SetTypingCallback(TLInputPeerBase peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetTypingCallback(TLInputPeerBase peer, TLSendMessageActionBase action, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetContactsCallback(string hash, Action<TLContactsContactsBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportContactsCallback(TLVector<TLInputContactBase> contacts, bool replace, Action<TLContactsImportedContacts> callback, Action<TLRPCError> faultCallback = null);

        void BlockCallback(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnblockCallback(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null); 
        void GetBlockedCallback(int offset, int limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateProfileCallback(string firstName, string lastName, string about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateStatusCallback(bool offline, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetFileCallback(int dcId, TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFile> callback, Action<TLRPCError> faultCallback = null);
        void GetFileCallback(TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFile> callback, Action<TLRPCError> faultCallback = null);
        void SaveFilePartCallback(long fileId, int filePart, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SaveBigFilePartCallback(long fileId, int filePart, int fileTotalParts, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetNotifySettingsCallback(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateNotifySettingsCallback(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ResetNotifySettingsCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // didn't work
        //void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        void UploadProfilePhotoCallback(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null);
        void UpdateProfilePhotoCallback(TLInputPhotoBase id, Action<TLPhotoBase> callback, Action<TLRPCError> faultCallback = null);

        void GetDHConfigCallback(int version, int randomLength, Action<TLMessagesDHConfig> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] g_a, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void AcceptEncryptionAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedFileAsync(TLInputEncryptedChat peer, long randomId, byte[] data, TLInputEncryptedFileBase file, Action<TLMessagesSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int maxDate, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedServiceAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void DiscardEncryptionAsync(int chatId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetConfigInformationAsync(Action<string> callback);
        void GetTransportInformationAsync(Action<string> callback);
        void GetUserPhotosCallback(TLInputUserBase userId, int offset, long maxId, int limit, Action<TLPhotosPhotosBase> callback, Action<TLRPCError> faultCallback = null);
        void GetNearestDCCallback(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null);
        void GetSupportCallback(Action<TLHelpSupport> callback, Action<TLRPCError> faultCallback = null);

        void ResetAuthorizationsCallback(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetInitState();

        void PingCallback(long pingId, Action<TLPong> callback, Action<TLRPCError> faultCallback = null); 
        void PingDelayDisconnectCallback(long pingId, int disconnectDelay, Action<TLPong> callback, Action<TLRPCError> faultCallback = null);

        void SearchCallback(string q, int limit, Action<TLContactsFound> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameCallback(string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameCallback(string username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAccountTTLCallback(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null);
        void SetAccountTTLCallback(TLAccountDaysTTL ttl, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountTTLCallback(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPrivacyCallback(TLInputPrivacyKeyBase key, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void SetPrivacyCallback(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void GetStatusesCallback(Action<TLVector<TLContactStatus>> callback, Action<TLRPCError> faultCallback = null);
        void UpdateTransportInfoAsync(int dcId, string dcIpAddress, int dcPort, Action<bool> callback);

        void ResolveUsernameCallback(string username, Action<TLContactsResolvedPeer> callback, Action<TLRPCError> faultCallback = null);
        void SendChangePhoneCodeCallback(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void ChangePhoneCallback(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWallpapersCallback(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAllStickersCallback(byte[] hash, Action<TLMessagesAllStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAllStickersCallback(int hash, Action<TLMessagesAllStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetStickerSetsAsync(ITLStickers stickers, Action<ITLStickers> callback, Action<object> getStickerSetCallback, Action<TLRPCError> faultCallback);

        void UpdateDeviceLockedCallback(int period, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetSendingQueueInfoAsync(Action<string> callback);
        void GetSyncErrorsAsync(Action<ExceptionInfo, IList<ExceptionInfo>> callback);
        void GetMessagesCallback(TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);

        // messages
        void GetFeaturedStickersCallback(bool full, int hash, Action<TLMessagesFeaturedStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetArchivedStickersCallback(bool full, long offsetId, int limit, Action<TLMessagesArchivedStickers> callback, Action<TLRPCError> faultCallback = null);
        void ReadFeaturedStickersCallback(TLVector<long> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAllDraftsCallback(Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveDraftCallback(TLInputPeerBase peer, TLDraftMessageBase draft, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetInlineBotResultsCallback(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset, Action<TLMessagesBotResults> callback, Action<TLRPCError> faultCallback = null);
        void SetInlineBotResultsCallback(bool gallery, bool pr, long queryId, TLVector<TLInputBotInlineResultBase> results, int cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendInlineBotResultCallback(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void GetDocumentByHashCallback(byte[] sha256, int size, string mimeType, Action<TLDocumentBase> callback, Action<TLRPCError> faultCallback = null);
        void SearchGifsCallback(string q, int offset, Action<TLMessagesFoundGifs> callback, Action<TLRPCError> faultCallback = null);
        void GetSavedGifsCallback(int hash, Action<TLMessagesSavedGifsBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveGifCallback(TLInputDocumentBase id, bool unsave, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderStickerSetsCallback(bool masks, TLVector<long> order, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SearchGlobalCallback(string query, int offsetDate, TLInputPeerBase offsetPeer, int offsetId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamCallback(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendMessageCallback(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendMediaCallback(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void StartBotCallback(TLInputUserBase bot, string startParam, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessageCallback(TLInputPeerBase peer, int fwdMessageId, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessagesCallback(TLInputPeerBase toPeer, TLInputPeerBase fromPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void CreateChatCallback(TLVector<TLInputUserBase> users, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatTitleCallback(int chatId, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatPhotoCallback(int chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void AddChatUserCallback(int chatId, TLInputUserBase userId, int fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChatUserCallback(int chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPagePreviewCallback(string message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPageCallback(string url, int hash, Action<TLWebPageBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportChatInviteCallback(int chatId, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckChatInviteCallback(string hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportChatInviteCallback(string hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetStickerSetCallback(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null);
        void InstallStickerSetCallback(TLInputStickerSetBase stickerset, bool archived, Action<TLMessagesStickerSetInstallResultBase> callback, Action<TLRPCError> faultCallback = null);
        void UninstallStickerSetCallback(TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void HideReportSpamCallback(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerSettingsCallback(TLInputPeerBase peer, Action<TLPeerSettings> callback, Action<TLRPCError> faultCallback = null);
        void GetBotCallbackAnswerCallback(TLInputPeerBase peer, int messageId, byte[] data, bool game, Action<TLMessagesBotCallbackAnswer> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerDialogsCallback(TLVector<TLInputPeerBase> peers, Action<TLMessagesPeerDialogs> callback, Action<TLRPCError> faultCallback = null);
        void GetRecentStickersCallback(bool attached, int hash, Action<TLMessagesRecentStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void ClearRecentStickersCallback(bool attached, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAttachedStickersCallback(TLInputStickeredMediaBase media, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);
        void ToggleDialogPinCallback(TLInputPeerBase peer, bool pin, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderPinnedDialogsCallback(TLVector<TLInputPeerBase> order, bool force, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // contacts
        void GetTopPeersCallback(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash, Action<TLContactsTopPeersBase> callback, Action<TLRPCError> faultCallback = null);
        void ResetTopPeerRatingCallback(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // channels
        void GetChannelHistoryCallback(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessagesCallback(TLInputChannelBase inputChannel, TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateChannelCallback(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void EditAdminCallback(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void KickFromChannelCallback(TLChannel channel, TLInputUserBase userId, bool kicked, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantCallback(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantsCallback(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit, Action<TLChannelsChannelParticipants> callback, Action<TLRPCError> faultCallback = null);
        void EditTitleCallback(TLChannel channel, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditAboutCallback(TLChannel channel, string about, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void EditPhotoCallback(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void JoinChannelCallback(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void LeaveChannelCallback(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChannelCallback(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void InviteToChannelCallback(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChannelCallback(TLInputChannelBase channel, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void CreateChannelCallback(TLChannelsCreateChannel.Flag flags, string title, string about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportInviteCallback(TLInputChannelBase channel, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameCallback(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameCallback(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Layer 56 void GetImportantHistoryCallback(TLInputChannelBase channel, TLPeerBase peer, bool sync, int? offsetId, int? addOffset, int? limit, int? maxId, int? minId, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryCallback(TLChannel channel, int maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesCallback(TLInputChannelBase channel, TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ToggleInvitesCallback(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportMessageLinkCallback(TLInputChannelBase channel, int id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null);
        void ToggleSignaturesCallback(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessageEditDataCallback(TLInputPeerBase peer, int id, Action<TLMessagesMessageEditData> callback, Action<TLRPCError> faultCallback = null);
        void EditMessageCallback(TLInputPeerBase peer, int id, string message, TLVector<TLMessageEntityBase> entities, TLReplyMarkupBase replyMarkup, bool noWebPage, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePinnedMessageCallback(bool silent, TLInputChannelBase channel, int id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamCallback(TLInputChannelBase channel, TLInputUserBase userId, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteUserHistoryCallback(TLChannel channel, TLInputUserBase userId, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void GetAdminedPublicChannelsCallback(Action<TLMessagesChatsBase> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Layer 56 void GetAdminedPublicChannelsCallback(Action<TLMessagesChats> callback, Action<TLRPCError> faultCallback = null);

        // updates
        void GetChannelDifferenceCallback(TLInputChannelBase inputChannel, TLChannelMessagesFilterBase filter, int pts, int limit, Action<TLUpdatesChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        // admins
        void ToggleChatAdminsCallback(int chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatAdminCallback(int chatId, TLInputUserBase userId, bool isAdmin, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: probably deprecated void DeactivateChatAsync(int chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void MigrateChatCallback(int chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);


        // account
        void ReportPeerCallback(TLInputPeerBase peer, TLReportReasonBase reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountCallback(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAuthorizationsCallback(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null);
        void ResetAuthorizationCallback(long hash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordCallback(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordSettingsCallback(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePasswordSettingsCallback(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void CheckPasswordCallback(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void RequestPasswordRecoveryCallback(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null);
        void RecoverPasswordCallback(string code, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void ConfirmPhoneCallback(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendConfirmPhoneCodeCallback(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);

        // help
        void GetAppChangelogCallback(string deviceModel, string systemVersion, string appVersion, string langCode, Action<TLHelpAppChangelogBase> callback, Action<TLRPCError> faultCallback = null); 
        void GetTermsOfServiceCallback(string langCode, Action<TLHelpTermsOfService> callback, Action<TLRPCError> faultCallback = null);


        // encrypted chats
        void RekeyAsync(TLEncryptedChatBase chat, Action<long> callback);

        // background task
        void SendActionsAsync(List<TLObject> actions, Action<TLObject, object> callback, Action<TLRPCError> faultCallback = null);
        void ClearQueue();
    }
}
