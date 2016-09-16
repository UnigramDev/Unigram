using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache
{
    public interface ICacheService
    {
        ExceptionInfo LastSyncMessageException { get; }

        void CompressAsync(Action callback);
        void Commit();
        bool TryCommit();
        void SaveSnapshot(string toDirectoryName);
        void SaveTempSnapshot(string toDirectoryName);
        void LoadSnapshot(string fromDirectoryName);
        //event EventHandler<DialogAddedEventArgs> DialogAdded;
        //event EventHandler<TopMessageUpdatedEventArgs> TopMessageUpdated;

        TLUserBase GetUser(int? id);
        TLUserBase GetUser(string username);
        TLUserBase GetUser(TLUserProfilePhoto photo);
        TLMessageBase GetMessage(int? id, int? channelId = null); 
        TLMessageBase GetMessage(long? randomId);
        TLMessageBase GetMessage(TLWebPageBase webPage);
        TLDecryptedMessageBase GetDecryptedMessage(int? chatId, long? randomId);
        TLDialog GetDialog(TLMessage message);
        TLDialog GetDialog(TLPeerBase peer);
        TLDialog GetEncryptedDialog(int? chatId);

        TLChat GetChat(TLChatPhoto chatPhoto);
        TLChannel GetChannel(string username);
        TLChannel GetChannel(TLChatPhoto channelPhoto);
        TLChatBase GetChat(int? id);
        TLBroadcastChat GetBroadcast(int? id);

        IList<TLMessageBase> GetMessages();
        IList<TLMessageBase> GetSendingMessages();
        IList<TLMessageBase> GetResendingMessages(); 

        void GetHistoryAsync(TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(TLPeerBase peer, int maxId, int limit = Constants.CachedMessagesCount);
        //IList<TLMessageBase> GetUnreadHistory(int? currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(int dialogId);
        IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, int limit = Constants.CachedMessagesCount);
        IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, long randomId, int limit = Constants.CachedMessagesCount);
        IList<TLDecryptedMessageBase> GetUnreadDecryptedHistory(int dialogId);
        void GetDialogsAsync(Action<IList<TLDialog>> callback);
        IList<TLDialog> GetDialogs();
        void GetContactsAsync(Action<IList<TLUserBase>> callback);

        List<TLUserBase> GetContacts();
        List<TLUserBase> GetUsersForSearch(IList<TLDialog> nonCachedDialogs);
        List<TLUserBase> GetUsers();
        List<TLChatBase> GetChats();
        void GetChatsAsync(Action<IList<TLChatBase>> callback);


        void ClearAsync(Action callback = null);
        void SyncMessage(TLMessageBase message, Action<TLMessageBase> callback);
        void SyncMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback);
        void SyncEditedMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback);
        void SyncSendingMessage(TLMessage message, TLMessageBase previousMessage, Action<TLMessage> callback);
        void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, Action<IList<TLMessage>> callback);
        void SyncSendingMessageId(long? randomId, int? id, Action<TLMessage> callback);
        void SyncPeerMessages(TLPeerBase peer, TLMessagesBase messages, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesBase> callback);
        void AddMessagesToContext(TLMessagesBase messages, Action<TLMessagesBase> callback);
        void SyncDialogs(Stopwatch stopwatch, TLDialogsBase dialogs, Action<TLDialogsBase> callback);
        void SyncChannelDialogs(TLDialogsBase dialogs, Action<TLDialogsBase> callback);
        void MergeMessagesAndChannels(TLDialogsBase dialogs);
        void SyncUser(TLUserBase user, Action<TLUserBase> callback);
        void SyncUser(TLUserFull userFull, Action<TLUserFull> callback);
        void SyncUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void AddUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void SyncUsersAndChats(TLVector<TLUserBase> users, TLVector<TLChatBase> chats, Action<Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>> callback);
        void SyncUserLink(TLLinkBase link, Action<TLLinkBase> callback);
        void SyncContacts(TLContactsBase contacts, Action<TLContactsBase> callback);
        void SyncContacts(TLImportedContacts contacts, Action<TLImportedContacts> callback);

        void ClearDialog(TLPeerBase peer);
        void DeleteDialog(TLDialog dialog);
        void DeleteMessages(TLVector<int> ids);
        void DeleteChannelMessages(int? channelId, TLVector<int> ids);
        void DeleteMessages(TLPeerBase peer, TLMessageBase lastItem, TLVector<int> messages);
        void DeleteMessages(TLVector<long> ids);
        void DeleteDecryptedMessages(TLVector<long> ids);
        void ClearDecryptedHistoryAsync(int? chatId);
        void ClearBroadcastHistoryAsync(int? chatId);

        void SyncStatedMessage(TLStatedMessageBase statedMessage, Action<TLStatedMessageBase> callback);
        void SyncStatedMessages(TLStatedMessagesBase statedMessages, Action<TLStatedMessagesBase> callback);

        void CheckDisabledFeature(string featureKey, Action callback, Action<TLDisabledFeature> faultCallback = null);
        void CheckDisabledFeature(TLObject with, string featurePMMessage, string featureChatMessage, string featureBigChatMessage, Action callback, Action<TLDisabledFeature> faultCallback);
        void GetConfigAsync(Action<TLConfig> config);
        TLConfig GetConfig();
        void SetConfig(TLConfig config);
        void ClearConfigImportAsync();
        void SyncChat(TLMessagesChatFull messagesChatFull, Action<TLMessagesChatFull> callback);
        void AddChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback);
        void SyncBroadcast(TLBroadcastChat broadcast, Action<TLBroadcastChat> callback);

        TLEncryptedChatBase GetEncryptedChat(int? id);
        void SyncEncryptedChat(TLEncryptedChatBase encryptedChat, Action<TLEncryptedChatBase> callback);
        void SyncDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, Action<TLDecryptedMessageBase> callback);
        void SyncDecryptedMessages(IList<Tuple<TLDecryptedMessageBase, TLObject>> tuples, TLEncryptedChatBase peer, Action<IList<Tuple<TLDecryptedMessageBase, TLObject>>> callback);
        void SyncSendingDecryptedMessage(int? chatId, int? date, long? randomId, Action<TLDecryptedMessageBase> callback);
        
        void Init();

        void SyncDifference(TLDifference difference, Action<TLDifference> result, IList<ExceptionInfo> exceptions);
        void SyncDifferenceWithoutUsersAndChats(TLDifference difference, Action<TLDifference> result, IList<ExceptionInfo> exceptions);
        void SyncStatuses(TLVector<TLContactStatusBase> contacts, Action<TLVector<TLContactStatusBase>> callback);
        void DeleteUser(int? id);
        void DeleteChat(int? id);
        void DeleteUserHistory(TLPeerChannel channel, TLPeerUser peer);
    }

    public class ExceptionInfo
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
