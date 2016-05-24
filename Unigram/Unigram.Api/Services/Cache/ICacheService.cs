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

        TLUserBase GetUser(TLInt id);
        TLUserBase GetUser(TLUserProfilePhoto photo);
        TLMessageBase GetMessage(TLInt id, TLInt channelId = null); 
        TLMessageBase GetMessage(TLLong randomId);
        TLMessageBase GetMessage(TLWebPageBase webPage);
        TLDialog GetDialog(TLMessageCommon message);
        TLDialog GetDialog(TLPeerBase peer);
        TLDialogBase GetEncryptedDialog(TLInt chatId);

        TLChat GetChat(TLChatPhoto chatPhoto);
        TLChannel GetChannel(TLChatPhoto channelPhoto);
        TLChatBase GetChat(TLInt id);
        TLBroadcastChat GetBroadcast(TLInt id);

        IList<TLMessageBase> GetMessages();
        IList<TLMessageBase> GetSendingMessages();
        IList<TLMessageBase> GetResendingMessages(); 

        void GetHistoryAsync(TLInt currentUserId, TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(TLInt currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        //IList<TLMessageBase> GetUnreadHistory(TLInt currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount);
        IList<TLMessageBase> GetHistory(int dialogId);
        IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, int limit = Constants.CachedMessagesCount);
        IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogId, long randomId, int limit = Constants.CachedMessagesCount);
        void GetDialogsAsync(Action<IList<TLDialogBase>> callback);
        IList<TLDialogBase> GetDialogs();
        void GetContactsAsync(Action<IList<TLUserBase>> callback);

        List<TLUserBase> GetContacts();
        List<TLUserBase> GetUsersForSearch(IList<TLDialogBase> nonCachedDialogs);
        List<TLUserBase> GetUsers();
        List<TLChatBase> GetChats();
        void GetChatsAsync(Action<IList<TLChatBase>> callback);


        void ClearAsync(Action callback = null);
        void SyncMessage(TLMessageBase message, TLPeerBase peer, Action<TLMessageBase> callback);
        void SyncSendingMessage(TLMessage message, TLMessageBase previousMessage, TLPeerBase peer, Action<TLMessage> callback);
        void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, TLPeerBase peer, Action<IList<TLMessage>> callback);
        void SyncSendingMessageId(TLLong randomId, TLInt id, Action<TLMessage> callback);
        void SyncMessages(TLMessagesBase messages, TLPeerBase peer, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesBase> callback);
        void SyncDialogs(TLDialogsBase dialogs, Action<TLDialogsBase> callback);
        void SyncChannelDialogs(TLDialogsBase dialogs, Action<TLDialogsBase> callback);
        void MergeMessagesAndChannels(TLDialogsBase dialogs);
        void SyncUser(TLUserBase user, Action<TLUserBase> callback);
        void SyncUser(TLUserFull userFull, Action<TLUserFull> callback);
        void SyncUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void AddUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback);
        void SyncUsersAndChats(TLVector<TLUserBase> users, TLVector<TLChatBase> chats, Action<WindowsPhone.Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>> callback);
        void SyncUserLink(TLLinkBase link, Action<TLLinkBase> callback);
        void SyncContacts(TLContactsBase contacts, Action<TLContactsBase> callback);
        void SyncContacts(TLImportedContacts contacts, Action<TLImportedContacts> callback);

        void DeleteDialog(TLDialogBase dialog);
        void DeleteMessages(TLVector<TLInt> ids);
        void DeleteChannelMessages(TLInt channelId, TLVector<TLInt> ids);
        void DeleteMessages(TLPeerBase peer, TLMessageBase lastItem, TLVector<TLInt> messages);
        void DeleteMessages(TLVector<TLLong> ids);
        void DeleteDecryptedMessages(TLVector<TLLong> ids);
        void ClearDecryptedHistoryAsync(TLInt chatId);
        void ClearBroadcastHistoryAsync(TLInt chatId);

        void SyncStatedMessage(TLStatedMessageBase statedMessage, Action<TLStatedMessageBase> callback);
        void SyncStatedMessages(TLStatedMessagesBase statedMessages, Action<TLStatedMessagesBase> callback);

        void CheckDisabledFeature(string featureKey, Action callback, Action<TLDisabledFeature> faultCallback = null);
        void CheckDisabledFeature(TLObject with, string featurePMMessage, string featureChatMessage, string featureBigChatMessage, Action callback, Action<TLDisabledFeature> faultCallback);
        void GetConfigAsync(Action<TLConfig> config);
        void SetConfig(TLConfig config);
        void SyncChat(TLMessagesChatFull messagesChatFull, Action<TLMessagesChatFull> callback);
        void AddChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback);
        void SyncBroadcast(TLBroadcastChat broadcast, Action<TLBroadcastChat> callback);

        TLEncryptedChatBase GetEncryptedChat(TLInt id);
        void SyncEncryptedChat(TLEncryptedChatBase encryptedChat, Action<TLEncryptedChatBase> callback);
        void SyncDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, Action<TLDecryptedMessageBase> callback);
        void SyncSendingDecryptedMessage(TLInt chatId, TLInt date, TLLong randomId, Action<TLDecryptedMessageBase> callback);
        
        void Init();

        void SyncDifference(TLDifference difference, Action<TLDifference> result, IList<ExceptionInfo> exceptions);
        void SyncDifferenceWithoutUsersAndChats(TLDifference difference, Action<TLDifference> result, IList<ExceptionInfo> exceptions);
        void SyncStatuses(TLVector<TLContactStatusBase> contacts, Action<TLVector<TLContactStatusBase>> callback);
        void DeleteUser(TLInt id);
        void DeleteChat(TLInt id);
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
