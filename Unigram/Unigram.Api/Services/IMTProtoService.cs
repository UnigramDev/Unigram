using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public interface IMTProtoService
    {
        string Message { get; }
        void SetMessageOnTimeAsync(double seconds, string message);

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

        #region Callbacks
        void GetStateCallbackAsync(Action<TLUpdatesState> callback, Action<TLRPCError> faultCallback = null);

        void GetDHConfigCallbackAsync(int version, int randomLength, Action<TLServerDHInnerData> result, Action<TLRPCError> faultCallback = null);

        void GetDifferenceCallbackAsync(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);

        void SendEncryptedServiceCallbackAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);

        void AcceptEncryptionCallbackAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);

        void GetFullChatCallbackAsync(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);

        void UpdateChannelCallbackAsync(int channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);

        void GetParticipantCallbackAsync(TLInputChannelBase channelId, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback);
        #endregion

        Task<MTProtoResponse<TLUpdatesState>> GetStateAsync();

        Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceAsync(int pts, int date, int qts);

        Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts);

        Task<MTProtoResponse<bool>> RegisterDeviceAsync(int tokenType, string token);

        Task<MTProtoResponse<bool>> UnregisterDeviceAsync(string token);

        void MessageAcknowledgments(TLVector<long> ids);

        Task<MTProtoResponse<TLAuthSentCode>> SendCodeAsync(string phoneNumber);

        Task<MTProtoResponse<TLAuthAuthorization>> SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode);
        Task<MTProtoResponse<TLAuthSentCode>> CancelSignInAsync();
        
        Task<MTProtoResponse<bool>> LogOutAsync();
        void LogOutTransportsAsync();
        Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName);
        // DEPRECATED: Task<MTProtoResponse<bool>> SendCallAsync(string phoneNumber, string phoneCodeHash);
        Task<MTProtoResponse<TLMessagesMessagesBase>> SearchAsync(TLInputPeerBase peer, string query, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit);

#if LAYER_40
        Task<MTProtoResponse<TLMessagesDialogsBase>> GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase peer, int limit);
#else
        void GetDialogsAsync(int? offset, int? maxId, int? limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null);
