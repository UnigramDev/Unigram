using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;


namespace Telegram.Api.Services.Cache
{
    public class InMemoryDatabase : IDisposable
    {
        private volatile bool _isOpened;

        private const string DialogsMTProtoFileName = "dialogs.dat";
        private const string EncryptedDialogsMTProtoFileName = "encryptedDialogs.dat";
        private const string UsersMTProtoFileName = "users.dat";
        private const string ChatsMTProtoFileName = "chats.dat";
        private const string BroadcastsMTProtoFileName = "broadcasts.dat";
        private const string EncryptedChatsMTProtoFileName = "encryptedChats.dat";

        private readonly object _dialogsFileSyncRoot = new object();
        private readonly object _usersSyncRoot = new object();
        private readonly object _chatsSyncRoot = new object();
        private readonly object _broadcastsSyncRoot = new object();
        private readonly object _encryptedChatsSyncRoot = new object();

        public Context<TLMessageBase> MessagesContext = new Context<TLMessageBase>();
        public Context<Context<TLMessageBase>> ChannelsContext = new Context<Context<TLMessageBase>>(); 
        // TODO: Secrets public Context<TLDecryptedMessageBase> DecryptedMessagesContext = new Context<TLDecryptedMessageBase>(); 
        public Context<TLMessageBase> RandomMessagesContext = new Context<TLMessageBase>(); 
        public Context<TLDialog> DialogsContext = new Context<TLDialog>();
        public Context<TLUserBase> UsersContext = new Context<TLUserBase>();
        public Context<TLChatBase> ChatsContext = new Context<TLChatBase>();
        // NO MORE SUPPORTED: public Context<TLBroadcastChat> BroadcastsContext = new Context<TLBroadcastChat>(); 
        // TODO: Secrets: public Context<TLEncryptedChatBase> EncryptedChatsContext = new Context<TLEncryptedChatBase>(); 

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
                DialogsContext[dialog.Peer.Id] = dialog;

                var topMessage = dialog.TopMessageItem;

                // add in desc order by Date
                var isAdded = false;

                lock (_dialogsSyncRoot)
                {
                    for (var i = 0; i < Dialogs.Count; i++)
                    {
                        var d = Dialogs[i] as TLDialog;
                        // TODO: Secrets
                        //var ed = Dialogs[i] as TLEncryptedDialog;
                        if (d != null)
                        {
                            var currentTopMessage = d.TopMessageItem;

                            if (currentTopMessage != null && currentTopMessage.Date < topMessage.Date)
                            {
                                isAdded = true;
                                Dialogs.Insert(i, dialog);
                                break;
                            }
                        }
                        //else if (ed != null)
                        //{
                        //    if (ed.TopMessage != null && ed.TopMessage.Date < topMessage.Date)
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
                TLPeerChannel peerChannel;
                if (TLUtils.IsChannelMessage(message, out peerChannel))
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
            if (message.RandomId != null)
            {
                RandomMessagesContext.Remove(message.RandomId.Value);
            }
        }

        // TODO: Secrets
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
                TLPeerChannel peerChannel;
                if (TLUtils.IsChannelMessage(message, out peerChannel))
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
            else if (message.RandomId != 0)
            {
                RandomMessagesContext[message.RandomId.Value] = message;
            }
            else
            {
                throw new Exception("MsgId and RandomMsgId are zero");
            }
        }

