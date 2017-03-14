using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Security;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;


namespace Telegram.Api.Services.Cache
{
    public class InMemoryDatabase : IDisposable
    {
        private volatile bool _isOpened;

        public const string DialogsMTProtoFileName = "dialogs.dat";

        public const string UsersMTProtoFileName = "users.dat";

        public const string ChatsMTProtoFileName = "chats.dat";

        // TODO: Encrypted public const string EncryptedChatsMTProtoFileName = "encryptedChats.dat";

        public const string TempDialogsMTProtoFileName = "temp_dialogs.dat";

        public const string TempUsersMTProtoFileName = "temp_users.dat";

        public const string TempChatsMTProtoFileName = "temp_chats.dat";

        // TODO: Encrypted public const string TempEncryptedChatsMTProtoFileName = "temp_encryptedChats.dat";

        private readonly object _dialogsFileSyncRoot = new object();

        private readonly object _usersSyncRoot = new object();

        private readonly object _chatsSyncRoot = new object();

        private readonly object _encryptedChatsSyncRoot = new object();

        public Context<TLMessageBase> MessagesContext = new Context<TLMessageBase>();

        public Context<Context<TLMessageBase>> ChannelsContext = new Context<Context<TLMessageBase>>();

        // TODO: Encrypted public Context<TLDecryptedMessageBase> DecryptedMessagesContext = new Context<TLDecryptedMessageBase>(); 

        public Context<TLMessageBase> RandomMessagesContext = new Context<TLMessageBase>(); 

        public Context<TLDialog> DialogsContext = new Context<TLDialog>();

        public Context<TLUserBase> UsersContext = new Context<TLUserBase>();

        public Context<TLChatBase> ChatsContext = new Context<TLChatBase>();

        public Context<TLEncryptedChatBase> EncryptedChatsContext = new Context<TLEncryptedChatBase>(); 

        private readonly object _dialogsSyncRoot = new object();
        
        public ObservableCollection<TLDialog> Dialogs { get; set; }

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IDisposable _commitSubscription;

        public volatile bool HasChanges;

        public InMemoryDatabase(ITelegramEventAggregator eventAggregator)
        {
            var commitEvents = Observable.FromEventPattern<EventHandler, System.EventArgs>(
                keh => { CommitInvoked += keh; },
                keh => { CommitInvoked -= keh; });

            _commitSubscription = commitEvents
                .Throttle(TimeSpan.FromSeconds(Constants.CommitDBInterval))
                .Subscribe(e => CommitInternal());

            //commitEvents.Publish()

            Dialogs = new ObservableCollection<TLDialog>();
            _eventAggregator = eventAggregator;
        }

        public void AddDialog(TLDialog dialog)
        {
            if (dialog != null)
            {
                DialogsContext[dialog.Index] = dialog;

                var topMessage = (TLMessageCommonBase) dialog.TopMessageItem;

                // add in desc order by Date
                var isAdded = false;

                lock (_dialogsSyncRoot)
                {
                    for (var i = 0; i < Dialogs.Count; i++)
                    {
                        var d = Dialogs[i] as TLDialog;
                        // TODO: Encrypted var ed = Dialogs[i] as TLEncryptedDialog;
                        if (d != null)
                        {
                            var currentTopMessage = (TLMessageCommonBase)d.TopMessageItem;

                            if (currentTopMessage != null
                                && currentTopMessage.Date < topMessage.Date)
                            {
                                isAdded = true;
                                Dialogs.Insert(i, dialog);
                                break;
                            }
                        }
                        // TODO: Encrypted 
                        //else if (ed != null)
                        //{
                        //    if (ed.TopMessage != null
                        //        && ed.TopMessage.Date.Value < topMessage.Date.Value)
                        //    {
                        //        isAdded = true;
                        //        Dialogs.Insert(i, dialog);
                        //        break;
                        //    }
                        //}
                    }
                    if (!isAdded)
                    {
                        Dialogs.Add(dialog);
                    }
                }

                //sync topMessage
                AddMessageToContext(topMessage);
                //MessagesContext[topMessage.Index] = topMessage;

            }
        }

        //private TLMessageBase GetMessageFromContext(long commonId)
        //{
        //    if (RandomMessagesContext.ContainsKey(commonId))
        //    {
        //        return RandomMessagesContext[commonId];
        //    }

        //    if (MessagesContext.ContainsKey(commonId))
        //    {
        //        return MessagesContext[commonId];
        //    }

        //    return null;
        //}

        public void RemoveMessageFromContext(TLMessageBase message)
        {
            if (message.Id != 0)
            {
                if (TLUtils.IsChannelMessage(message, out TLPeerChannel peerChannel))
                {
                    var channelContext = ChannelsContext[peerChannel.Id];
                    if (channelContext != null)
                    {
                        channelContext.Remove(message.Id);
                    }
                }
                else
                {
                    MessagesContext.Remove(message.Id);
                }
            }
            if ((message.RandomId ?? 0) != 0)
            {
                RandomMessagesContext.Remove(message.RandomId.Value);
            }
        }

        // TODO: Encrypted 
        //public void RemoveDecryptedMessageFromContext(TLDecryptedMessageBase message)
        //{
        //    if (message.RandomIndex != 0)
        //    {
        //       DecryptedMessagesContext.Remove(message.RandomIndex);
        //    }
        //    //if (message.RandomIndex != 0)
        //    //{
        //    //    RandomMessagesContext.Remove(message.RandomIndex);
        //    //}
        //}

        public void AddMessageToContext(TLMessageBase message)
        {
            if (message.Id != 0)
            {
                if (TLUtils.IsChannelMessage(message, out TLPeerChannel peerChannel))
                {
                    var channelContext = ChannelsContext[peerChannel.Id];
                    if (channelContext == null)
                    {
                        channelContext = new Context<TLMessageBase>();
                        ChannelsContext[peerChannel.Id] = channelContext;
                    }

                    channelContext[message.Id] = message;
                }
                else
                {
                    MessagesContext[message.Id] = message;
                }
            }
            else if ((message.RandomId ?? 0) != 0)
            {
                RandomMessagesContext[message.RandomId.Value] = message;
            }
            else
            {
                throw new Exception("MsgId and RandomMsgId are zero");
            }
        }

        // TODO: Encrypted
        //public void AddDecryptedMessageToContext(TLDecryptedMessageBase message)
        //{
        //    if (message.RandomIndex != 0)
        //    {
        //        DecryptedMessagesContext[message.RandomIndex] = message;
        //    }
        //    else
        //    {
        //        throw new Exception("RandomId is zero for DecryptedMessage");
        //    }
        //}

        public void AddUser(TLUserBase user)
        {
            if (user != null)
            {
                // TODO: remove
                if (user is TLUser userFull && userFull.Id.Equals(38475861))
                {
                    userFull.IsVerified = true;
                }

                UsersContext[user.Id] = user;
            }
        }

        public void AddChat(TLChatBase chat)
        {
            if (chat != null)
            {
                // TODO: remove
                if (chat is TLChannel channel && (channel.Id.Equals(1060755082) || channel.Id.Equals(1057176757)))
                {
                    channel.IsVerified = true;
                }

                ChatsContext[chat.Id] = chat;
            }
        }

        public void AddEncryptedChat(TLEncryptedChatBase chat)
        {
            if (chat != null)
            {
                EncryptedChatsContext[chat.Id] = chat;
            }
        }