#endif
        Task<MTProtoResponse<TLMessagesMessagesBase>> GetHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit);
        Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLVector<int> id);
        Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteHistoryAsync(TLInputPeerBase peer, int offset);
        Task<MTProtoResponse<TLContactsLink>> DeleteContactAsync(TLInputUserBase id);
        Task<MTProtoResponse<TLMessagesAffectedHistory>> ReadHistoryAsync(TLInputPeerBase peer, int maxId);
        Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadMessageContentsAsync(TLVector<int> id);
        Task<MTProtoResponse<TLMessagesChatFull>> GetFullChatAsync(int chatId);
        Task<MTProtoResponse<TLUserFull>> GetFullUserAsync(TLInputUserBase id);
        Task<MTProtoResponse<TLVector<TLUserBase>>> GetUsersAsync(TLVector<TLInputUserBase> id);

        Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, bool typing);
        Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action);

        Task<MTProtoResponse<TLContactsContactsBase>> GetContactsAsync(string hash);
        Task<MTProtoResponse<TLContactsImportedContacts>> ImportContactsAsync(TLVector<TLInputContactBase> contacts, bool replace);

        Task<MTProtoResponse<bool>> BlockAsync(TLInputUserBase id);
        Task<MTProtoResponse<bool>> UnblockAsync(TLInputUserBase id);
        Task<MTProtoResponse<TLContactsBlockedBase>> GetBlockedAsync(int offset, int limit);

        Task<MTProtoResponse<TLUserBase>> UpdateProfileAsync(string firstName, string lastName);
        Task<MTProtoResponse<bool>> UpdateStatusAsync(bool offline);

        Task<MTProtoResponse<TLUploadFile>> GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit);
        Task<MTProtoResponse<TLUploadFile>> GetFileAsync(TLInputFileLocationBase location, int offset, int limit);
        Task<MTProtoResponse<bool>> SaveFilePartAsync(long fileId, int filePart, byte[] bytes);
        Task<MTProtoResponse<bool>> SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes);

        Task<MTProtoResponse<TLPeerNotifySettingsBase>> GetNotifySettingsAsync(TLInputNotifyPeerBase peer);
        Task<MTProtoResponse<bool>> UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings);
        Task<MTProtoResponse<bool>> ResetNotifySettingsAsync();

        // didn't work
        //void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null);
        Task<MTProtoResponse<TLPhotosPhoto>> UploadProfilePhotoAsync(TLInputFile file, string caption, TLInputGeoPointBase geoPoint, TLInputPhotoCropBase crop);
        Task<MTProtoResponse<TLPhotoBase>> UpdateProfilePhotoAsync(TLInputPhotoBase id, TLInputPhotoCropBase crop);

        Task<MTProtoResponse<TLServerDHInnerData>> GetDHConfigAsync(int version, int randomLength);
        Task<MTProtoResponse<TLEncryptedChatBase>> RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] g_a);
        Task<MTProtoResponse<TLEncryptedChatBase>> AcceptEncryptionAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint);
        Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedAsync(TLInputEncryptedChat peer, long randomId, byte[] data);
        Task<MTProtoResponse<TLMessagesSentEncryptedFile>> SendEncryptedFileAsync(TLInputEncryptedChat peer, long randomId, byte[] data, TLInputEncryptedFileBase file);
        Task<MTProtoResponse<bool>> ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int maxDate);
        Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedServiceAsync(TLInputEncryptedChat peer, long randomId, byte[] data);
        Task<MTProtoResponse<bool>> DiscardEncryptionAsync(int chatId);
        Task<MTProtoResponse<bool>> SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing);

        void GetConfigInformationAsync(Action<string> callback);
        void GetTransportInformationAsync(Action<string> callback);
        Task<MTProtoResponse<TLPhotosPhotosBase>> GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit);
        Task<MTProtoResponse<TLNearestDC>> GetNearestDCAsync();
        Task<MTProtoResponse<TLHelpSupport>> GetSupportAsync();

        Task<MTProtoResponse<bool>> ResetAuthorizationsAsync();
        void SetInitState();

        Task<MTProtoResponse<TLPong>> PingAsync(long pingId);
        Task<MTProtoResponse<TLPong>> PingDelayDisconnectAsync(long pingId, int disconnectDelay);

        Task<MTProtoResponse<TLContactsFound>> SearchAsync(string q, int limit);
        Task<MTProtoResponse<bool>> CheckUsernameAsync(string username);
        Task<MTProtoResponse<TLUserBase>> UpdateUsernameAsync(string username);
        Task<MTProtoResponse<TLAccountDaysTTL>> GetAccountTTLAsync();
        Task<MTProtoResponse<bool>> SetAccountTTLAsync(TLAccountDaysTTL ttl);
        Task<MTProtoResponse<bool>> DeleteAccountTTLAsync(string reason);
        Task<MTProtoResponse<TLAccountPrivacyRules>> GetPrivacyAsync(TLInputPrivacyKeyBase key);
        Task<MTProtoResponse<TLInputPrivacyRuleBase>> SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules);
        Task<MTProtoResponse<TLVector<TLContactStatus>>> GetStatusesAsync();
        void UpdateTransportInfoAsync(int dcId, string dcIpAddress, int dcPort);

        Task<MTProtoResponse<TLContactsResolvedPeer>> ResolveUsernameAsync(string username);
        Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber);
        Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode);
        Task<MTProtoResponse<TLVector<TLWallPaperBase>>> GetWallpapersAsync();
        // NO MORE SUPPORTED: Task<MTProtoResponse<TLAllStickersBase>> GetAllStickersAsync(string hash);

        Task<MTProtoResponse<bool>> UpdateDeviceLockedAsync(int period);

        void GetSendingQueueInfoAsync(Action<string> callback);
        void GetSyncErrorsAsync(Action<ExceptionInfo, IList<ExceptionInfo>> callback);
        Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLVector<int> id);

        // messages
        Task<MTProtoResponse<bool>> ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason);
        Task<MTProtoResponse<bool>> ReportSpamAsync(TLInputPeerBase peer);
        Task<MTProtoResponse<TLMessage>> SendMessageAsync(TLMessage message);
        Task<MTProtoResponse<TLUpdatesBase>> SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message);
        Task<MTProtoResponse<TLUpdatesBase>> StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message);
        // NO MORE SUPPORTED: Task<MTProtoResponse<TLUpdatesBase>> SendBroadcastAsync(TLVector<TLInputUserBase> contacts, TLInputMediaBase inputMedia, TLMessageBase message);
        Task<MTProtoResponse<TLUpdatesBase>> ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message);
        Task<MTProtoResponse<TLUpdatesBase>> ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<int> id, IList<TLMessage> messages);
        Task<MTProtoResponse<TLUpdatesBase>> CreateChatAsync(TLVector<TLInputUserBase> users, string title);
        Task<MTProtoResponse<TLUpdatesBase>> EditChatTitleAsync(int chatId, string title);
        Task<MTProtoResponse<TLUpdatesBase>> EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo);
        Task<MTProtoResponse<TLUpdatesBase>> AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit);
        Task<MTProtoResponse<TLUpdatesBase>> DeleteChatUserAsync(int chatId, TLInputUserBase userId);
        Task<MTProtoResponse<TLMessageMediaBase>> GetWebPagePreviewAsync(string message);
        Task<MTProtoResponse<TLExportedChatInviteBase>> ExportChatInviteAsync(int chatId);
        Task<MTProtoResponse<TLChatInviteBase>> CheckChatInviteAsync(string hash);
        Task<MTProtoResponse<TLUpdatesBase>> ImportChatInviteAsync(string hash);
        Task<MTProtoResponse<TLMessagesStickerSet>> GetStickerSetAsync(TLInputStickerSetBase stickerset);
        Task<MTProtoResponse<bool>> InstallStickerSetAsync(TLInputStickerSetBase stickerset);
        Task<MTProtoResponse<bool>> UninstallStickerSetAsync(TLInputStickerSetBase stickerset);

        // channels
        Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id);
        Task<MTProtoResponse<TLMessagesChatFull>> UpdateChannelAsync(int channelId);
        Task<MTProtoResponse<bool>> EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role);
        Task<MTProtoResponse<TLUpdatesBase>> KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, bool kicked);
        Task<MTProtoResponse<TLChannelsChannelParticipant>> GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId);
        Task<MTProtoResponse<TLChannelsChannelParticipants>> GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit);
        Task<MTProtoResponse<TLUpdatesBase>> EditTitleAsync(TLChannel channel, string title);
        Task<MTProtoResponse<bool>> EditAboutAsync(TLChannel channel, string about);
        Task<MTProtoResponse<TLUpdatesBase>> EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo);
        Task<MTProtoResponse<TLUpdatesBase>> JoinChannelAsync(TLChannel channel);
        Task<MTProtoResponse<TLUpdatesBase>> LeaveChannelAsync(TLChannel channel);
        Task<MTProtoResponse<TLUpdatesBase>> DeleteChannelAsync(TLChannel channel);
        Task<MTProtoResponse<TLUpdatesBase>> InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users);
        Task<MTProtoResponse<TLMessagesChatFull>> GetFullChannelAsync(TLInputChannelBase channel);
        Task<MTProtoResponse<TLUpdatesBase>> CreateChannelAsync(int flags, string title, string about);
        Task<MTProtoResponse<TLExportedChatInviteBase>> ExportInviteAsync(TLInputChannelBase channel);
        Task<MTProtoResponse<bool>> CheckUsernameAsync(TLInputChannelBase channel, string username);
        Task<MTProtoResponse<bool>> UpdateUsernameAsync(TLInputChannelBase channel, string username);
        // TODO: Task<MTProtoResponse<TLMessagesDialogsBase>> GetChannelDialogsAsync(int offset, int limit);
        // TODO: Task<MTProtoResponse<TLMessagesMessagesBase>> GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, int offsetId, int addOffset, int limit, int maxId, int minId);
        Task<MTProtoResponse<bool>> ReadHistoryAsync(TLChannel channel, int maxId);
        Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id);

        // admins
        Task<MTProtoResponse<TLUpdatesBase>> ToggleChatAdminsAsync(int chatId, bool enabled);
        Task<MTProtoResponse<bool>> EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin);
        // TODO: Probably deprecated: Task<MTProtoResponse<TLUpdatesBase>> DeactivateChatAsync(int chatId, bool enabled);
        Task<MTProtoResponse<TLUpdatesBase>> MigrateChatAsync(int chatId);


        // account
        Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason);
        Task<MTProtoResponse<TLAccountAuthorizations>> GetAuthorizationsAsync();
        Task<MTProtoResponse<bool>> ResetAuthorizationAsync(long hash);
        Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync();
        Task<MTProtoResponse<TLAccountPasswordSettings>> GetPasswordSettingsAsync(byte[] currentPasswordHash);
        Task<MTProtoResponse<bool>> UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings);
        Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash);
        Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync();
        Task<MTProtoResponse<TLAuthAuthorization>> RecoverPasswordAsync(string code);

        // help
        Task<MTProtoResponse<TLHelpAppChangelogBase>> GetAppChangelogAsync(string deviceModel, string systemVersion, string appVersion, string langCode);

        // encrypted chats
        void RekeyAsync(TLEncryptedChatBase chat, Action<long> callback);

        // background task
        Task<MTProtoResponse<TLObject>> SendActionsAsync(List<TLObject> actions);
        void ClearQueue();
    }
}