        // TODO: Secrets: 
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
                UsersContext[user.Id] = user;
            }
        }

        public void AddChat(TLChatBase chat)
        {
            if (chat != null)
            {
                ChatsContext[chat.Id] = chat;
            }
        }

        // TODO: Secrets:
        //public void AddEncryptedChat(TLEncryptedChatBase chat)
        //{
        //    if (chat != null)
        //    {
        //        EncryptedChatsContext[chat.Id] = chat;
        //    }
        //}

        // NO MORE SUPPORTED: 
        //public void AddBroadcast(TLBroadcastChat broadcast)
        //{
        //    if (broadcast != null)
        //    {
        //        BroadcastsContext[broadcast.Index] = broadcast;
        //    }
        //}

        public void SaveSnapshot(string toDirectoryName)
        {
            var timer = Stopwatch.StartNew();

            CopyInternal(_usersSyncRoot, UsersMTProtoFileName, Path.Combine(toDirectoryName, UsersMTProtoFileName));
            CopyInternal(_chatsSyncRoot, ChatsMTProtoFileName, Path.Combine(toDirectoryName, ChatsMTProtoFileName));
            CopyInternal(_broadcastsSyncRoot, BroadcastsMTProtoFileName, Path.Combine(toDirectoryName, BroadcastsMTProtoFileName));
            CopyInternal(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName, Path.Combine(toDirectoryName, EncryptedChatsMTProtoFileName));
            CopyInternal(_dialogsFileSyncRoot, DialogsMTProtoFileName, Path.Combine(toDirectoryName, DialogsMTProtoFileName));

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
            CopyInternal(_broadcastsSyncRoot, Path.Combine(fromDirectoryName, BroadcastsMTProtoFileName), BroadcastsMTProtoFileName);
            CopyInternal(_encryptedChatsSyncRoot, Path.Combine(fromDirectoryName, EncryptedChatsMTProtoFileName), EncryptedChatsMTProtoFileName);
            CopyInternal(_dialogsFileSyncRoot, Path.Combine(fromDirectoryName, DialogsMTProtoFileName), DialogsMTProtoFileName);

            TLUtils.WritePerformance("Load DB snapshot time: " + timer.Elapsed);
        }

        public void Clear()
        {
            var timer = Stopwatch.StartNew();                   

            Dialogs.Clear();
            UsersContext.Clear();
            ChatsContext.Clear();
            // NO MORE SUPPORTED: BroadcastsContext.Clear();
            // TODO: Secrets: EncryptedChatsContext.Clear();
            DialogsContext.Clear();

            ClearInternal(_usersSyncRoot, UsersMTProtoFileName);
            ClearInternal(_chatsSyncRoot, ChatsMTProtoFileName);
            ClearInternal(_broadcastsSyncRoot, BroadcastsMTProtoFileName);
            ClearInternal(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName);
            ClearInternal(_dialogsFileSyncRoot, DialogsMTProtoFileName);

            TLUtils.WritePerformance("Clear DB time: " + timer.Elapsed);
        }

        private void ClearInternal(object syncRoot, string fileName)
        {
            FileUtils.Delete(syncRoot, fileName);
        }

        // TODO: Secrets:
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
        //                UnreadCount = isUnread ? new int?(1) : new int?(0)
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

                var commonMessage = message as TLMessage;
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

                            if (isUnread)
                            {
                                dialogBase.UnreadCount = dialogBase.UnreadCount + 1;
                            }

                            CorrectDialogOrder(dialogBase, message.Date, message.Id);

                            if (dialog.Messages[0] == commonMessage)
                            {
                                if (notifyTopMessageUpdated)
                                {
                                    dialog.TopMessageItem = commonMessage;
                                    Execute.BeginOnUIThread(() => dialog.RaisePropertyChanged(() => dialog.TopMessageItem));
                                }
                                else
                                {
                                    dialog.TopMessageItem = commonMessage;
                                }
                                dialog.TopMessage = commonMessage.Id;
                                dialog.TopMessageRandomId = commonMessage.RandomId;
                                if (_eventAggregator != null && notifyTopMessageUpdated)
                                {
                                   Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, commonMessage)));
                                }
                            }
                        }

                        // NO MORE SUPPORTED:
                        //var broadcast = dialogBase as TLBroadcastDialog;
                        //if (broadcast != null)
                        //{
                        //    if (broadcast.Messages.Count > 0)
                        //    {
                        //        var isAdded = insertAction(broadcast.Messages);

                        //        if (!isAdded)
                        //        {
                        //            broadcast.Messages.Add(commonMessage);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        broadcast.Messages.Add(commonMessage);
                        //    }

                        //    if (isUnread)
                        //    {
                        //        dialogBase.UnreadCount = dialogBase.UnreadCount + 1;
                        //    }

                        //    CorrectDialogOrder(dialogBase, message.Date, message.Id);

                        //    if (broadcast.Messages[0] == commonMessage)
                        //    {
                        //        if (notifyTopMessageUpdated)
                        //        {
                        //            broadcast._topMessage = commonMessage;
                        //            Helpers.Execute.BeginOnUIThread(() => broadcast.NotifyOfPropertyChange(() => broadcast.TopMessage));
                        //        }
                        //        else
                        //        {
                        //            broadcast._topMessage = commonMessage;
                        //        }
                        //        broadcast.TopMessageId = commonMessage.Id;
                        //        broadcast.TopMessageRandomId = commonMessage.RandomId;
                        //        if (_eventAggregator != null && notifyTopMessageUpdated)
                        //        {
                        //            _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialogBase, commonMessage));
                        //        }
                        //    }
                        //}
                    }
                    else
                    {
                        TLObject with;

                        TLPeerBase peer;

                        if (commonMessage.ToId is TLPeerChannel)
                        {
                            peer = commonMessage.ToId;
                        }
                        //else if (commonMessage.ToId is TLPeerBroadcast)
                        //{
                        //    peer = commonMessage.ToId;
                        //}
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
                                peer = new TLPeerUser { Id = commonMessage.FromId.Value };
                            }
                        }

                        if (peer is TLPeerChannel)
                        {
                            with = ChatsContext[peer.Id];
                        }
                        //else if (peer is TLPeerBroadcast)
                        //{
                        //    with = BroadcastsContext[peer.Id.Value];
                        //}
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

                        //if (peer is TLPeerBroadcast)
                        //{
                        //    addingDialog = new TLBroadcastDialog
                        //    {
                        //        Peer = peer,
                        //        With = with,
                        //        Messages = new ObservableCollection<TLMessageBase> { commonMessage },
                        //        TopMessageId = commonMessage.Id,
                        //        TopMessageRandomId = commonMessage.RandomId,
                        //        TopMessage = commonMessage,
                        //        UnreadCount = isUnread ? new int?(1) : new int?(0)
                        //    };
                        //}
                        //else
                        {
                            addingDialog = new TLDialog
                            {
                                Peer = peer,
                                With = with,
                                Messages = new ObservableCollection<TLMessageBase> { commonMessage },
                                TopMessage = commonMessage.Id,
                                TopMessageRandomId = commonMessage.RandomId,
                                TopMessageItem = commonMessage,
                                UnreadCount = isUnread ? 1 : 0,
                                ReadInboxMaxId = 0
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
                            DialogsContext[addingDialog.Peer.Id] = addingDialog;
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

        // TODO: Secrets: 
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

        // TODO: Secrets:
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

        // TODO: Secrets: 
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

        private TLDialog GetDialog(TLPeerBase peer)
        {
            lock (_dialogsSyncRoot)
            {
                foreach (var d in Dialogs)
                {
                    if (d.Peer.Id == peer.Id)
                    {
                        if (d.Peer is TLPeerChannel && peer is TLPeerChannel)
                        {
                            return d as TLDialog;
                        }

                        if (d.Peer is TLPeerChat && peer is TLPeerChat)
                        {
                            return d as TLDialog;
                        }

                        if (d.Peer is TLPeerUser && peer is TLPeerUser)
                        {
                            return d as TLDialog;
                        }

                        //if (d.Peer is TLPeerBroadcast && peer is TLPeerBroadcast)
                        //{
                        //    return d as TLBroadcastDialog;
                        //}
                    }
                }
            }
            return null;
        }

        private TLDialog GetDialog(TLMessage commonMessage)
        {
            lock (_dialogsSyncRoot)
            {
                foreach (var d in Dialogs)
                {
                    if (d.Peer is TLPeerChat &&
                        commonMessage.ToId is TLPeerChat &&
                        d.Peer.Id == commonMessage.ToId.Id)
                    {
                        return d as TLDialog;
                    }

                    if (d.Peer is TLPeerUser &&
                        commonMessage.ToId is TLPeerUser &&
                        commonMessage.IsOut &&
                        d.Peer.Id == commonMessage.ToId.Id)
                    {
                        return d as TLDialog;
                    }

                    if (d.Peer is TLPeerUser &&
                        commonMessage.ToId is TLPeerUser &&
                        !commonMessage.IsOut &&
                        d.Peer.Id == commonMessage.FromId)
                    {
                        return d as TLDialog;
                    }

                    //if (d.Peer is TLPeerBroadcast &&
                    //    commonMessage.ToId is TLPeerBroadcast &&
                    //    d.Peer.Id.Value == commonMessage.ToId.Id.Value)
                    //{
                    //    return d as TLBroadcastDialog;
                    //}

                    if (d.Peer is TLPeerChannel &&
                        commonMessage.ToId is TLPeerChannel &&
                        d.Peer.Id == commonMessage.ToId.Id)
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

        public void UpdateSendingMessage(TLMessageBase message, TLMessageBase cachedMessage, TLPeerBase peer)
        {
            RemoveMessageFromContext(cachedMessage);

            cachedMessage.Update(message);
            cachedMessage.RandomId = null;

            AddMessageToContext(cachedMessage);

            var commonMessage = message as TLMessage;
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
                        if (dialog.TopMessage < cachedMessage.Id)
                        {
                            dialog.TopMessageItem = cachedMessage;
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

        // TODO: Secrets: 
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
            AddMessageCommon(message, true, true, (messages) =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (messages[i] == previousMessage)
                    {
                        messages.Insert(i, message);
                        return true;
                    }
                }

                // TODO: Verify this IF
                //if (previousMessage != null)
                //{
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
                //}

                return false;
            });
        }

        public void AddSendingMessage(TLMessageBase message, TLMessageBase previousMessage, bool notifyNewDialog, bool notifyTopMessageUpdated)
        {
            AddMessageCommon(message, notifyNewDialog, notifyTopMessageUpdated, (messages) =>
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

        // TODO: Secrets: 
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
            AddMessageCommon(message, notifyNewDialogs, notifyNewMessageUpdated, (messages) =>
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
                            return true;
                        }
                    }
                }

                return false;
            });
        }

        // TODO: Secrets: 
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
        //                RandomBytes = new string(""),
        //                ChatId = chat.Id,
        //                Action = new TLDecryptedMessageActionEmpty(),
        //                FromId = MTProtoService.Instance.CurrentUserId,
        //                Date = chat.Date,
        //                Out = new bool(false),
        //                Unread = new bool(false),
        //                Status = MessageStatus.Read
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

        // NO MORE SUPPORTED:
        //public void ClearBroadcastHistory(int? chatId)
        //{
        //    var dialog = DialogsContext[chatId.Value] as TLBroadcastDialog;
        //    if (dialog != null)
        //    {

        //        lock (_dialogsSyncRoot)
        //        {
        //            foreach (var message in dialog.Messages)
        //            {
        //                RemoveMessageFromContext(message);
        //            }

        //            dialog.Messages.Clear();

        //            var topMessage = dialog.TopMessage as TLMessageCommon;
        //            if (topMessage == null) return;

        //            var broadcastChat = InMemoryCacheService.Instance.GetBroadcast(chatId);
        //            if (broadcastChat== null) return;

        //            var serviceMessage = new TLMessageService17
        //            {
        //                FromId = topMessage.FromId,
        //                ToId = topMessage.ToId,
        //                Status = MessageStatus.Confirmed,
        //                Out = new bool { Value = true },
        //                Date = topMessage.Date,
        //                //IsAnimated = true,
        //                RandomId = TLLong.Random(),
        //                Action = new TLMessageActionChatCreate
        //                {
        //                    Title = broadcastChat.Title,
        //                    Users = broadcastChat.ParticipantIds
        //                }
        //            };
        //            serviceMessage.SetUnread(new bool(false));

        //            dialog.Messages.Add(serviceMessage);
        //            AddMessageToContext(serviceMessage);

        //            dialog.TopMessage = serviceMessage;
        //            dialog.TopMessageRandomId = serviceMessage.RandomId;

        //            CorrectDialogOrder(dialog, dialog.TopMessage.DateIndex, dialog.TopMessage.Index);
        //        }

        //        if (_eventAggregator != null)
        //        {
        //            _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, dialog.TopMessage));
        //        }
        //    }
        //}

        // TODO: Secrets: 
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
                    // NO MORE SUPPORTED:
                    //var broadcast = dialogBase as TLBroadcastDialog;
                    //if (broadcast != null)
                    //{
                    //    for (var i = 0; i < messageIds.Count; i++)
                    //    {
                    //        for (var j = 0; j < broadcast.Messages.Count; j++)
                    //        {
                    //            if (messageIds[i].Value == broadcast.Messages[j].Index)
                    //            {
                    //                broadcast.Messages.RemoveAt(j);
                    //                break;
                    //            }
                    //        }
                    //    }

                    //    if (broadcast.Messages.Count == 0)
                    //    {
                    //        if (lastMessage == null)
                    //        {
                    //            DialogsContext.Remove(dialogBase.Index);
                    //            Dialogs.Remove(dialogBase);
                    //            isDialogRemoved = true;
                    //        }
                    //        else
                    //        {
                    //            broadcast.Messages.Add(lastMessage);
                    //            broadcast._topMessage = broadcast.Messages[0];
                    //            broadcast.TopMessageId = broadcast.Messages[0].Id;
                    //            broadcast.TopMessageRandomId = broadcast.Messages[0].RandomId;
                    //            isTopMessageUpdated = true;
                    //            topMessage = broadcast.TopMessage;
                    //        }
                    //    }
                    //    else
                    //    {
                    //        broadcast._topMessage = broadcast.Messages[0];
                    //        broadcast.TopMessageId = broadcast.Messages[0].Id;
                    //        broadcast.TopMessageRandomId = broadcast.Messages[0].RandomId;
                    //        isTopMessageUpdated = true;
                    //        topMessage = broadcast.TopMessage;
                    //    }

                    //    CorrectDialogOrder(broadcast, broadcast.TopMessage.DateIndex, broadcast.TopMessage.Index);
                    //}

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
                                DialogsContext.Remove(dialogBase.Peer.Id);
                                Dialogs.Remove(dialogBase);
                                isDialogRemoved = true;
                            }
                            else
                            {
                                dialog.Messages.Add(lastMessage);
                                dialog.TopMessageItem = dialog.Messages[0];
                                dialog.TopMessage = dialog.Messages[0].Id;
                                dialog.TopMessageRandomId = dialog.Messages[0].RandomId;
                                isTopMessageUpdated = true;
                                topMessage = dialog.TopMessageItem;
                            }
                        }
                        else
                        {
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

        public void DeleteMessage(TLMessageBase message, TLPeerBase peer)
        {
            if (message != null)
            {
                RemoveMessageFromContext(message);
                //MessagesContext.Remove(message.Index);

                var commonMessage = message as TLMessage;
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
                        // NO MORE SUPPORTED:
                        //var broadcast = dialogBase as TLBroadcastDialog;
                        //if (broadcast != null)
                        //{
                        //    broadcast.Messages.Remove(commonMessage);
                        //    isMessageRemoved = true;

                        //    if (broadcast.Messages.Count == 0)
                        //    {
                        //        DialogsContext.Remove(dialogBase.Index);
                        //        Dialogs.Remove(dialogBase);
                        //        isDialogRemoved = true;
                        //    }
                        //    else
                        //    {
                        //        broadcast.TopMessage = broadcast.Messages[0];
                        //        broadcast.TopMessageId = broadcast.Messages[0].Id;
                        //        broadcast.TopMessageRandomId = broadcast.Messages[0].RandomId;
                        //        isTopMessageUpdated = true;
                        //        topMessage = broadcast.TopMessage;
                        //    }

                        //    CorrectDialogOrder(broadcast, broadcast.TopMessage.DateIndex, broadcast.TopMessage.Index);
                        //}
                        
                        var dialog = dialogBase as TLDialog;
                        if (dialog != null)
                        {
                            dialog.Messages.Remove(commonMessage);
                            isMessageRemoved = true;

                            if (dialog.Messages.Count == 0)
                            {
                                DialogsContext.Remove(dialogBase.Peer.Id);
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

                lock (_usersSyncRoot)
                {
                    foreach (var dialog in Dialogs)
                    {
                        if (!dialog.IsChat && dialog.WithId == index)
                        {
                            dialog.With = user;
                        }
                    }
                }
            }
        }

        public void DeleteUser(int id)
        {
            UsersContext.Remove(id);
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
                        if (dialog.Peer is TLPeerChat && dialog.WithId == index)
                        {
                            dialog.With = chat;
                        }
                    }
                }
            }
        }

        // NO MORE SUPPORTED
        //public void ReplaceBroadcast(int index, TLBroadcastChat chat)
        //{
        //    var cachedChat = BroadcastsContext[index];
        //    if (cachedChat == null)
        //    {
        //        BroadcastsContext[index] = chat;
        //    }
        //    else
        //    {
        //        BroadcastsContext[index] = chat;

        //        lock (_dialogsSyncRoot)
        //        {
        //            foreach (var dialog in Dialogs)
        //            {
        //                if (dialog.Peer is TLPeerChat && dialog.WithId == index)
        //                {
        //                    dialog._with = chat;
        //                }
        //            }
        //        }
        //    }
        //}

        // TODO: Secrets: 
        //public void ReplaceEncryptedChat(int index, TLEncryptedChatBase chat)
        //{
        //    var cachedChat = EncryptedChatsContext[index];
        //    if (cachedChat == null)
        //    {
        //        EncryptedChatsContext[index] = chat;
        //    }
        //    else
        //    {
        //        EncryptedChatsContext[index] = chat;

        //        //lock (_encryptedDialogsSyncRoot)
        //        //{
        //        //    foreach (var dialog in EncryptedDialogs)
        //        //    {
        //        //        if (dialog.IsChat
        //        //            && dialog.WithId == index)
        //        //        {
        //        //            dialog.With = chat;
        //        //        }
        //        //    }
        //        //}
        //    }
        //}

        public void DeleteChat(int id)
        {
            ChatsContext.Remove(id);
        }

        public void DeleteChat(TLChatBase chat)
        {
            ChatsContext.Remove(chat.Id);
        }

        public void DeleteDialog(TLDialog dialog)
        {
            lock (_dialogsSyncRoot)
            {
                var dbDialog = Dialogs.FirstOrDefault(x => x.Peer?.Id == dialog.Peer?.Id);

                Dialogs.Remove(dbDialog);
            }
        }

        public void Open()
        {
            try
            {
                var timer = Stopwatch.StartNew();

                var usersTimer = Stopwatch.StartNew();
                var users = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLUserBase>>(_usersSyncRoot, UsersMTProtoFileName) ?? new TLVector<TLUserBase>();
                TLUtils.WritePerformance(string.Format("Open users time ({0}) : {1}", users.Count, usersTimer.Elapsed));
            
                UsersContext = new Context<TLUserBase>(users, x => x.Id);

                var chatsTimer = Stopwatch.StartNew();
                var chats = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLChatBase>>(_chatsSyncRoot, ChatsMTProtoFileName) ?? new TLVector<TLChatBase>();
                TLUtils.WritePerformance(string.Format("Open chats time ({0}) : {1}", chats.Count, chatsTimer.Elapsed));
            
                ChatsContext = new Context<TLChatBase>(chats, x => x.Id);

                // NO MORE SUPPORTED:
                //var broadcastsTimer = Stopwatch.StartNew();
                //var broadcasts = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLBroadcastChat>>(_broadcastsSyncRoot, BroadcastsMTProtoFileName) ?? new TLVector<TLBroadcastChat>();
                //TLUtils.WritePerformance(string.Format("Open broadcasts time ({0}) : {1}", broadcasts.Count, broadcastsTimer.Elapsed));

                //BroadcastsContext = new Context<TLBroadcastChat>(broadcasts, x => x.Index);

                // TODO: Secrets: 
                //var encryptedChatsTimer = Stopwatch.StartNew();
                //var encryptedChats = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLEncryptedChatBase>>(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName) ?? new TLVector<TLEncryptedChatBase>();
                //TLUtils.WritePerformance(string.Format("Open encrypted chats time ({0}) : {1}", encryptedChats.Count, encryptedChatsTimer.Elapsed));

                //EncryptedChatsContext = new Context<TLEncryptedChatBase>(encryptedChats, x => x.Id);

                MessagesContext.Clear();
                ChannelsContext.Clear();
                lock (_dialogsSyncRoot)
                {

                    var dialogsTimer = Stopwatch.StartNew();
                    var dialogs = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLDialog>>(_dialogsFileSyncRoot, DialogsMTProtoFileName) ?? new TLVector<TLDialog>();
                    Dialogs = new ObservableCollection<TLDialog>(dialogs);
                    TLUtils.WritePerformance(string.Format("Open dialogs time ({0}) : {1}", dialogs.Count, dialogsTimer.Elapsed));
            
                    DialogsContext = new Context<TLDialog>(Dialogs, x => x.Peer.Id);


                    ResendingMessages = new List<TLMessageBase>();
                    //ResendingDecryptedMessages = new List<TLDecryptedMessageBase>();
                    foreach (var d in Dialogs)
                    {
                        //var encryptedDialog = d as TLEncryptedDialog;
                        //var broadcastDialog = d as TLBroadcastDialog;
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
                        //        if (message.Status == MessageStatus.Sending
                        //            || message.Status == MessageStatus.Compressing)
                        //        {
                        //            message.Status = MessageStatus.Failed;
                        //            ResendingDecryptedMessages.Add(message);
                        //        }

                        //        if (message.RandomIndex != 0)
                        //        {
                        //            DecryptedMessagesContext[message.RandomIndex] = message;
                        //        }
                        //    }
                        //}
                        //else if (broadcastDialog != null)
                        //{
                        //    var dialog = broadcastDialog;
                        //    var peer = dialog.Peer;
                        //    var broadcastChat = ChatsContext[peer.Id.Value];
                        //    if (broadcastChat != null)
                        //    {
                        //        dialog._with = broadcastChat;
                        //    }
                        //    else
                        //    {
                        //        broadcastChat = dialog.With as TLBroadcastChat;
                        //        if (broadcastChat != null)
                        //        {
                        //            ChatsContext[broadcastChat.Index] = broadcastChat;
                        //        }
                        //    }

                        //    dialog._topMessage = dialog.Messages.FirstOrDefault();
                        //    dialog.TopMessageId = dialog.TopMessage != null ? dialog.TopMessage.Id : null;
                        //    if (dialog.TopMessageId == null)
                        //    {
                        //        dialog.TopMessageRandomId = dialog.TopMessage != null ? dialog.TopMessage.RandomId : null;
                        //    }
                        //    foreach (var message in dialog.Messages)
                        //    {
                        //        if (message.Status == MessageStatus.Sending
                        //            || message.Status == MessageStatus.Compressing)
                        //        {
                        //            message._status = message.Index != 0 ? MessageStatus.Confirmed : MessageStatus.Failed;
                        //            ResendingMessages.Add(message);
                        //        }

                        //        AddMessageToContext(message);
                        //        if (message.RandomIndex != 0)
                        //        {
                        //            RandomMessagesContext[message.RandomIndex] = message;
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
                                    dialog.With = user;
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
                                    dialog.With = chat;
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
                            //else if (peer is TLPeerBroadcast)
                            //{
                            //    var broadcastChat = ChatsContext[peer.Id];
                            //    if (broadcastChat != null)
                            //    {
                            //        dialog._with = broadcastChat;
                            //    }
                            //    else
                            //    {
                            //        broadcastChat = dialog.With as TLBroadcastChat;
                            //        if (broadcastChat != null)
                            //        {
                            //            ChatsContext[broadcastChat.Id] = broadcastChat;
                            //        }
                            //    }
                            //}
                            else if (peer is TLPeerChannel)
                            {
                                var channel = ChatsContext[peer.Id];
                                if (channel != null)
                                {
                                    dialog.With = channel;
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

                            dialog.TopMessageItem = dialog.Messages.FirstOrDefault();
                            //dialog.TopMessageId = dialog.TopMessage != null ? dialog.TopMessage.Id : null;
                            dialog.TopMessage = dialog.TopMessageItem != null ? dialog.TopMessageItem.Id : 0;
                            if (dialog.TopMessage == 0)
                            {
                                dialog.TopMessageRandomId = dialog.TopMessageItem != null ? dialog.TopMessageItem.RandomId : null;
                            }
                            foreach (var message in dialog.Messages)
                            {
                                if (message.State == TLMessageState.Sending
                                    || message.State == TLMessageState.Compressing)
                                {
                                    message.State = message.Id != 0 ? TLMessageState.Confirmed : TLMessageState.Failed;
                                    ResendingMessages.Add(message);
                                }

                                AddMessageToContext(message);
                                if (message.RandomId != 0)
                                {
                                    RandomMessagesContext[message.RandomId.Value] = message;
                                }
                            }
                        }
                        
                    }
                }
                _isOpened = true;

                TLUtils.WritePerformance("Open DB time: " + timer.Elapsed);
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR: open DB error", LogSeverity.Error);
                TLUtils.WriteException(e);
            }
        }

        public IList<TLMessageBase> ResendingMessages { get; protected set; }
        // TODO: Secrets: public IList<TLDecryptedMessageBase> ResendingDecryptedMessages { get; protected set; }  



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
            List<TLDialog> dialogs;
            int messagesCount = 0;
            lock (_dialogsSyncRoot)
            {
                dialogs = Dialogs.OrderByDescending(x => x.GetDateIndex()).Take(Constants.CachedDialogsCount).ToList();
                foreach (var d in dialogs)
                {
                    var dialog = d as TLDialog;
                    if (dialog != null)
                    {
                        dialog.Messages = new ObservableCollection<TLMessageBase>(dialog.Messages.Take(Constants.CachedMessagesCount));
                        messagesCount += dialog.Messages.Count;
                    }
                }
            }

            var mtProtoDialogs = new TLVector<TLDialog>(dialogs);
            TLUtils.SaveObjectToMTProtoFile(_dialogsFileSyncRoot, DialogsMTProtoFileName, mtProtoDialogs);
            TLUtils.SaveObjectToMTProtoFile(_usersSyncRoot, UsersMTProtoFileName, new TLVector<TLUserBase>(UsersContext.Values.ToList()));
            TLUtils.SaveObjectToMTProtoFile(_chatsSyncRoot, ChatsMTProtoFileName, new TLVector<TLChatBase>(ChatsContext.Values.ToList()));
            // NO MORE SUPPORTED: TLUtils.SaveObjectToMTProtoFile(_broadcastsSyncRoot, BroadcastsMTProtoFileName, new TLVector<TLBroadcastChat> { Items = BroadcastsContext.Values.ToList() });
            // TODO: Secrets: TLUtils.SaveObjectToMTProtoFile(_encryptedChatsSyncRoot, EncryptedChatsMTProtoFileName, new TLVector<TLEncryptedChatBase> { Items = EncryptedChatsContext.Values.ToList() });

            HasChanges = false;

            TLUtils.WritePerformance(string.Format("Commit DB time ({0}d, {1}u, {2}c, {6}b, {5}ec, {4}m): {3}", dialogs.Count, UsersContext.Count, ChatsContext.Count, timer.Elapsed, messagesCount, 0, 0 /*EncryptedChatsContext.Count, BroadcastsContext.Count*/ ));
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
            //else if (typeof(T) == typeof(TLBroadcastChat))
            //{
            //    result += BroadcastsContext.Count;
            //}
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
    }
}