        public void SaveSnapshot(string toDirectoryName)
        {
            var timer = Stopwatch.StartNew();

            CopyInternal(_usersSyncRoot, UsersMTProtoFileName, Path.Combine(toDirectoryName, UsersMTProtoFileName));
            CopyInternal(_chatsSyncRoot, ChatsMTProtoFileName, Path.Combine(toDirectoryName, ChatsMTProtoFileName));
            // TODO: Encrypted CopyInternal(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName, Path.Combine(toDirectoryName, EncryptedChatsMTProtoFileName));
            CopyInternal(_dialogsFileSyncRoot, DialogsMTProtoFileName, Path.Combine(toDirectoryName, DialogsMTProtoFileName));

            TLUtils.WritePerformance("Save DB snapshot time: " + timer.Elapsed);
        }

        public void SaveTempSnapshot(string toDirectoryName)
        {
            var timer = Stopwatch.StartNew();

            CopyInternal(_usersSyncRoot, TempUsersMTProtoFileName, Path.Combine(toDirectoryName, UsersMTProtoFileName));
            CopyInternal(_chatsSyncRoot, TempChatsMTProtoFileName, Path.Combine(toDirectoryName, ChatsMTProtoFileName));
            // TODO: Encrypted CopyInternal(_encryptedChatsSyncRoot, TempEncryptedChatsMTProtoFileName, Path.Combine(toDirectoryName, EncryptedChatsMTProtoFileName));
            CopyInternal(_dialogsFileSyncRoot, TempDialogsMTProtoFileName, Path.Combine(toDirectoryName, DialogsMTProtoFileName));

            TLUtils.WritePerformance("Save DB snapshot time: " + timer.Elapsed);
        }

        private void CopyInternal(object syncRoot, string fileName, string destinationFileName)
        {
            FileUtils.Copy(syncRoot, fileName, destinationFileName);
        }

        public void LoadSnapshot(string fromDirectoryName)
        {
            var timer = Stopwatch.StartNew();

            CopyInternal(_usersSyncRoot, Path.Combine(fromDirectoryName, UsersMTProtoFileName), UsersMTProtoFileName);
            CopyInternal(_chatsSyncRoot, Path.Combine(fromDirectoryName, ChatsMTProtoFileName), ChatsMTProtoFileName);
            // TODO: Encrypted CopyInternal(_encryptedChatsSyncRoot, Path.Combine(fromDirectoryName, EncryptedChatsMTProtoFileName), EncryptedChatsMTProtoFileName);
            CopyInternal(_dialogsFileSyncRoot, Path.Combine(fromDirectoryName, DialogsMTProtoFileName), DialogsMTProtoFileName);

            TLUtils.WritePerformance("Load DB snapshot time: " + timer.Elapsed);
        }

        public void Clear()
        {
            var timer = Stopwatch.StartNew();                   

            Dialogs.Clear();
            UsersContext.Clear();
            ChatsContext.Clear();
            EncryptedChatsContext.Clear();
            DialogsContext.Clear();

            ClearInternal(_usersSyncRoot, UsersMTProtoFileName);
            ClearInternal(_chatsSyncRoot, ChatsMTProtoFileName);
            // TODO: Encrypted ClearInternal(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName);
            ClearInternal(_dialogsFileSyncRoot, DialogsMTProtoFileName);

            TLUtils.WritePerformance("Clear DB time: " + timer.Elapsed);
        }

        private void ClearInternal(object syncRoot, string fileName)
        {
            FileUtils.Delete(syncRoot, fileName);
        }

        // TODO: Encrypted 
        //private void AddDecryptedMessageCommon(TLDecryptedMessageBase message, TLEncryptedChatBase p, bool notifyNewDialogs, Func<IList<TLDecryptedMessageBase>, bool> insertAction)
        //{
        //    if (message != null)
        //    {
        //        AddDecryptedMessageToContext(message);

        //        var isUnread = !message.Out.Value && message.Unread.Value;

        //        var dialog = GetEncryptedDialog(message);

        //        if (dialog != null)
        //        {
        //            if (dialog.Messages.Count > 0)
        //            {
        //                var isAdded = insertAction(dialog.Messages);

        //                if (!isAdded)
        //                {
        //                    dialog.Messages.Add(message);
        //                }
        //            }
        //            else
        //            {
        //                dialog.Messages.Add(message);
        //            }

        //            if (isUnread)
        //            {
        //                var isDisplayedMessage = TLUtils.IsDisplayedDecryptedMessage(message);

        //                if (isDisplayedMessage)
        //                {
        //                    dialog.UnreadCount = new int?(dialog.UnreadCount.Value + 1);
        //                }
        //            }

        //            CorrectDialogOrder(dialog, message.DateIndex, 0);

        //            if (dialog.Messages[0] == message)
        //            {
        //                dialog.TopMessage = message;
        //                dialog.TopDecryptedMessageRandomId = message.RandomId;
        //                if (_eventAggregator != null)
        //                {
        //                    _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, message));
        //                }
        //            }
        //        }
        //        else
        //        {
        //            var currentUserId = MTProtoService.Instance.CurrentUserId;
        //            int? withId;
        //            TLObject with = null;

        //            var encryptedChatCommmon = p as TLEncryptedChatCommon;
        //            if (encryptedChatCommmon != null)
        //            {
        //                withId = encryptedChatCommmon.AdminId.Value == currentUserId.Value
        //                    ? encryptedChatCommmon.ParticipantId
        //                    : encryptedChatCommmon.AdminId;

        //                with = UsersContext[withId.Value];
        //            }


        //            TLPeerBase peer = new TLPeerEncryptedChat { Id = message.ChatId };
        //            int index = 0;

        //            var addingDialog = new TLEncryptedDialog
        //            {
        //                Peer = peer,
        //                With = with,
        //                Messages = new ObservableCollection<TLDecryptedMessageBase> { message },
        //                TopDecryptedMessageRandomId = message.RandomId,
        //                TopMessage = message,
        //                UnreadCount = isUnread ? 1 : 0
        //            };

        //            lock (_dialogsSyncRoot)
        //            {
        //                for (var i = 0; i < Dialogs.Count; i++)
        //                {
        //                    if (Dialogs[i].GetDateIndex() < message.DateIndex)
        //                    {
        //                        index = i;
        //                        break;
        //                    }
        //                }

        //                Dialogs.Insert(index, addingDialog);
        //                DialogsContext[addingDialog.Index] = addingDialog;
        //            }
        //            if (_eventAggregator != null && notifyNewDialogs)
        //            {
        //                _eventAggregator.Publish(new DialogAddedEventArgs(addingDialog));
        //            }
        //        }
        //    }
        //    else
        //    {
        //        throw new NotImplementedException();
        //    }
        //}

        private void CorrectDialogOrder(TLDialog dialog, int dateIndex, int messageId)
        {
            var isInserted = false;
            lock (_dialogsSyncRoot)
            {
                Dialogs.Remove(dialog);
                for (var i = 0; i < Dialogs.Count; i++)
                {
                    if (Dialogs[i].GetDateIndex() < dateIndex)
                    {
                        Dialogs.Insert(i, dialog);
                        isInserted = true;
                        break;
                    }
                    
                    // в бродкастах дата у всех сообщений совпадает: правильный порядок можно определить по индексу сообщения
                    if (Dialogs[i].GetDateIndex() == dateIndex)
                    {
                        var currentMessageId = Dialogs[i].TopMessage;
                        if (currentMessageId != 0 && messageId != 0)
                        {
                            if (currentMessageId < messageId)
                            {
                                Dialogs.Insert(i, dialog);
                                isInserted = true;
                                break;
                            }

                            Dialogs.Insert(i + 1, dialog);
                            isInserted = true;
                            break;
                        }
                    }
                }

                if (!isInserted)
                {
                    Dialogs.Add(dialog);
                }
            }
        }

