using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Native.TL;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Channels;
using Telegram.Api.TL.Channels.Methods;
using Telegram.Api.TL.Contacts;
using Telegram.Api.TL.Contacts.Methods;
using Telegram.Api.TL.Help;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Payments;
using Telegram.Api.TL.Phone;
using Telegram.Api.TL.Photos;
using Telegram.Api.TL.Updates;
using Telegram.Api.TL.Upload;

namespace Telegram.Api.Services
{
    public partial interface IMTProtoService
    {
        string Message { get; }
        void SetMessageOnTime(double seconds, string message);

        string Country { get; }
        event EventHandler<CountryEventArgs> GotUserCountry;

        NetworkType NetworkType { get; }

        // To remove multiple UpdateStatusAsync calls, it's prefer to invoke this method instead
        void RaiseSendStatus(bool offline);

        int CurrentUserId { get; set; }

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

        void ToggleProxy();

        void GetStateAsync(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        void RegisterDeviceAsync(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnregisterDeviceAsync(int tokenType, string token, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        

        void SendRequestAsync<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null);
        void SendRequestAsync<T>(string caption, TLObject obj, int dcId, bool cdn, Action<T> callback, Action<TLRPCError> faultCallback = null);

        // auth
        void SendCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<int> attemptFailed = null, Action<TLRPCError> faultCallback = null);
        void ResendCodeAsync(string phoneNumber, string phoneCodeHash, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void CancelCodeAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void LogOutAsync(Action callback);
        void LogOutAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Deprecated void SendCallAsync(string phoneNumber, string phoneCodeHash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
       
        void SearchAsync(TLInputPeerBase peer, string query, TLInputUserBase from, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase offsetPeer, int limit, Action<TLMessagesDialogsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetHistoryAsync(TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int offsetDate, int maxId, int limit, int hash, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLVector<int> id, bool revoke, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, int maxId, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void DeleteContactAsync(TLInputUserBase id, Action<TLContactsLink> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLInputPeerBase peer, int maxId, int offset, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ReadMessageContentsAsync(TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChatAsync(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void GetFullUserAsync(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null);
        void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetCommonChatsAsync(TLInputUserBase id, int maxId, int limit, Action<TLMessagesChatsBase> callback, Action<TLRPCError> faultCallback = null);

        void SetTypingAsync(TLInputPeerBase peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetContactsAsync(int hash, Action<TLContactsContactsBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportContactsAsync(TLVector<TLInputContactBase> contacts, Action<TLContactsImportedContacts> callback, Action<TLRPCError> faultCallback = null);

        void BlockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UnblockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null); 
        void GetBlockedAsync(int offset, int limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateProfileAsync(string firstName, string lastName, string about, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateStatusAsync(bool offline, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetCdnFileAsync(int dcId, byte[] fileToken, int offset, int limit, Action<TLUploadCdnFileBase> callback, Action<TLRPCError> faultCallback = null);
        void ReuploadCdnFileAsync(int dcId, byte[] fileToken, byte[] requestToken, Action<TLVector<TLCdnFileHash>> callback, Action<TLRPCError> faultCallback = null);
        void GetWebFileAsync(int dcId, TLInputWebFileLocation location, int offset, int limit, Action<TLUploadWebFile> callback, Action<TLRPCError> faultCallback = null);
        void GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFileAsync(TLInputFileLocationBase location, int offset, int limit, Action<TLUploadFileBase> callback, Action<TLRPCError> faultCallback = null);
        void GetCdnFileAsync(byte[] fileToken, int offset, int limit, Action<TLUploadCdnFileBase> callback, Action<TLRPCError> faultCallback = null);
        void GetCdnFileHashesAsync(byte[] fileToken, int offset, Action<TLVector<TLCdnFileHash>> callback, Action<TLRPCError> faultCallback = null);
        void ReuploadCdnFileAsync(byte[] fileToken, byte[] requestToken, Action<TLVector<TLCdnFileHash>> callback, Action<TLRPCError> faultCallback = null);
        void SaveFilePartAsync(long fileId, int filePart, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetNotifySettingsAsync(TLInputNotifyPeerBase peer, Action<TLPeerNotifySettingsBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ResetNotifySettingsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // didn't work
        //void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        void UploadProfilePhotoAsync(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null);
        void UpdateProfilePhotoAsync(TLInputPhotoBase id, Action<TLUserProfilePhotoBase> callback, Action<TLRPCError> faultCallback = null);
        void DeletePhotosAsync(TLVector<TLInputPhotoBase> id, Action<TLVector<long>> callback, Action<TLRPCError> faultCallback = null);

        void GetDHConfigAsync(int version, int randomLength, Action<TLMessagesDHConfig> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] g_a, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void AcceptEncryptionAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedFileAsync(TLInputEncryptedChat peer, long randomId, byte[] data, TLInputEncryptedFileBase file, Action<TLMessagesSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int maxDate, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SendEncryptedServiceAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void DiscardEncryptionAsync(int chatId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Encrypted void SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        //void GetConfigInformationAsync(Action<string> callback);
        void GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit, Action<TLPhotosPhotosBase> callback, Action<TLRPCError> faultCallback = null);
        void GetNearestDCAsync(Action<TLNearestDC> callback, Action<TLRPCError> faultCallback = null);
        void GetSupportAsync(Action<TLHelpSupport> callback, Action<TLRPCError> faultCallback = null);

        void ResetAuthorizationsAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetInitState();

        void SearchAsync(string q, int limit, Action<TLContactsFound> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(string username, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAccountTTLAsync(Action<TLAccountDaysTTL> callback, Action<TLRPCError> faultCallback = null);
        void SetAccountTTLAsync(TLAccountDaysTTL ttl, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountTTLAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPrivacyAsync(TLInputPrivacyKeyBase key, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules, Action<TLAccountPrivacyRules> callback, Action<TLRPCError> faultCallback = null);
        void GetStatusesAsync(Action<TLVector<TLContactStatus>> callback, Action<TLRPCError> faultCallback = null);

        void ResolveUsernameAsync(string username, Action<TLContactsResolvedPeer> callback, Action<TLRPCError> faultCallback = null);
        void SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);
        void ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode, Action<TLUserBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWallpapersAsync(Action<TLVector<TLWallPaperBase>> callback, Action<TLRPCError> faultCallback = null);
        void GetAllStickersAsync(int hash, Action<TLMessagesAllStickersBase> callback, Action<TLRPCError> faultCallback = null);

        void UpdateDeviceLockedAsync(int period, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        void GetMessagesAsync(TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);

        // messages
        void SendMultiMediaAsync(TLInputPeerBase inputPeer, TLVector<TLInputSingleMedia> multiMedia, IList<TLMessage> messages, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void UploadMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadMentionsAsync(TLInputPeerBase inputPeer, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void GetUnreadMentionsAsync(TLInputPeerBase inputPeer, int offsetId, int addOffset, int limit, int maxId, int minId, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void FaveStickerAsync(TLInputDocumentBase id, bool unfave, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetFavedStickersAsync(int hash, Action<TLMessagesFavedStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFeaturedStickersAsync(int hash, Action<TLMessagesFeaturedStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void GetArchivedStickersAsync(long offsetId, int limit, bool masks, Action<TLMessagesArchivedStickers> callback, Action<TLRPCError> faultCallback = null);
        void ReadFeaturedStickersAsync(TLVector<long> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAllDraftsAsync(Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset, Action<TLMessagesBotResults> callback, Action<TLRPCError> faultCallback = null);
        void SetInlineBotResultsAsync(bool gallery, bool pr, long queryId, TLVector<TLInputBotInlineResultBase> results, int cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendInlineBotResultAsync(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void GetDocumentByHashAsync(byte[] sha256, int size, string mimeType, Action<TLDocumentBase> callback, Action<TLRPCError> faultCallback = null);
        void SearchGifsAsync(string q, int offset, Action<TLMessagesFoundGifs> callback, Action<TLRPCError> faultCallback = null);
        void GetSavedGifsAsync(int hash, Action<TLMessagesSavedGifsBase> callback, Action<TLRPCError> faultCallback = null);
        void SaveGifAsync(TLInputDocumentBase id, bool unsave, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderStickerSetsAsync(bool masks, TLVector<long> order, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SearchGlobalAsync(string query, int offsetDate, TLInputPeerBase offsetPeer, int offsetId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendMessageAsync(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null);
        void SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ForwardMessagesAsync(TLInputPeerBase toPeer, TLInputPeerBase fromPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore, bool grouped, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void CreateChatAsync(TLVector<TLInputUserBase> users, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatTitleAsync(int chatId, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChatUserAsync(int chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPagePreviewAsync(string message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null);
        void GetWebPageAsync(string url, int hash, Action<TLWebPageBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportChatInviteAsync(int chatId, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckChatInviteAsync(string hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void ImportChatInviteAsync(string hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null);
        void InstallStickerSetAsync(TLInputStickerSetBase stickerset, bool archived, Action<TLMessagesStickerSetInstallResultBase> callback, Action<TLRPCError> faultCallback = null);
        void UninstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void HideReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerSettingsAsync(TLInputPeerBase peer, Action<TLPeerSettings> callback, Action<TLRPCError> faultCallback = null);
        void GetBotCallbackAnswerAsync(TLInputPeerBase peer, int messageId, byte[] data, bool game, Action<TLMessagesBotCallbackAnswer> callback, Action<TLRPCError> faultCallback = null);
        void GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers, Action<TLMessagesPeerDialogs> callback, Action<TLRPCError> faultCallback = null);
        void GetRecentStickersAsync(bool attached, int hash, Action<TLMessagesRecentStickersBase> callback, Action<TLRPCError> faultCallback = null);
        void ClearRecentStickersAsync(bool attached, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAttachedStickersAsync(TLInputStickeredMediaBase media, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null);
        void ToggleDialogPinAsync(TLInputPeerBase peer, bool pin, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReorderPinnedDialogsAsync(TLVector<TLInputPeerBase> order, bool force, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // contacts
        void ResetSavedAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetTopPeersAsync(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash, Action<TLContactsTopPeersBase> callback, Action<TLRPCError> faultCallback = null);
        void ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);

        // channels
        void TogglePreHistoryHiddenAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteHistoryAsync(TLInputChannelBase inputChannel, int maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetStickersAsync(TLInputChannelBase inputChannel, TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void ReadMessageContentsAsync(TLInputChannelBase inputChannel, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdateChannelAsync(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelAdminRights rights, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditBannedAsync(TLChannel channel, TLInputUserBase userId, TLChannelBannedRights rights, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null);
        void GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit, int hash, Action<TLChannelsChannelParticipantsBase> callback, Action<TLRPCError> faultCallback = null);
        void EditTitleAsync(TLChannel channel, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditAboutAsync(TLChannel channel, string about, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void JoinChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void LeaveChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void DeleteChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetFullChannelAsync(TLInputChannelBase channel, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
        void CreateChannelAsync(TLChannelsCreateChannel.Flag flags, string title, string about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportInviteAsync(TLInputChannelBase channel, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null);
        void CheckUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void UpdateUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: Layer 56 void GetImportantHistoryCallback(TLInputChannelBase channel, TLPeerBase peer, bool sync, int? offsetId, int? addOffset, int? limit, int? maxId, int? minId, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReadHistoryAsync(TLChannel channel, int maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null);
        void ToggleInvitesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ExportMessageLinkAsync(TLInputChannelBase channel, int id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null);
        void ToggleSignaturesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetMessageEditDataAsync(TLInputPeerBase peer, int id, Action<TLMessagesMessageEditData> callback, Action<TLRPCError> faultCallback = null);
        void EditMessageAsync(TLInputPeerBase peer, int id, string message, TLVector<TLMessageEntityBase> entities, TLReplyMarkupBase replyMarkup, TLInputGeoPointBase geoPoint, bool noWebPage, bool stop, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, int id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void ReportSpamAsync(TLInputChannelBase channel, TLInputUserBase userId, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null);
        void GetAdminedPublicChannelsAsync(Action<TLMessagesChatsBase> callback, Action<TLRPCError> faultCallback = null);
        void GetAdminLogAsync(TLInputChannelBase inputChannel, string query, TLChannelAdminLogEventsFilter filter, TLVector<TLInputUserBase> admins, long maxId, long minId, int limit, Action<TLChannelsAdminLogResults> callback, Action<TLRPCError> faultCallback = null);

        // updates
        void GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilterBase filter, int pts, int limit, Action<TLUpdatesChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        // admins
        void ToggleChatAdminsAsync(int chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        // TODO: probably deprecated void DeactivateChatAsync(int chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void MigrateChatAsync(int chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);


        // account
        void GetTmpPasswordAsync(byte[] hash, int period, Action<TLAccountTmpPassword> callback, Action<TLRPCError> faultCallback = null);
        void ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void DeleteAccountAsync(string reason, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetAuthorizationsAsync(Action<TLAccountAuthorizations> callback, Action<TLRPCError> faultCallback = null);
        void ResetAuthorizationAsync(long hash, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordAsync(Action<TLAccountPasswordBase> callback, Action<TLRPCError> faultCallback = null);
        void GetPasswordSettingsAsync(byte[] currentPasswordHash, Action<TLAccountPasswordSettings> callback, Action<TLRPCError> faultCallback = null);
        void UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void CheckPasswordAsync(byte[] passwordHash, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void RequestPasswordRecoveryAsync(Action<TLAuthPasswordRecovery> callback, Action<TLRPCError> faultCallback = null);
        void RecoverPasswordAsync(string code, Action<TLAuthAuthorization> callback, Action<TLRPCError> faultCallback = null);
        void ConfirmPhoneAsync(string phoneCodeHash, string phoneCode, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SendConfirmPhoneCodeAsync(string hash, bool currentNumber, Action<TLAuthSentCode> callback, Action<TLRPCError> faultCallback = null);

        // help
        void GetAppChangelogAsync(string prevAppVersion, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null); 
        void GetTermsOfServiceAsync(string langCode, Action<TLHelpTermsOfService> callback, Action<TLRPCError> faultCallback = null);

        // payments
        void ClearSavedInfoAsync(bool info, bool credentials, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void GetPaymentFormAsync(int msgId, Action<TLPaymentsPaymentForm> callback, Action<TLRPCError> faultCallback = null);
        void GetPaymentReceiptAsync(int msgId, Action<TLPaymentsPaymentReceipt> callback, Action<TLRPCError> faultCallback = null);
        void GetSavedInfoAsync(Action<TLPaymentsSavedInfo> callback, Action<TLRPCError> faultCallback = null);
        void SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials, Action<TLPaymentsPaymentResultBase> callback, Action<TLRPCError> faultCallback = null);
        void ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null);

        // calls
        void AcceptCallAsync(TLInputPhoneCall peer, byte[] gb, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null);
        void ConfirmCallAsync(TLInputPhoneCall peer, byte[] ga, long fingerprint, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null);
        void DiscardCallAsync(TLInputPhoneCall peer, int duration, TLPhoneCallDiscardReasonBase reason, long connectionId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);
        void GetCallConfigAsync(Action<TLDataJSON> callback, Action<TLRPCError> faultCallback = null);
        void ReceivedCallAsync(TLInputPhoneCall peer, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void RequestCallAsync(TLInputUserBase userId, int randomId, byte[] gaHash, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null);
        void SaveCallDebugAsync(TLInputPhoneCall peer, TLDataJSON debug, Action<bool> callback, Action<TLRPCError> faultCallback = null);
        void SetCallRatingAsync(TLInputPhoneCall peer, int rating, string comment, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null);

        // langpack
        void GetDifferenceAsync(int fromVersion, Action<TLLangPackDifference> callback, Action<TLRPCError> faultCallback = null);
        void GetLangPackAsync(string langCode, Action<TLLangPackDifference> callback, Action<TLRPCError> faultCallback = null);
        void GetLanguagesAsync(Action<TLVector<TLLangPackLanguage>> callback, Action<TLRPCError> faultCallback = null);
        void GetStringsAsync(string langCode, TLVector<string> keys, Action<TLVector<TLLangPackStringBase>> callback, Action<TLRPCError> faultCallback = null);
    }
}
