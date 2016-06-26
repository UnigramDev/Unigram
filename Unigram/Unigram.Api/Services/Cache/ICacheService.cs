using System;
using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache
{
    public interface ICacheService
    {
        ExceptionInfo LastSyncMessageException { get; }

        void Commit();
        bool TryCommit();
        void SaveSnapshot(string toDirectoryName);
        void LoadSnapshot(string fromDirectoryName);
        //event EventHandler<DialogAddedEventArgs> DialogAdded;
        //event EventHandler<TopMessageUpdatedEventArgs> TopMessageUpdated;

        TLUserBase GetUser(int id);
        TLUserBase GetUser(TLUserProfilePhoto photo);
        TLMessageBase GetMessage(int id, int? channelId = null); 
        TLMessageBase GetMessage(long randomId);
        TLMessageBase GetMessage(TLWebPageBase webPage);
        TLDialog GetDialog(TLMessage message);
        TLDialog GetDialog(TLPeerBase peer);
        // TODO: Secrets: TLDialogBase GetEncryptedDialog(int chatId);

        TLChat GetChat(TLChatPhoto chatPhoto);
        TLChannel GetChannel(TLChatPhoto channelPhoto);
        TLChatBase GetChat(int id);
        // DEPRECATED: TLBroadcastChat GetBroadcast(int id);

        IList<TLMessageBase> GetMessages();
        IList<TLMessageBase> GetSendingMessages();
        IList<TLMessageBase> GetResendingMessages(); 

        void GetHistoryAsync(int currentUserId, TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(int currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        //IList<TLMessageBase> GetUnreadHistory(int currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(int dialogId);
        // TODO: Secrets: IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, int limit = Constants.CachedMessagesCount);
        // TODO: Secrets: IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, long randomId, int limit = Constants.CachedMessagesCount);
        void GetDialogsAsync(Action<IList<TLDialog>> callback);
        IList<TLDialog> GetDialogs();
        void GetContactsAsync(Action<IList<TLUserBase>> callback);

        List<TLUserBase> GetContacts();
        List<TLUserBase> GetUsersForSearch(IList<TLDialog> nonCachedDialogs);
        List<TLUserBase> GetUsers();
        List<TLChatBase> GetChats();
        void GetChatsAsync(Action<IList<TLChatBase>> callback);


        void ClearAsync(Action callback = null);
        void SyncMessage(TLMessageBase message, TLPeerBase peer, Action<TLMessageBase> callback);
        void SyncSendingMessage(TLMessage message, TLMessageBase previousMessage, TLPeerBase peer, Action<TLMessage> callback);
        void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, TLPeerBase peer, Action<IList<TLMessage>> callback);
        void SyncSendingMessageId(long randomId, int id, Action<TLMessage> callback);
        void SyncMessages(TLMessagesMessagesBase messages, TLPeerBase peer, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesMessagesBase> callback);
        void SyncEditedMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback);
        void SyncDialogs(TLMessagesDialogsBase dialogs, Action<TLMessagesDialogsBase> callback);
        void SyncChannelDialogs(TLMessagesDialogsBase dialogs, Action<TLMessagesDialogsBase> callback);
        void MergeMessagesAndChannels(TLMessagesDialogsBase dialogs);
        void SyncUser(TLUserBase user, Action<TLUserBase> callback);
        void SyncUser(TLUserFull userFull, Action<TLUserFull> callback);
        void SyncUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void AddUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void SyncUsersAndChats(TLVector<TLUserBase> users, TLVector<TLChatBase> chats, Action<Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>> callback);
        void SyncUserLink(TLContactsLink link, Action<TLContactsLink> callback);
        void SyncContacts(TLContactsContactsBase contacts, Action<TLContactsContactsBase> callback);
        void SyncContacts(TLContactsImportedContacts contacts, Action<TLContactsImportedContacts> callback);

        void DeleteDialog(TLDialog dialog);
        void DeleteMessages(TLVector<int> ids);
        void DeleteChannelMessages(int channelId, TLVector<int> ids);
        void DeleteMessages(TLPeerBase peer, TLMessageBase lastItem, TLVector<int> messages);
        void DeleteMessages(TLVector<long> ids);
        // TODO: Secrets: void DeleteDecryptedMessages(TLVector<long> ids);
        // TODO: Secrets: void ClearDecryptedHistoryAsync(int chatId);
        // DEPRECATED: void ClearBroadcastHistoryAsync(int chatId);

        // TODO: No idea: void SyncStatedMessage(TLStatedMessageBase statedMessage, Action<TLStatedMessageBase> callback);
        // TODO: No idea: void SyncStatedMessages(TLStatedMessagesBase statedMessages, Action<TLStatedMessagesBase> callback);

        void CheckDisabledFeature(string featureKey, Action callback, Action<TLDisabledFeature> faultCallback = null);
        void CheckDisabledFeature(TLObject with, string featurePMMessage, string featureChatMessage, string featureBigChatMessage, Action callback, Action<TLDisabledFeature> faultCallback);
        void GetConfigAsync(Action<TLConfig> config);
        void SetConfig(TLConfig config);
        void SyncChat(TLMessagesChatFull messagesChatFull, Action<TLMessagesChatFull> callback);
        void AddChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback);
        // NO MORE SUPPORTED: void SyncBroadcast(TLBroadcastChat broadcast, Action<TLBroadcastChat> callback);

        // TODO: Secrets: TLEncryptedChatBase GetEncryptedChat(int id);
        // TODO: Secrets: void SyncEncryptedChat(TLEncryptedChatBase encryptedChat, Action<TLEncryptedChatBase> callback);
        // TODO: Secrets: void SyncDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, Action<TLDecryptedMessageBase> callback);
        // TODO: Secrets: void SyncSendingDecryptedMessage(int chatId, int date, long randomId, Action<TLDecryptedMessageBase> callback);

        void Initialize();

        void SyncDifference(TLUpdatesDifference difference, Action<TLUpdatesDifference> result, IList<ExceptionInfo> exceptions);
        void SyncDifferenceWithoutUsersAndChats(TLUpdatesDifference difference, Action<TLUpdatesDifference> result, IList<ExceptionInfo> exceptions);
        void SyncStatuses(TLVector<TLContactStatus> contacts, Action<TLVector<TLContactStatus>> callback);
        void DeleteUser(int id);
        void DeleteChat(int id);
    }

    public class ExceptionInfo : TLObject
    {
        public Exception Exception { get; set; }

        public DateTime Timestamp { get; set; }

        public string Caption { get; set; }

        public override string ToString()
        {
            return string.Format("Caption={2}\nTimestamp={0}\nException={1}", Timestamp, Exception, Caption);
        }
    }
}