        private void AddMessageCommon(TLMessageBase message, bool notifyNewDialogs, bool notifyTopMessageUpdated, Func<IList<TLMessageBase>, bool> insertAction)
        {
            if (message != null)
            {
                AddMessageToContext(message);

                var commonMessage = message as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    var isUnread = !commonMessage.IsOut && commonMessage.IsUnread;

                    var dialogBase = GetDialog(commonMessage);
                    if (dialogBase != null)
                    {
                        var dialog = dialogBase as TLDialog;
                        if (dialog != null)
                        {
                            if (dialog.Messages.Count > 0)
                            {
                                var isAdded = insertAction(dialog.Messages);

                                if (!isAdded)
                                {
                                    dialog.Messages.Add(commonMessage);
                                }
                            }
                            else
                            {
                                dialog.Messages.Add(commonMessage);
                            }

                            if (isUnread && dialog.TopMessageItem != null && dialog.TopMessageItem.Id < commonMessage.Id)
                            {
                                dialogBase.UnreadCount = dialogBase.UnreadCount + 1;
                            }

                            if (dialog.TopMessageItem != null)
                            {
                                CorrectDialogOrder(dialogBase, dialog.TopMessageItem.Date, dialog.TopMessageItem.Id);
                            }

                            if (dialog.Messages[0] == commonMessage)
                            {
                                if (notifyTopMessageUpdated)
                                {
                                    dialog._topMessageItem = commonMessage;
                                    Helpers.Execute.BeginOnUIThread(() => dialog.RaisePropertyChanged(() => dialog.TopMessageItem));
                                }
                                else
                                {
                                    dialog._topMessageItem = commonMessage;
                                }
                                dialog.TopMessage = commonMessage.Id;
                                dialog.TopMessageRandomId = commonMessage.RandomId;
                                if (_eventAggregator != null && notifyTopMessageUpdated)
                                {
                                   Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, commonMessage)));
                                }
                            }
                        }
                    }
                    else
                    {
                        ITLDialogWith with;

                        TLPeerBase peer;

                        if (commonMessage.ToId is TLPeerChannel)
                        {
                            peer = commonMessage.ToId;
                        }
                        else if (commonMessage.ToId is TLPeerChat)
                        {
                            peer = commonMessage.ToId;
                        }
                        else
                        {
                            if (commonMessage.IsOut)
                            {
                                peer = commonMessage.ToId;
                            }
                            else
                            {
                                peer = new TLPeerUser { Id = commonMessage.FromId ?? 0 };
                            }
                        }

                        if (peer is TLPeerChannel)
                        {
                            with = ChatsContext[peer.Id];
                        }
                        else if (peer is TLPeerChat)
                        {
                            with = ChatsContext[peer.Id];
                        }
                        else 
                        {
                            with = UsersContext[peer.Id];
                        }
                        int index = 0;


                        TLDialog addingDialog;

                        if (peer is TLPeerChannel)
                        {
                            addingDialog = new TLDialog
                            {
                                Flags = 0,
                                Peer = peer,
                                With = with,
                                Messages = new ObservableCollection<TLMessageBase> { commonMessage },
                                TopMessage = commonMessage.Id,
                                TopMessageRandomId = commonMessage.RandomId,
                                TopMessageItem = commonMessage,
                                UnreadCount = isUnread ? 1 : 0,
                                ReadInboxMaxId = 0,
                                ReadOutboxMaxId = 0,

                                Pts = 0
                            };
                        }
                        else
                        {
                            addingDialog = new TLDialog
                            {
                                Flags = 0,
                                Peer = peer,
                                With = with,
                                Messages = new ObservableCollection<TLMessageBase> { commonMessage },
                                TopMessage = commonMessage.Id,
                                TopMessageRandomId = commonMessage.RandomId,
                                TopMessageItem = commonMessage,
                                UnreadCount = isUnread ? 1 : 0,
                                ReadInboxMaxId = 0,
                                ReadOutboxMaxId = 0
                            };
                        }
                        

                        lock (_dialogsSyncRoot)
                        {
                            for (var i = 0; i < Dialogs.Count; i++)
                            {
                                if (Dialogs[i].GetDateIndex() < message.Date)
                                {
                                    index = i;
                                    break;
                                }
                            }

                            Dialogs.Insert(index, addingDialog);
                            DialogsContext[addingDialog.Index] = addingDialog;
                        }
                        if (_eventAggregator != null && notifyNewDialogs)
                        {
                            _eventAggregator.Publish(new DialogAddedEventArgs(addingDialog));
                        }
                    }
                }
            }
            else
            {

            }
        }

        // TODO: Encrypted 
        //private TLEncryptedDialog GetEncryptedDialog(int? id)
        //{
        //    lock (_dialogsSyncRoot)
        //    {
        //        foreach (var d in Dialogs)
        //        {
        //            if (d.Peer is TLPeerEncryptedChat
        //                && d.Peer.Id.Value == id.Value)
        //            {
        //                return d as TLEncryptedDialog;
        //            }
        //        }
        //    }
        //    return null;
        //}

        // TODO: Encrypted 
        //private TLEncryptedDialog GetEncryptedDialog(TLEncryptedChatBase peer)
        //{
        //    lock (_dialogsSyncRoot)
        //    {
        //        foreach (var d in Dialogs)
        //        {
        //            if (d.Peer is TLPeerEncryptedChat
        //                && d.Peer.Id.Value == peer.Id.Value)
        //            {
        //                return d as TLEncryptedDialog;
        //            }
        //        }
        //    }
        //    return null;
        //}

        // TODO: Encrypted 
        //private TLEncryptedDialog GetEncryptedDialog(TLDecryptedMessageBase commonMessage)
        //{
        //    lock (_dialogsSyncRoot)
        //    {
        //        foreach (var d in Dialogs)
        //        {
        //            if (d.Peer is TLPeerEncryptedChat
        //                //&& message.ToId is TLPeerChat
        //                && d.Peer.Id.Value == commonMessage.ChatId.Value)
        //            {
        //                return d as TLEncryptedDialog;
        //            }
        //        }

        //        //dialog = Dialogs.FirstOrDefault(x => x.WithId == peer.Id.Value && x.IsChat == peer is TLPeerChat);
        //    }
        //    return null;
        //}

        public TLDialog GetDialog(TLPeerBase peer)
        {
            TLDialog result = null;

            lock (_dialogsSyncRoot)
            {
                foreach (var d in Dialogs)
                {
                    if (d.Peer.Id == peer.Id)
                    {
                        if (d.Peer is TLPeerChannel
                            && peer is TLPeerChannel)
                        {
                            result = d as TLDialog;
                            if (result != null) break;
                        }

                        if (d.Peer is TLPeerChat
                            && peer is TLPeerChat)
                        {
                            result = d as TLDialog;
                            if (result != null) break;
                        }

                        if (d.Peer is TLPeerUser
                            && peer is TLPeerUser)
                        {
                            result = d as TLDialog;
                            if (result != null) break;
                        }
                    }
                }
            }

            return result;
        }

        private TLDialog GetDialog(TLMessageCommonBase commonMessage)
        {
            lock (_dialogsSyncRoot)
            {
                foreach (var d in Dialogs)
                {
                    if (d.Peer is TLPeerChat
                        && commonMessage.ToId is TLPeerChat
                        && d.Peer.Id == commonMessage.ToId.Id)
                    {
                        return d as TLDialog;
                    }

                    if (d.Peer is TLPeerUser
                        && commonMessage.ToId is TLPeerUser
                        && commonMessage.IsOut
                        && d.Peer.Id == commonMessage.ToId.Id)
                    {
                        return d as TLDialog;
                    }

                    if (d.Peer is TLPeerUser
                        && commonMessage.ToId is TLPeerUser
                        && !commonMessage.IsOut
                        && d.Peer.Id == commonMessage.FromId)
                    {
                        return d as TLDialog;
                    }

                    if (d.Peer is TLPeerChannel
                        && commonMessage.ToId is TLPeerChannel
                        && d.Peer.Id == commonMessage.ToId.Id)
                    {
                        return d as TLDialog;
                    }
                }

                //dialog = Dialogs.FirstOrDefault(x => x.WithId == peer.Id.Value && x.IsChat == peer is TLPeerChat);
            }
            return null;
        }

        public void UpdateSendingMessageContext(TLMessageBase message)
        {
            RemoveMessageFromContext(message);

            message.RandomId = null;

            AddMessageToContext(message);
        }

        public void UpdateSendingMessage(TLMessageBase message, TLMessageBase cachedMessage)
        {
            RemoveMessageFromContext(cachedMessage);

            cachedMessage.Update(message);
            cachedMessage.RandomId = null;

            AddMessageToContext(cachedMessage);

            var commonMessage = message as TLMessageCommonBase;
            if (commonMessage == null)
            {
                return;
            }
            var dialog = GetDialog(commonMessage) as TLDialog;

            if (dialog != null)
            {
                bool notify = false;
                lock (dialog.MessagesSyncRoot)
                {
                    var oldMessage = dialog.Messages.FirstOrDefault(x => x.Id == cachedMessage.Id);
                    if (oldMessage != null)
                    {
                        dialog.Messages.Remove(oldMessage);
                        TLDialog.InsertMessageInOrder(dialog.Messages, cachedMessage);
                        if (dialog.TopMessage == null || dialog.TopMessage < cachedMessage.Id)
                        {
                            dialog._topMessageItem = cachedMessage;
                            dialog.TopMessage = cachedMessage.Id;
                            dialog.TopMessageRandomId = cachedMessage.RandomId;
                            notify = true;
                        }
                    }
                }
                if (notify)
                {
                    _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, cachedMessage));
                }
            }
        }

        // TODO: Encrypted 
        //public void UpdateSendingDecryptedMessage(int? chatId, int? date, TLDecryptedMessageBase cachedMessage)
        //{
        //    RemoveDecryptedMessageFromContext(cachedMessage);

        //    cachedMessage.Date = date;

        //    AddDecryptedMessageToContext(cachedMessage);

        //    var dialog = GetEncryptedDialog(chatId);

        //    if (dialog != null)
        //    {
        //        var oldMessage = dialog.Messages.FirstOrDefault(x => x.RandomIndex == cachedMessage.RandomIndex);
        //        if (oldMessage != null)
        //        {
        //            dialog.Messages.Remove(oldMessage);
        //            TLEncryptedDialog.InsertMessageInOrder(dialog.Messages, cachedMessage);
        //            if (dialog.TopDecryptedMessageRandomId == null || dialog.TopDecryptedMessageRandomId.Value != cachedMessage.RandomIndex)
        //            {
        //                dialog._topMessage = cachedMessage;
        //                dialog.TopDecryptedMessageRandomId = cachedMessage.RandomId;
        //                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, cachedMessage));
        //            }
        //        }
        //    }
        //}

        public void AddSendingMessage(TLMessageBase message, TLMessageBase previousMessage)
        {
            AddMessageCommon(
                message, true, true,
                messages =>
                {
                    if (previousMessage == null)
                    {
                        messages.Insert(0, message);
                        return true;
                    }

                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (messages[i] == previousMessage)
                        {
                            messages.Insert(i, message);
                            return true;
                        }
                    }

                    if (previousMessage.Id > 0)
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            if (messages[i].Id > 0 && previousMessage.Id > messages[i].Id)
                            {
                                messages.Insert(i, message);
                                return true;
                            }
                        }
                    }
                    else
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            if (messages[i].Date > 0 && previousMessage.Date > messages[i].Date)
                            {
                                messages.Insert(i, message);
                                return true;
                            }
                        }
                    }

                    return false;
                });
        }

        public void AddSendingMessage(TLMessageBase message, TLMessageBase previousMessage, bool notifyNewDialog, bool notifyTopMessageUpdated)
        {
            AddMessageCommon(
                message, notifyNewDialog, notifyTopMessageUpdated,
                messages =>
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (messages[i] == previousMessage)
                        {
                            messages.Insert(i, message);
                            return true;
                        }
                    }

                    return false;
                });
        }


        // TODO: Encrypted 
        //public void AddDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, bool notifyNewDialogs = true)
        //{
        //    AddDecryptedMessageCommon(
        //        message, peer, notifyNewDialogs,
        //        messages =>
        //        {
        //            for (var i = 0; i < messages.Count; i++)
        //            {
        //                if (messages[i].DateIndex < message.DateIndex)
        //                {
        //                    messages.Insert(i, message);
        //                    return true;
        //                }

        //                if (messages[i].DateIndex == message.DateIndex)
        //                {
        //                    // TODO: fix for randomId and id (messages[i] has only randomId)
        //                    //if (messages[i].RandomIndex == 0)
        //                    //{
        //                    //    messages.Insert(i, message);
        //                    //    return true;
        //                    //}

        //                    //if (messages[i].RandomIndex < message.RandomIndex)
        //                    //{
        //                    //    messages.Insert(i, message);
        //                    //    return true;
        //                    //}

        //                    if (messages[i].RandomIndex == message.RandomIndex)
        //                    {
        //                        return true;
        //                    }

        //                    messages.Insert(i, message);

        //                    return true;
        //                }
        //            }

        //            return false;
        //        });
        //}

        public void AddMessage(TLMessageBase message, bool notifyNewDialogs = true, bool notifyNewMessageUpdated = true)
        {
            AddMessageCommon(
                message, notifyNewDialogs, notifyNewMessageUpdated,
                messages =>
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (messages[i].Date < message.Date)
                        {
                            messages.Insert(i, message);
                            return true;
                        }
                        
                        if (messages[i].Date == message.Date)
                        {
                            // TODO: fix for randomId and id (messages[i] can has only randomId)
                            if (messages[i].Id == 0)
                            {
                                messages.Insert(i, message);
                                return true;
                            }

                            if (messages[i].Id < message.Id)
                            {
                                messages.Insert(i, message);
                                return true;
                            }

                            if (messages[i].Id == message.Id)
                            {
                                if (messages[i].GetType() != message.GetType())
                                {
                                    messages[i] = message;
                                }

                                return true;
                            }
                        }
                    }

                    return false;
                });
        }

        // TODO: Encrypted 
        //public void ClearDecryptedHistory(int? chatId)
        //{
        //    var dialog = DialogsContext[chatId.Value] as TLEncryptedDialog;
        //    if (dialog != null)
        //    {

        //        lock (_dialogsSyncRoot)
        //        {
        //            foreach (var message in dialog.Messages)
        //            {
        //                RemoveDecryptedMessageFromContext(message);
        //            }

        //            dialog.Messages.Clear();

        //            var chat = (TLEncryptedChatCommon)InMemoryCacheService.Instance.GetEncryptedChat(chatId);
        //            var lastMessage = new TLDecryptedMessageService
        //            {
        //                RandomId = TLLong.Random(),
        //                RandomBytes = TLString.Random(Constants.MinRandomBytesLength),
        //                ChatId = chat.Id,
        //                Action = new TLDecryptedMessageActionEmpty(),
        //                FromId = MTProtoService.Instance.CurrentUserId,
        //                Date = chat.Date,
        //                Out = new TLBool(false),
        //                Unread = new TLBool(false),
        //                Status = TLMessageState.Read
        //            };
        //            dialog.Messages.Add(lastMessage);
        //            AddDecryptedMessageToContext(lastMessage);

        //            dialog.TopMessage = lastMessage;
        //            dialog.TopDecryptedMessageRandomId = lastMessage.RandomId;

        //            CorrectDialogOrder(dialog, dialog.TopMessage.DateIndex, 0);
        //        }

        //        if (_eventAggregator != null)
        //        {
        //            _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, dialog.TopMessage));
        //        }
        //    }
        //}

        // TODO: Encrypted 
        //public void DeleteDecryptedMessage(TLDecryptedMessageBase message, TLPeerBase peer)
        //{
        //    if (message != null)
        //    {
        //        RemoveDecryptedMessageFromContext(message);
        //        //MessagesContext.Remove(message.Index);

        //        var commonMessage = message;
        //        if (commonMessage != null)
        //        {
        //            //TLDialog dialog;
        //            var isMessageRemoved = false;
        //            var isDialogRemoved = false;
        //            var isTopMessageUpdated = false;

        //            var dialog = DialogsContext[peer.Id.Value] as TLEncryptedDialog;
        //            if (dialog != null)
        //            {
        //                lock (_dialogsSyncRoot)
        //                {
        //                    dialog.Messages.Remove(commonMessage);
        //                    isMessageRemoved = true;

        //                    if (dialog.Messages.Count == 0)
        //                    {
        //                        DialogsContext.Remove(dialog.Index);
        //                        Dialogs.Remove(dialog);
        //                        isDialogRemoved = true;
        //                    }
        //                    else
        //                    {
        //                        dialog.TopMessage = dialog.Messages[0];
        //                        dialog.TopDecryptedMessageRandomId = dialog.Messages[0].RandomId;
        //                        isTopMessageUpdated = true;
        //                    }

        //                    CorrectDialogOrder(dialog, dialog.TopMessage.DateIndex, 0);
        //                }
        //            }


        //            if (isDialogRemoved && _eventAggregator != null)
        //            {
        //                _eventAggregator.Publish(new DialogRemovedEventArgs(dialog));
        //            }
        //            if (isTopMessageUpdated && _eventAggregator != null)
        //            {
        //                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, dialog.TopMessage));
        //            }
        //            if (isMessageRemoved && _eventAggregator != null)
        //            {
        //                _eventAggregator.Publish(new MessagesRemovedEventArgs(dialog, commonMessage));
        //            }
        //        }
        //    }
        //    else
        //    {
        //        throw new NotImplementedException();
        //    }
        //}

        public TLMessageBase GetMessage(TLPeerBase peer, int? messageId)
        {
            if (peer is TLPeerChannel)
            {
                var channelContext = ChannelsContext[peer.Id];
                if (channelContext != null)
                {
                    return channelContext[messageId.Value];
                }

                return null;
            }

            return MessagesContext[messageId.Value];
        }

        public void DeleteMessages(TLPeerBase peer, TLMessageBase lastMessage, IList<int> messageIds)
        {
            if (messageIds != null)
            {
                var messages = new List<TLMessageBase>();
                for (var i = 0; i < messageIds.Count; i++)
                {
                    var message = GetMessage(peer, messageIds[i]);
                    if (message != null)
                    {
                        RemoveMessageFromContext(message);
                        messages.Add(message);
                    }
                }

                var isDialogRemoved = false;
                var isTopMessageUpdated = false;

                var dialogBase = GetDialog(peer);
                TLMessageBase topMessage = null;
                lock (_dialogsSyncRoot)
                {
                    var dialog = dialogBase as TLDialog;
                    if (dialog != null)
                    {
                        for (var i = 0; i < messageIds.Count; i++)
                        {
                            for (var j = 0; j < dialog.Messages.Count; j++)
                            {
                                if (messageIds[i] == dialog.Messages[j].Id)
                                {
                                    dialog.Messages.RemoveAt(j);
                                    break;
                                }
                            }
                        }

                        if (dialog.Messages.Count == 0)
                        {
                            if (lastMessage == null)
                            {
                                DialogsContext.Remove(dialogBase.Index);
                                Dialogs.Remove(dialogBase);
                                isDialogRemoved = true;
                            }
                            else
                            {
                                dialog.Messages.Add(lastMessage);
                                dialog._topMessageItem = dialog.Messages[0];
                                dialog.TopMessage = dialog.Messages[0].Id;
                                dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                                isTopMessageUpdated = true;
                                topMessage = dialog.TopMessageItem;
                            }
                        }
                        else
                        {
                            dialog._topMessageItem = dialog.Messages[0];
                            dialog.TopMessage = dialog.Messages[0].Id;
                            dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                            isTopMessageUpdated = true;
                            topMessage = dialog.TopMessageItem;
                        }

                        CorrectDialogOrder(dialog, dialog.TopMessageItem.Date, dialog.TopMessageItem.Id);
                    }
                }

                if (isDialogRemoved && _eventAggregator != null)
                {
                    _eventAggregator.Publish(new DialogRemovedEventArgs(dialogBase));
                }
                if (isTopMessageUpdated && _eventAggregator != null)
                {
                    _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, topMessage));
                }
                if (_eventAggregator != null)
                {
                    _eventAggregator.Publish(new MessagesRemovedEventArgs(dialogBase, messages));
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void DeleteUserHistory(TLPeerBase channel, TLPeerBase user)
        {
            var isMessageRemoved = false;
            var isDialogRemoved = false;
            var isTopMessageUpdated = false;
            TLMessageBase topMessage = null;

            var dialogBase = GetDialog(channel);

            var messages = new List<TLMessageBase>();

            lock (_dialogsSyncRoot)
            {
                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    for (var j = 0; j < dialog.Messages.Count; j++)
                    {
                        var messageCommon = dialog.Messages[j] as TLMessageCommonBase;
                        if (messageCommon != null && messageCommon.FromId.Value == user.Id)
                        {
                            if (messageCommon.Id == 1)
                            {
                                var message = messageCommon as TLMessageService;
                                if (message != null)
                                {
                                    var channelMigrateFrom = message.Action as TLMessageActionChannelMigrateFrom;
                                    if (channelMigrateFrom != null)
                                    {
                                        continue;
                                    }
                                }
                            }

                            messages.Add(messageCommon);
                            dialog.Messages.RemoveAt(j--);
                        }
                    }

                    isMessageRemoved = true;

                    if (dialog.Messages.Count == 0)
                    {
                        DialogsContext.Remove(dialogBase.Index);
                        Dialogs.Remove(dialogBase);
                        isDialogRemoved = true;
                    }
                    else
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            var commonMessage = messages[i] as TLMessageCommonBase;
                            if (commonMessage != null && !commonMessage.IsOut && commonMessage.IsUnread)
                            {
                                dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1);
                            }
                        }

                        dialog._topMessageItem = dialog.Messages[0];
                        dialog.TopMessage = dialog.Messages[0].Id;
                        dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                        isTopMessageUpdated = true;
                        topMessage = dialog.TopMessageItem;
                    }
                }
            }

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message != null)
                {
                    RemoveMessageFromContext(message);
                }
            }

            if (topMessage != null)
            {
                CorrectDialogOrder(dialogBase, topMessage.Date, topMessage.Id);
            }

            if (isDialogRemoved && _eventAggregator != null)
            {
                _eventAggregator.Publish(new DialogRemovedEventArgs(dialogBase));
            }
            if (isTopMessageUpdated && _eventAggregator != null)
            {
                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, topMessage));
            }
            if (isMessageRemoved && _eventAggregator != null)
            {
                _eventAggregator.Publish(new MessagesRemovedEventArgs(dialogBase, messages));
            }
        }

        public void DeleteMessages(IList<TLMessageBase> messages, TLPeerBase peer)
        {
            var isMessageRemoved = false;
            var isDialogRemoved = false;
            var isTopMessageUpdated = false;
            TLMessageBase topMessage = null;

            var dialogBase = GetDialog(peer);

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message != null)
                {
                    RemoveMessageFromContext(message);
                }
            }

            lock (_dialogsSyncRoot)
            {
                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        for (var j = 0; j < dialog.Messages.Count; j++)
                        {
                            if (messages[i].Id == dialog.Messages[j].Id)
                            {
                                dialog.Messages.RemoveAt(j);
                                break;
                            }
                        }
                    }

                    isMessageRemoved = true;

                    if (dialog.Messages.Count == 0)
                    {
                        DialogsContext.Remove(dialogBase.Index);
                        Dialogs.Remove(dialogBase);
                        isDialogRemoved = true;
                    }
                    else
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            var commonMessage = messages[i] as TLMessageCommonBase;
                            if (commonMessage != null && !commonMessage.IsOut && commonMessage.IsUnread)
                            {
                                dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1);
                            }
                        }

                        dialog._topMessageItem = dialog.Messages[0];
                        dialog.TopMessage = dialog.Messages[0].Id;
                        dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                        isTopMessageUpdated = true;
                        topMessage = dialog.TopMessageItem;
                    }
                }
            }

            if (topMessage != null)
            {
                CorrectDialogOrder(dialogBase, topMessage.Date, topMessage.Id);
            }

            if (isDialogRemoved && _eventAggregator != null)
            {
                _eventAggregator.Publish(new DialogRemovedEventArgs(dialogBase));
            }
            if (isTopMessageUpdated && _eventAggregator != null)
            {
                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, topMessage));
            }
            if (isMessageRemoved && _eventAggregator != null)
            {
                _eventAggregator.Publish(new MessagesRemovedEventArgs(dialogBase, messages));
            }
        }

        public void DeleteMessage(TLMessageBase message)
        {
            if (message != null)
            {
                RemoveMessageFromContext(message);
                //MessagesContext.Remove(message.Index);

                var commonMessage = message as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    //TLDialog dialog;
                    var isMessageRemoved = false;
                    var isDialogRemoved = false;
                    var isTopMessageUpdated = false;

                    var dialogBase = GetDialog(commonMessage);
                    TLMessageBase topMessage = null;
                    lock (_dialogsSyncRoot)
                    {
                        //dialog = Dialogs.FirstOrDefault(x => x.WithId == peer.Id.Value && x.IsChat == peer is TLPeerChat);
                        var dialog = dialogBase as TLDialog;
                        if (dialog != null)
                        {
                            dialog.Messages.Remove(commonMessage);
                            isMessageRemoved = true;

                            if (dialog.Messages.Count == 0)
                            {
                                DialogsContext.Remove(dialogBase.Index);
                                Dialogs.Remove(dialogBase);
                                isDialogRemoved = true;
                            }
                            else
                            {
                                if (!commonMessage.IsOut && commonMessage.IsUnread)
                                {
                                    dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1);
                                }
                                dialog.TopMessageItem = dialog.Messages[0];
                                dialog.TopMessage = dialog.Messages[0].Id;
                                dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                                isTopMessageUpdated = true;
                                topMessage = dialog.TopMessageItem;
                            }

                            CorrectDialogOrder(dialog, dialog.TopMessageItem.Date, dialog.TopMessageItem.Id);
                        }
                    }

                    if (isDialogRemoved && _eventAggregator != null)
                    {
                        _eventAggregator.Publish(new DialogRemovedEventArgs(dialogBase));
                    }
                    if (isTopMessageUpdated && _eventAggregator != null)
                    {
                        _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, topMessage));
                    }
                    if (isMessageRemoved && _eventAggregator != null)
                    {
                        _eventAggregator.Publish(new MessagesRemovedEventArgs(dialogBase, commonMessage));
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void ReplaceUser(int index, TLUserBase user)
        {
            var cachedUser = UsersContext[index];
            if (cachedUser == null)
            {
                UsersContext[index] = user;
            }
            else
            {
                UsersContext[index] = user;

                lock (_dialogsSyncRoot)
                {
                    foreach (var dialog in Dialogs)
                    {
                        if (!dialog.IsChat
                            && dialog.WithId == index)
                        {
                            dialog.With = user;
                        }
                    }
                }
            }
        }

        public void DeleteUser(int? id)
        {
            UsersContext.Remove(id.Value);
        }

        public void DeleteUser(TLUserBase user)
        {
            UsersContext.Remove(user.Id);
        }

        public void ReplaceChat(int index, TLChatBase chat)
        {
            var cachedChat = ChatsContext[index];
            if (cachedChat == null)
            {
                ChatsContext[index] = chat;
            }
            else
            {
                ChatsContext[index] = chat;

                lock (_dialogsSyncRoot)
                {
                    foreach (var dialog in Dialogs)
                    {
                        if (dialog.IsChat
                            && dialog.WithId == index)
                        {
                            dialog.With = chat;
                        }
                    }
                }
            }
        }

        public void ReplaceEncryptedChat(int index, TLEncryptedChatBase chat)
        {
            var cachedChat = EncryptedChatsContext[index];
            if (cachedChat == null)
            {
                EncryptedChatsContext[index] = chat;
            }
            else
            {
                EncryptedChatsContext[index] = chat;

                //lock (_encryptedDialogsSyncRoot)
                //{
                //    foreach (var dialog in EncryptedDialogs)
                //    {
                //        if (dialog.IsChat
                //            && dialog.WithId == index)
                //        {
                //            dialog.With = chat;
                //        }
                //    }
                //}
            }
        }

        public void DeleteChat(int? id)
        {
            ChatsContext.Remove(id.Value);
        }

        public void DeleteChat(TLChatBase chat)
        {
            ChatsContext.Remove(chat.Id);
        }

        public void DeleteDialog(TLDialog dialog)
        {
            lock (_dialogsSyncRoot)
            {
                var dbDialog = Dialogs.FirstOrDefault(x => x.Index == dialog.Index);

                Dialogs.Remove(dbDialog);
            }
        }

        public void ClearDialog(TLPeerBase peer)
        {
            var messages = new List<TLMessageBase>();

            var isDialogRemoved = false;
            var isTopMessageUpdated = false;

            var dialogBase = GetDialog(peer);
            TLMessageBase topMessage = null;
            lock (_dialogsSyncRoot)
            {
                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    var updatedIndex = -1;
                    for (var i = 0; i < dialog.Messages.Count; i++)
                    {
                        if (dialog.Messages[i].Id > 0)
                        {
                            if (updatedIndex == -1)
                            {
                                updatedIndex = i;
                            }
                            else
                            {
                                dialog.Messages.RemoveAt(i--);
                            }
                        }
                    }

                    if (updatedIndex >= 0 && updatedIndex < dialog.Messages.Count)
                    {
                        var messageCommon = dialog.Messages[updatedIndex] as TLMessageCommonBase;
                        if (messageCommon != null)
                        {
                            var clearHistoryMessage = new TLMessageService();
                            clearHistoryMessage.Flags = 0;
                            clearHistoryMessage.Id = messageCommon.Id;
                            clearHistoryMessage.FromId = messageCommon.FromId ?? new int?(-1);
                            clearHistoryMessage.ToId = messageCommon.ToId;
                            clearHistoryMessage.Date = messageCommon.Date;
                            clearHistoryMessage.IsOut = messageCommon.IsOut;
                            clearHistoryMessage.Action = new TLMessageActionHistoryClear();

                            dialog.Messages[updatedIndex] = clearHistoryMessage;
                        }
                    }

                    dialog.UnreadCount = 0;

                    if (dialog.Messages.Count == 0)
                    {
                        DialogsContext.Remove(dialogBase.Index);
                        Dialogs.Remove(dialogBase);
                        isDialogRemoved = true;
                    }
                    else
                    {
                        dialog._topMessageItem = dialog.Messages[0];
                        dialog.TopMessage = dialog.Messages[0].Id;
                        dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                        isTopMessageUpdated = true;
                        topMessage = dialog.TopMessageItem;
                    }

                    //CorrectDialogOrder(dialog, dialog.TopMessage.DateIndex, dialog.TopMessage.Index);
                }
            }

            for (var i = 0; i < messages.Count; i++)
            {
                RemoveMessageFromContext(messages[i]);
            }

            if (isDialogRemoved && _eventAggregator != null)
            {
                _eventAggregator.Publish(new DialogRemovedEventArgs(dialogBase));
            }
            if (isTopMessageUpdated && _eventAggregator != null)
            {
                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, topMessage));
            }
            if (_eventAggregator != null)
            {
                _eventAggregator.Publish(new MessagesRemovedEventArgs(dialogBase, messages));
            }
        }

        private static bool _logEnabled = true;

        private static void Log(string str)
        {
            if (!_logEnabled) return;

            Debug.WriteLine(str);
        }

        public void Open()
        {
            try
            {
                //TLObject.LogNotify = true;
                var timer = Stopwatch.StartNew();

                var handles = new List<ManualResetEvent>();

                var openUsersHandle = new ManualResetEvent(false);
                var t1 = Task.Factory.StartNew(() =>
                //Execute.BeginOnThreadPool(() =>
                {
                    var usersTimer = Stopwatch.StartNew();
                    var users = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLUserBase>>(_usersSyncRoot, UsersMTProtoFileName) ?? new TLVector<TLUserBase>();
                    Log(string.Format("Open users time ({0}) : {1}", users.Count, usersTimer.Elapsed));

                    UsersContext = new Context<TLUserBase>(users, x => x.Id);
                    
                    openUsersHandle.Set();
                });
                handles.Add(openUsersHandle);

                var openChatsHandle = new ManualResetEvent(false);
                var t2 = Task.Factory.StartNew(() =>
                //Execute.BeginOnThreadPool(() =>
                {
                    var chatsTimer = Stopwatch.StartNew();
                    var chats = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLChatBase>>(_chatsSyncRoot, ChatsMTProtoFileName) ?? new TLVector<TLChatBase>();
                    Log(string.Format("Open chats time ({0}) : {1}", chats.Count, chatsTimer.Elapsed));

                    ChatsContext = new Context<TLChatBase>(chats, x => x.Id);

                    openChatsHandle.Set();
                });
                handles.Add(openChatsHandle);

                // TODO: Encrypted 
                //var openEncryptedChatsHandle = new ManualResetEvent(false);
                ////Execute.BeginOnThreadPool(() =>
                //var t4 = Task.Factory.StartNew(() =>
                //{
                //    var encryptedChatsTimer = Stopwatch.StartNew();
                //    var encryptedChats = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLEncryptedChatBase>>(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName) ?? new TLVector<TLEncryptedChatBase>();
                //    Log(string.Format("Open encrypted chats time ({0}) : {1}", encryptedChats.Count, encryptedChatsTimer.Elapsed));

                //    EncryptedChatsContext = new Context<TLEncryptedChatBase>(encryptedChats, x => x.Index);

                //    openEncryptedChatsHandle.Set();
                //});
                //handles.Add(openEncryptedChatsHandle);

                var openDialogsHandle = new ManualResetEvent(false);
                //Execute.BeginOnThreadPool(() =>
                var t5 = Task.Factory.StartNew(() =>
                {
                    var dialogsTimer = Stopwatch.StartNew();
                    var dialogs = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLDialog>>(_dialogsFileSyncRoot, DialogsMTProtoFileName) ?? new TLVector<TLDialog>();
                    Dialogs = new ObservableCollection<TLDialog>(dialogs);
                    Log(string.Format("Open dialogs time ({0}) : {1}", dialogs.Count, dialogsTimer.Elapsed));

                    DialogsContext = new Context<TLDialog>(Dialogs, x => x.Index);
                    
                    openDialogsHandle.Set();
                });
                handles.Add(openDialogsHandle);

                Task.WaitAll(t1, t2/*, t4*/, t5);
                //WaitHandle.WaitAll(handles.ToArray());

                Log("Open DB files time: " + timer.Elapsed);
                MessagesContext.Clear();
                ChannelsContext.Clear();
                lock (_dialogsSyncRoot)
                {

                    ResendingMessages = new List<TLMessageBase>();
                    // TODO: Encrypted ResendingDecryptedMessages = new List<TLDecryptedMessageBase>();
                    foreach (var d in Dialogs)
                    {
                        // TODO: Encrypted 
                        //var encryptedDialog = d as TLEncryptedDialog;
                        //if (encryptedDialog != null)
                        //{
                        //    //var peer = encryptedDialog.Peer;
                        //    //if (peer is TLPeerEncryptedChat)
                        //    //{
                        //    //    var encryptedChat = EncryptedChatsContext[peer.Id.Value] as TLEncryptedChatCommon;
                        //    //    if (encryptedChat != null)
                        //    //    {
                        //    //        encryptedDialog._with = UsersContext[encryptedChat.ParticipantId.Value];
                        //    //    }
                        //    //}

                        //    encryptedDialog._topMessage = encryptedDialog.Messages.FirstOrDefault();
                        //    encryptedDialog.TopDecryptedMessageRandomId = encryptedDialog.TopMessage != null ? encryptedDialog.TopMessage.RandomId : null;
                        //    if (encryptedDialog.TopMessageId == null)
                        //    {
                        //        encryptedDialog.TopMessageRandomId = encryptedDialog.TopMessage != null ? encryptedDialog.TopMessage.RandomId : null;
                        //    }
                        //    foreach (var message in encryptedDialog.Messages)
                        //    {
                        //        if (message.Status == TLMessageState.Sending
                        //            || message.Status == TLMessageState.Compressing)
                        //        {
                        //            message.Status = TLMessageState.Failed;
                        //            ResendingDecryptedMessages.Add(message);
                        //        }

                        //        if (message.RandomIndex != 0)
                        //        {
                        //            DecryptedMessagesContext[message.RandomIndex] = message;
                        //        }
                        //    }
                        //}
                        //else
                        {
                            var dialog = (TLDialog) d;
                            var peer = dialog.Peer;
                            if (peer is TLPeerUser)
                            {
                                var user = UsersContext[peer.Id];
                                if (user != null)
                                {
                                    dialog._with = user;
                                }
                                else
                                {
                                    var userBase = dialog.With as TLUserBase;
                                    if (userBase != null)
                                    {
                                        UsersContext[userBase.Id] = userBase;
                                    }
                                }
                            }
                            else if (peer is TLPeerChat)
                            {
                                var chat = ChatsContext[peer.Id];
                                if (chat != null)
                                {
                                    dialog._with = chat;
                                }
                                else
                                {
                                    var chatBase = dialog.With as TLChatBase;
                                    if (chatBase != null)
                                    {
                                        ChatsContext[chatBase.Id] = chatBase;
                                    }
                                }
                            }
                            else if (peer is TLPeerChannel)
                            {
                                var channel = ChatsContext[peer.Id];
                                if (channel != null)
                                {
                                    dialog._with = channel;
                                }
                                else
                                {
                                    channel = dialog.With as TLChannel;
                                    if (channel != null)
                                    {
                                        ChatsContext[channel.Id] = channel;
                                    }
                                }
                            }

                            dialog._topMessageItem = dialog.Messages.FirstOrDefault();
                            var topMessage = dialog.TopMessageItem != null ? dialog.TopMessageItem.Id : new int?();
                            if (topMessage == null)
                            {
                                dialog.TopMessageRandomId = dialog.TopMessageItem?.RandomId;
                            }
                            else
                            {
                                dialog.TopMessage = topMessage.Value;
                            }
                            foreach (var message in dialog.Messages)
                            {
                                if (message.State == TLMessageState.Sending
                                    || message.State == TLMessageState.Compressing)
                                {
                                    message._state = message.Id != 0 ? TLMessageState.Confirmed : TLMessageState.Failed;
                                    ResendingMessages.Add(message);
                                }

                                AddMessageToContext(message);
                                if ((message.RandomId ?? 0) != 0)
                                {
                                    RandomMessagesContext[message.RandomId.Value] = message;
                                }
                            }
                        }
                        
                    }
                }
                _isOpened = true;

//#if DEBUG
                Execute.BeginOnThreadPool(() =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    FileUtils.Copy(_usersSyncRoot, UsersMTProtoFileName, TempUsersMTProtoFileName);
                    FileUtils.Copy(_chatsSyncRoot, ChatsMTProtoFileName, TempChatsMTProtoFileName);
                    // TODO: Encrypted FileUtils.Copy(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName, TempEncryptedChatsMTProtoFileName);
                    FileUtils.Copy(_dialogsSyncRoot, DialogsMTProtoFileName, TempDialogsMTProtoFileName);
                    var elapsed = stopwatch.Elapsed;
                    ;
                });
//#endif

                //TLObject.LogNotify = false;
                Log("Open DB time: " + timer.Elapsed);
                //TLUtils.WritePerformance("Open DB time: " + timer.Elapsed);
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR: open DB error", LogSeverity.Error);
                TLUtils.WriteException(e);
            }
        }

        public IList<TLMessageBase> ResendingMessages { get; protected set; }
        // TODO: Encrypted public IList<TLDecryptedMessageBase> ResendingDecryptedMessages { get; protected set; }  


        public event EventHandler CommitInvoked;

        protected virtual void RaiseCommitInvoked()
        {
            CommitInvoked?.Invoke(this, System.EventArgs.Empty);
        }

        public void Commit()
        {
            RaiseCommitInvoked();
            HasChanges = true;
        }

        public void CommitInternal()
        {
            //return;
            //return;
            if (!_isOpened) return;

            var timer = Stopwatch.StartNew();

            //truncate commiting DB
            var dialogs = new List<TLDialog>();
            var messagesCount = 0;
            lock (_dialogsSyncRoot)
            {
                var importantCount = 0;
                for (var i = 0; i < Dialogs.Count && importantCount < Constants.CachedDialogsCount; i++)
                {
                    dialogs.Add(Dialogs[i]);

                    var chat = Dialogs[i].With as TLChat;
                    if (chat == null || !chat.HasMigratedTo)
                    {
                        importantCount++;
                    }
                }
                foreach (var d in dialogs)
                {
                    var dialog = d as TLDialog;
                    if (dialog != null)
                    {
                        var chat = dialog.With as TLChat;
                        if (chat != null && chat.HasMigratedTo)
                        {
                            //dialog.Messages = new ObservableCollection<TLMessageBase>(dialog.Messages.Take(Constants.CachedMessagesCount));
                            dialog.CommitMessages = dialog.Messages.Take(Constants.CachedMessagesCount).ToList();
                        }
                        else
                        {
                            //dialog.Messages = new ObservableCollection<TLMessageBase>(dialog.Messages.Take(Constants.CachedMessagesCount));
                            dialog.CommitMessages = dialog.Messages.Take(Constants.CachedMessagesCount).ToList();
                        }
                        messagesCount += dialog.Messages.Count;
                    }
                }
            }

            var mtProtoDialogs = new TLVector<TLDialog>(dialogs);
            TLUtils.SaveObjectToMTProtoFile(_dialogsFileSyncRoot, DialogsMTProtoFileName, mtProtoDialogs);
            //var usersTimer = Stopwatch.StartNew();
            TLUtils.SaveObjectToMTProtoFile(_usersSyncRoot, UsersMTProtoFileName, new TLVector<TLUserBase>(UsersContext.Values.ToList()));
            //TLUtils.WritePerformance(string.Format("Commit users time ({0}) : {1}", UsersContext.Count, usersTimer.Elapsed));
            TLUtils.SaveObjectToMTProtoFile(_chatsSyncRoot, ChatsMTProtoFileName, new TLVector<TLChatBase>(ChatsContext.Values.ToList()));
            // TODO: Encrypted TLUtils.SaveObjectToMTProtoFile(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName, new TLVector<TLEncryptedChatBase> { Items = EncryptedChatsContext.Values.ToList() });

            HasChanges = false;

            TLUtils.WritePerformance(string.Format("Commit DB time ({0}d, {1}u, {2}c, {5}ec, {4}m): {3}", dialogs.Count, UsersContext.Count, ChatsContext.Count, timer.Elapsed, messagesCount, EncryptedChatsContext.Count));
        }

        public int CountRecords<T>() where T : TLObject
        {
            var result = 0;
            if (typeof (T) == typeof (TLMessageBase))
            {
                lock (_dialogsSyncRoot)
                {
                    foreach (var dialog in Dialogs)
                    {
                        result += dialog.CountMessages();
                    }
                }
                
            }
            else if (typeof (T) == typeof (TLDialog))
            {
                result += Dialogs.Count;
            }
            else if (typeof (T) == typeof (TLUserBase))
            {
                result += UsersContext.Count;
            }
            else if (typeof (T) == typeof (TLChatBase))
            {
                result += ChatsContext.Count;
            }
            else
            {
                throw new NotImplementedException();
            }

            return result;
        }

        public void Dispose()
        {
            _commitSubscription.Dispose();
        }

        public void Compress()
        {
            lock (_dialogsSyncRoot)
            {
                MessagesContext.Clear();
                for (var i = 0; i < Dialogs.Count; i++)
                {
                    var dialog = Dialogs[i] as TLDialog;
                    if (dialog != null)
                    {
                        dialog.Messages = new ObservableCollection<TLMessageBase>(dialog.Messages.Take(1));

                        var message = dialog.Messages.FirstOrDefault();
                        if (message != null)
                        {
                            AddMessageToContext(message);
                        }
                    }
                }
            }

            CommitInternal();
        }
    }
}