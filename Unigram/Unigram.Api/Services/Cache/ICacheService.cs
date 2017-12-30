using System;
using System.Collections.Generic;
using System.Diagnostics;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Contacts;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Updates;

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
        TLUserFull GetFullUser(int? id);

        TLMessageBase GetMessage(int? id, int? channelId = null); 
        TLMessageBase GetMessage(long? randomId);
        TLMessageBase GetMessage(TLWebPageBase webPage);
        // TODO: Encrypted TLDecryptedMessageBase GetDecryptedMessage(int? chatId, long? randomId);
        TLDialog GetDialog(TLMessageCommonBase message);
        TLDialog GetDialog(TLPeerBase peer);
        // TODO: Encrypted TLDialog GetEncryptedDialog(int? chatId);

        TLChat GetChat(TLChatPhoto chatPhoto);
        TLChannel GetChannel(string username);
        TLChannel GetChannel(TLChatPhoto channelPhoto);
        TLChatBase GetChat(int? id);
        TLChatFullBase GetFullChat(int? id);

        IList<TLMessageBase> GetMessages();
        IList<TLMessageBase> GetSendingMessages();
        IList<TLMessageBase> GetResendingMessages(); 

        void GetHistoryAsync(TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount, Func<TLMessageBase, bool> predicate = null);
        IList<TLMessageBase> GetHistory(TLPeerBase peer, int limit = Constants.CachedMessagesCount, Func<TLMessageBase, bool> predicate = null);
        //IList<TLMessageBase> GetUnreadHistory(int? currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(int dialogId);
        // TODO: Encrypted IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, int limit = Constants.CachedMessagesCount);
        // TODO: Encrypted IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, long randomId, int limit = Constants.CachedMessagesCount);
        // TODO: Encrypted IList<TLDecryptedMessageBase> GetUnreadDecryptedHistory(int dialogId);
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
        void SyncSendingMessage(TLMessageCommonBase message, TLMessageBase previousMessage, Action<TLMessageCommonBase> callback);
        void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, Action<IList<TLMessage>> callback);
        void SyncSendingMessageId(long randomId, int id, Action<TLMessageCommonBase> callback);
        void SyncPeerMessages(TLPeerBase peer, TLMessagesMessagesBase messages, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesMessagesBase> callback);
        void AddMessagesToContext(TLMessagesMessagesBase messages, Action<TLMessagesMessagesBase> callback);
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

        void ClearDialog(TLPeerBase peer);
        void DeleteDialog(TLDialog dialog);
        void DeleteMessages(TLVector<int> ids);
        void DeleteChannelMessages(int channelId, TLVector<int> ids);
        void DeleteChannelMessages(int channelId, int minId);
        void DeleteMessages(TLPeerBase peer, TLMessageBase lastItem, TLVector<int> messages);
        void DeleteMessages(TLVector<long> ids);
        // TODO: Encrypted void DeleteDecryptedMessages(TLVector<long> ids);
        // TODO: Encrypted void ClearDecryptedHistoryAsync(int? chatId);

        // TODO: No idea void SyncStatedMessage(TLStatedMessageBase statedMessage, Action<TLStatedMessageBase> callback);
        // TODO: No idea void SyncStatedMessages(TLStatedMessagesBase statedMessages, Action<TLStatedMessagesBase> callback);

        void CheckDisabledFeature(string featureKey, Action callback, Action<TLDisabledFeature> faultCallback = null);
        void CheckDisabledFeature(TLObject with, string featurePMMessage, string featureChatMessage, string featureBigChatMessage, Action callback, Action<TLDisabledFeature> faultCallback);
        void GetConfigAsync(Action<TLConfig> config);
        TLConfig Config { get; }
        TLConfig GetConfig();
        void SetConfig(TLConfig config);
        void ClearConfigImportAsync();
        void SyncChat(TLMessagesChatFull messagesChatFull, Action<TLMessagesChatFull> callback);
        void AddChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback);

        // TODO: Encrypted TLEncryptedChatBase GetEncryptedChat(int? id);
        // TODO: Encrypted void SyncEncryptedChat(TLEncryptedChatBase encryptedChat, Action<TLEncryptedChatBase> callback);
        // TODO: Encrypted void SyncDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, Action<TLDecryptedMessageBase> callback);
        // TODO: Encrypted void SyncDecryptedMessages(IList<Tuple<TLDecryptedMessageBase, TLObject>> tuples, TLEncryptedChatBase peer, Action<IList<Tuple<TLDecryptedMessageBase, TLObject>>> callback);
        // TODO: Encrypted void SyncSendingDecryptedMessage(int? chatId, int? date, long? randomId, Action<TLDecryptedMessageBase> callback);

        void Init();

        void SyncDifference(TLUpdatesDifference difference, Action<TLUpdatesDifference> result, IList<ExceptionInfo> exceptions);
        void SyncDifferenceWithoutUsersAndChats(TLUpdatesDifference difference, Action<TLUpdatesDifference> result, IList<ExceptionInfo> exceptions);
        void SyncStatuses(TLVector<TLContactStatus> contacts, Action<TLVector<TLContactStatus>> callback);
        void DeleteUser(int? id);
        void DeleteChat(int? id);
        void DeleteUserFull(int? id);
        void DeleteChatFull(int? id);
        void DeleteUserHistory(TLPeerChannel channel, TLPeerUser peer);

        void ClearDialog(TLPeerBase peer, int availableMinId);
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
