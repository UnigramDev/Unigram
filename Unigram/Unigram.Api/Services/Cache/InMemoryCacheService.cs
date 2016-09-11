using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Action = System.Action;


namespace Telegram.Api.Services.Cache
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly object _databaseSyncRoot = new object();

        private InMemoryDatabase _database;

        private Context<TLUserBase> UsersContext
        {
            get { return _database != null ? _database.UsersContext : null; }
        }

        private Context<TLChatBase> ChatsContext
        {
            get { return _database != null ? _database.ChatsContext : null; }
        }

        // DEPRECATED:
        //private Context<TLBroadcastChat> BroadcastsContext
        //{
        //    get { return _database != null ? _database.BroadcastsContext : null; }
        //} 

        // TODO: Secrets: 
        //private Context<TLEncryptedChatBase> EncryptedChatsContext
        //{
        //    get { return _database != null ? _database.EncryptedChatsContext : null; }
        //} 

        private Context<TLMessageBase> MessagesContext
        {
            get { return _database != null ? _database.MessagesContext : null; }
        }

        private Context<Context<TLMessageBase>> ChannelsContext
        {
            get { return _database != null ? _database.ChannelsContext : null; }
        }

        // TODO: Secrets: 
        //private Context<TLDecryptedMessageBase> DecryptedMessagesContext
        //{
        //    get { return _database != null ? _database.DecryptedMessagesContext : null; }
        //} 

        private Context<TLMessageBase> RandomMessagesContext
        {
            get { return _database != null ? _database.RandomMessagesContext : null; }
        }

        private Context<TLDialog> DialogsContext
        {
            get { return _database != null ? _database.DialogsContext : null; }
        } 

        public void Initialize()
        {
            _database = new InMemoryDatabase(_eventAggregator);
            _database.Open();
        }

        private readonly ITelegramEventAggregator _eventAggregator;

        public static ICacheService Instance { get; protected set; }

        public InMemoryCacheService(ITelegramEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            Instance = this;
        }

        public IList<TLDialog> GetDialogs()
        {
            var result = new List<TLDialog>();

            if (_database == null) Initialize();

            if (DialogsContext == null)
            {

                return result;
            }
            var timer = Stopwatch.StartNew();

            IList<TLDialog> dialogs = new ObservableCollection<TLDialog>();

            try
            {
                dialogs = _database.Dialogs;

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
            }

            TLUtils.WritePerformance(string.Format("GetCachedDialogs time ({0} from {1}): {2}", dialogs.Count, _database.CountRecords<TLDialog>(), timer.Elapsed));
            return dialogs.OrderByDescending(x => x.GetDateIndex()).ToList();
        }


        public void GetDialogsAsync(Action<IList<TLDialog>> callback)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var result = new List<TLDialog>();

                if (_database == null) Initialize();

                if (DialogsContext == null)
                {
                    callback(result);
                    return;
                }
                var timer = Stopwatch.StartNew();

                IList<TLDialog> dialogs = new ObservableCollection<TLDialog>();

                try
                {
                    dialogs = _database.Dialogs;

                }
                catch (Exception e)
                {
                    TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                    TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
                }

                TLUtils.WritePerformance(string.Format("GetCachedDialogs time ({0} from {1}): {2}", dialogs.Count, _database.CountRecords<TLDialog>(), timer.Elapsed));
                callback(dialogs.OrderByDescending(x => x.GetDateIndex()).ToList());
            });
        }

        public List<TLUserBase> GetUsers()
        {
            var result = new List<TLUserBase>();

            if (_database == null) Initialize();

            if (UsersContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();

            var contacts = new List<TLUserBase>();

            try
            {
                contacts = _database.UsersContext.Values.ToList();

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            TLUtils.WritePerformance(string.Format("GetCachedContacts time ({0} from {1}): {2}", contacts.Count, _database.CountRecords<TLUserBase>(), timer.Elapsed));
            return contacts;
        }

        public List<TLUserBase> GetContacts()
        {
            var result = new List<TLUserBase>();

            if (_database == null) Initialize();

            if (UsersContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();

            var contacts = new List<TLUserBase>();

            try
            {
                //contacts = _database.UsersContext.Values.Where(x => x is TLUserContact).ToList();
                contacts = _database.UsersContext.Values.Where(x => x.Contact != null).ToList();

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            TLUtils.WritePerformance(string.Format("GetCachedContacts time ({0} from {1}): {2}", contacts.Count, _database.CountRecords<TLUserBase>(), timer.Elapsed));
            return contacts;
        }

        public List<TLUserBase> GetUsersForSearch(IList<TLDialog> nonCachedDialogs)
        {
            var result = new List<TLUserBase>();

            if (_database == null) Initialize();

            if (UsersContext == null)
            {
                return result;
            }

            var contacts = new List<TLUserBase>();
            try
            {
                var usersCache = new Dictionary<long, long>();

                if (nonCachedDialogs != null)
                {
                    for (var i = 0; i < nonCachedDialogs.Count; i++)
                    {
                        var dialog = nonCachedDialogs[i] as TLDialog;
                        if (dialog != null)
                        {
                            var user = nonCachedDialogs[i].With as TLUserBase;
                            if (user != null)
                            {
                                if (!usersCache.ContainsKey(user.Id))
                                {
                                    usersCache[user.Id] = user.Id;
                                    contacts.Add(user);
                                }
                            }
                        }
                    }
                }

                var dialogs = new List<TLDialog>(_database.Dialogs);

                for (var i = 0; i < dialogs.Count; i++)
                {
                    var dialog = dialogs[i] as TLDialog;
                    if (dialog != null)
                    {
                        var user = dialogs[i].With as TLUserBase;
                        if (user != null)
                        {
                            if (!usersCache.ContainsKey(user.Id))
                            {
                                usersCache[user.Id] = user.Id;
                                contacts.Add(user);
                            }
                        }
                    }
                }

                //var unsortedContacts = _database.UsersContext.Values.Where(x => x is TLUserContact).ToList();
                var unsortedContacts = _database.UsersContext.Values.Where(x => x.Contact != null).ToList();
                for (var i = 0; i < unsortedContacts.Count; i++)
                {
                    var user = unsortedContacts[i];
                    if (!usersCache.ContainsKey(user.Id))
                    {
                        usersCache[user.Id] = user.Id;
                        contacts.Add(user);
                    }
                }
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            return contacts;
        }

        public void GetContactsAsync(Action<IList<TLUserBase>> callback)
        {
            Execute.BeginOnThreadPool(
                () =>
                {
                    var result = new List<TLUserBase>();

                    if (_database == null) Initialize();

                    if (UsersContext == null)
                    {
                        callback(result);
                        return;
                    }
                    var timer = Stopwatch.StartNew();

                    IList<TLUserBase> contacts = new List<TLUserBase>();

                    try
                    {
                        //contacts = _database.UsersContext.Values.Where(x => x is TLUserContact).ToList();
                        contacts = _database.UsersContext.Values.Where(x => x.Contact != null).ToList();

                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                        TLUtils.WriteException(e);
                    }

                    TLUtils.WritePerformance(string.Format("GetCachedContacts time ({0} from {1}): {2}", contacts.Count, _database.CountRecords<TLUserBase>(), timer.Elapsed));
                    callback(contacts);
                });
        }

        public List<TLChatBase> GetChats()
        {
            var result = new List<TLChatBase>();

            if (_database == null) Initialize();

            if (ChatsContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();

            IList<TLChatBase> chats = new List<TLChatBase>();

            try
            {
                result = _database.ChatsContext.Values.ToList();

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            TLUtils.WritePerformance(string.Format("GetCachedChats time ({0} from {1}): {2}", chats.Count, _database.CountRecords<TLChatBase>(), timer.Elapsed));

            return result;
        }

        public void GetChatsAsync(Action<IList<TLChatBase>> callback)
        {
            Execute.BeginOnThreadPool(
                () =>
                {
                    var result = new List<TLChatBase>();

                    if (_database == null) Initialize();

                    if (ChatsContext == null)
                    {
                        callback(result);
                        return;
                    }
                    var timer = Stopwatch.StartNew();

                    IList<TLChatBase> chats = new List<TLChatBase>();

                    try
                    {
                        chats = _database.ChatsContext.Values.ToList();

                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                        TLUtils.WriteException(e);
                    }

                    TLUtils.WritePerformance(string.Format("GetCachedChats time ({0} from {1}): {2}", chats.Count, _database.CountRecords<TLChatBase>(), timer.Elapsed));
                    callback(chats);
                });
        }

        public TLChatBase GetChat(int id)
        {
            if (_database == null)
            {
                Initialize();
            }

            return ChatsContext[id];
        }

        // DEPRECATED
        //public TLBroadcastChat GetBroadcast(int? id)
        //{
        //    if (_database == null)
        //    {
        //        Init();
        //    }

        //    return BroadcastsContext[id.Value];
        //}

        // TODO: Secrets: 
        //public TLEncryptedChatBase GetEncryptedChat(int? id)
        //{
        //    if (_database == null)
        //    {
        //        Init();
        //    }

        //    return EncryptedChatsContext[id.Value];
        //}

        public TLUserBase GetUser(int id)
        {
            if (_database == null)
            {
                Initialize();
            }

            return UsersContext[id];
        }

        public TLUserBase GetUser(TLUserProfilePhoto photo)
        {
            // TODO: Maybe replace everywhere TLUserBase with TLUser
            return UsersContext.Values.OfType<TLUser>().FirstOrDefault(x => x.Photo == photo);
        }

        public TLMessageBase GetMessage(int id, int? channelId = null)
        {
            if (channelId != null)
            {
                var channelContext = ChannelsContext[channelId.Value];
                if (channelContext != null)
                {
                    return channelContext[id];
                }

                return null;
            }

            return MessagesContext[id];
        }

        public TLMessageBase GetMessage(long randomId)
        {
            return RandomMessagesContext[randomId];
        }

        public TLMessageBase GetMessage(TLWebPageBase webPageBase)
        {
            var m = MessagesContext.Values.FirstOrDefault(x =>
            {
                var message = x as TLMessage;
                if (message != null)
                {
                    var webPageMedia = message.Media as TLMessageMediaWebPage;
                    if (webPageMedia != null)
                    {
                        var currentWebPage = webPageMedia.Webpage;
                        if (currentWebPage != null && currentWebPage.Id == webPageBase.Id)
                        {
                            return true;
                        }
                    }
                }

                return false;
            });

            if (m != null) return m;

            foreach (var channelContext in ChannelsContext.Values)
            {
                foreach (var x in channelContext.Values)
                {
                    var message = x as TLMessage;
                    if (message != null)
                    {
                        var webPageMedia = message.Media as TLMessageMediaWebPage;
                        if (webPageMedia != null)
                        {
                            var currentWebPage = webPageMedia.Webpage;
                            if (currentWebPage != null && currentWebPage.Id == webPageBase.Id)
                            {
                                m = message;
                                break;
                            }
                        }
                    }
                }
            }

            if (m != null) return m;

            m = RandomMessagesContext.Values.FirstOrDefault(x =>
            {
                var message = x as TLMessage;
                if (message != null)
                {
                    var webPageMedia = message.Media as TLMessageMediaWebPage;
                    if (webPageMedia != null)
                    {
                        var currentWebPage = webPageMedia.Webpage;
                        if (currentWebPage != null && currentWebPage.Id == webPageBase.Id)
                        {
                            return true;
                        }
                    }
                }

                return false;
            });

            return m;
        }

        public TLDialog GetDialog(TLMessage message)
        {
            TLPeerBase peer;
            if (message.ToId is TLPeerChat)
            {
                peer = message.ToId;
            }
            else
            {
                peer = message.IsOut ? message.ToId : new TLPeerUser{ Id = message.FromId.Value };
            }
            return GetDialog(peer);
        }

        public TLDialog GetDialog(TLPeerBase peer)
        {
            return _database.Dialogs.OfType<TLDialog>().FirstOrDefault(x => x.WithId == peer.Id && x.IsChat == peer is TLPeerChat);
        }

        // TODO: Secrets: 
        //public TLDialogBase GetEncryptedDialog(int? chatId)
        //{
        //    return _database.Dialogs.OfType<TLEncryptedDialog>().FirstOrDefault(x => x.Index == chatId.Value);
        //}

        public TLChat GetChat(TLChatPhoto chatPhoto)
        {
            return _database.ChatsContext.Values.FirstOrDefault(x => x is TLChat && ((TLChat)x).Photo == chatPhoto) as TLChat;
        }

        public TLChannel GetChannel(TLChatPhoto chatPhoto)
        {
            return _database.ChatsContext.Values.FirstOrDefault(x => x is TLChannel && ((TLChannel)x).Photo == chatPhoto) as TLChannel;
        }

        public IList<TLMessageBase> GetHistory(int dialogIndex)
        {
            var result = new List<TLMessageBase>();

            if (_database == null) Initialize();

            // TODO: Secrets: 
            if (/*DecryptedMessagesContext == null ||*/ DialogsContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();


            IList<TLMessageBase> msgs = new List<TLMessageBase>();
            try
            {
                var dialog = DialogsContext[dialogIndex] as TLDialog;

                if (dialog != null)
                {
                    msgs = dialog.Messages
                        .OfType<TLMessage>()
                        //.Where(x =>

                            //x.FromId.Value == currentUserId.Value && x.ToId.Id.Value == peer.Id.Value           // to peer from current
                        //|| x.FromId.Value == peer.Id.Value && x.ToId.Id.Value == currentUserId.Value) // from peer to current

                            .Cast<TLMessageBase>()
                            .ToList();
                }

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            //TLUtils.WritePerformance(string.Format("GetCachedHistory time ({0}): {1}", _database.CountRecords<TLMessageBase>(), timer.Elapsed));
            return msgs.Take(Constants.CachedMessagesCount).ToList();
        }

        // TODO: Secrets: 
        //public IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogIndex, int limit = Constants.CachedMessagesCount)
        //{
        //    var result = new List<TLDecryptedMessageBase>();

        //    if (_database == null) Init();

        //    if (MessagesContext == null || DialogsContext == null)
        //    {
        //        return result;
        //    }

        //    IList<TLDecryptedMessageBase> msgs = new List<TLDecryptedMessageBase>();
        //    try
        //    {
        //        var dialog = DialogsContext[dialogIndex] as TLEncryptedDialog;

        //        if (dialog != null)
        //        {
        //            msgs = dialog.Messages.ToList();
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
        //        TLUtils.WriteException(e);
        //    }

        //    var returnedMessages = new List<TLDecryptedMessageBase>();
        //    var count = 0;
        //    for (var i = 0; i < msgs.Count && count < limit; i++)
        //    {
        //        returnedMessages.Add(msgs[i]);
        //        if (TLUtils.IsDisplayedDecryptedMessage(msgs[i]))
        //        {
        //            count++;
        //        }
        //    }

        //    return returnedMessages;
        //}

        // TODO: Secrets: 
        //public IList<TLDecryptedMessageBase> GetDecryptedHistory(int dialogIndex, long randomId, int limit = Constants.CachedMessagesCount)
        //{
        //    var result = new List<TLDecryptedMessageBase>();

        //    if (_database == null) Init();

        //    if (MessagesContext == null || DialogsContext == null)
        //    {
        //        return result;
        //    }

        //    IList<TLDecryptedMessageBase> msgs = new List<TLDecryptedMessageBase>();
        //    try
        //    {
        //        var dialog = DialogsContext[dialogIndex] as TLEncryptedDialog;

        //        if (dialog != null)
        //        {
        //            msgs = dialog.Messages.ToList();
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
        //        TLUtils.WriteException(e);
        //    }

        //    var skipCount = 0;
        //    if (randomId != 0)
        //    {
        //        skipCount = 1;
        //        for (var i = 0; i < msgs.Count; i++)
        //        {
        //            if (msgs[i].RandomIndex != randomId)
        //            {
        //                skipCount++;
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }
        //    }

        //    var returnedMessages = new List<TLDecryptedMessageBase>();
        //    var count = 0;
        //    for (var i = skipCount; i < msgs.Count && count < limit; i++)
        //    {
        //        returnedMessages.Add(msgs[i]);
        //        if (TLUtils.IsDisplayedDecryptedMessage(msgs[i]))
        //        {
        //            count++;
        //        }
        //    }

        //    return returnedMessages;
        //} 

        public IList<TLMessageBase> GetHistory(int currentUserId, TLPeerBase peer, int limit = Constants.CachedMessagesCount)
        {
            var result = new List<TLMessageBase>();

            if (_database == null) Initialize();

            if (MessagesContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();


            IList<TLMessageBase> msgs = new List<TLMessageBase>();
            try
            {
                var withId = peer.Id;
                var dialogBase = _database.Dialogs.FirstOrDefault(x => x.WithId == withId && peer.GetType() == x.Peer.GetType());

                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    msgs = dialog.Messages
                        .OfType<TLMessage>()
                        //.Where(x =>

                            //x.FromId.Value == currentUserId.Value && x.ToId.Id.Value == peer.Id.Value           // to peer from current
                        //|| x.FromId.Value == peer.Id.Value && x.ToId.Id.Value == currentUserId.Value) // from peer to current

                            .Cast<TLMessageBase>()
                            .ToList();
                }

                //var broadcast = dialogBase as TLBroadcastDialog;
                //if (broadcast != null)
                //{
                //    msgs = broadcast.Messages
                //        .OfType<TLMessageCommon>()
                //        //.Where(x =>

                //            //x.FromId.Value == currentUserId.Value && x.ToId.Id.Value == peer.Id.Value           // to peer from current
                //        //|| x.FromId.Value == peer.Id.Value && x.ToId.Id.Value == currentUserId.Value) // from peer to current

                //            .Cast<TLMessageBase>()
                //            .ToList();
                //}

            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

           // TLUtils.WritePerformance(string.Format("GetCachedHistory time ({0}): {1}", _database.CountRecords<TLMessageBase>(), timer.Elapsed));
            return msgs.Take(limit).ToList();
        }

        public void GetHistoryAsync(int currentUserId, TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount)
        {
            Execute.BeginOnThreadPool(
                () =>
                {
                    var result = new List<TLMessageBase>();

                    if (_database == null) Initialize();

                    if (MessagesContext == null)
                    {
                        callback(result);
                        return;
                    }
                    var timer = Stopwatch.StartNew();


                    IList<TLMessageBase> msgs = new List<TLMessageBase>();
                    try
                    {
                        var withId = peer.Id;
                        var dialogBase = _database.Dialogs.FirstOrDefault(x => x.WithId == withId && peer.GetType() == x.Peer.GetType());

                        var dialog = dialogBase as TLDialog;
                        if (dialog != null)
                        {
                            msgs = dialog.Messages
                                .OfType<TLMessage>()
                                //.Where(x =>

                                    //x.FromId.Value == currentUserId.Value && x.ToId.Id.Value == peer.Id.Value           // to peer from current
                                //|| x.FromId.Value == peer.Id.Value && x.ToId.Id.Value == currentUserId.Value) // from peer to current

                                    .Cast<TLMessageBase>()
                                    .ToList();
                        }

                        //var broadcast = dialogBase as TLBroadcastDialog;
                        //if (broadcast != null)
                        //{
                        //    msgs = broadcast.Messages
                        //        .OfType<TLMessageCommon>()
                        //        //.Where(x =>

                        //            //x.FromId.Value == currentUserId.Value && x.ToId.Id.Value == peer.Id.Value           // to peer from current
                        //        //|| x.FromId.Value == peer.Id.Value && x.ToId.Id.Value == currentUserId.Value) // from peer to current

                        //            .Cast<TLMessageBase>()
                        //            .ToList();
                        //}
                        
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                        TLUtils.WriteException(e);
                    }

                    //TLUtils.WritePerformance(string.Format("GetCachedHistory time ({0}): {1}", _database.CountRecords<TLMessageBase>(), timer.Elapsed));
                    callback(msgs.Take(limit).ToList());
                });
        }

        public void ClearAsync(Action callback = null)
        {
            Execute.BeginOnThreadPool(
                () =>
                {
                    lock (_databaseSyncRoot)
                    {
                        if (_database != null) _database.Clear();
                    }
                    callback.SafeInvoke();
                });
        }

        #region Messages

        private TLMessageBase GetCachedMessage(TLMessageBase message)
        {
            TLPeerChannel peerChannel;
            var isChannelMessage = TLUtils.IsChannelMessage(message, out peerChannel);
            if (isChannelMessage)
            {
                if (message.Id != 0 && ChannelsContext != null && ChannelsContext.ContainsKey(peerChannel.Id))
                {
                    var channelContext = ChannelsContext[peerChannel.Id];
                    if (channelContext != null)
                    {
                        return channelContext[message.Id];
                    }

                    return null;
                }
            }

            if (message.Id != 0 && MessagesContext != null && MessagesContext.ContainsKey(message.Id))
            {
                return MessagesContext[message.Id];
            }

            if (message.RandomId != null && RandomMessagesContext != null && RandomMessagesContext.ContainsKey(message.RandomId.Value))
            {
                return RandomMessagesContext[message.RandomId.Value];
            }

            return null;
        }

        // TODO: Secrets: 
        //private TLDecryptedMessageBase GetCachedDecryptedMessage(long? randomId)
        //{
        //    if (randomId != null && DecryptedMessagesContext != null && DecryptedMessagesContext.ContainsKey(randomId.Value))
        //    {
        //        return DecryptedMessagesContext[randomId.Value];
        //    }

        //    return null;
        //}

        // TODO: Secrets: 
        //private TLDecryptedMessageBase GetCachedDecryptedMessage(TLDecryptedMessageBase message)
        //{
        //    if (message.RandomId != null && DecryptedMessagesContext != null && DecryptedMessagesContext.ContainsKey(message.RandomIndex))
        //    {
        //        return DecryptedMessagesContext[message.RandomIndex];
        //    }

        //    //if (message.RandomIndex != 0 && RandomMessagesContext != null && RandomMessagesContext.ContainsKey(message.RandomIndex))
        //    //{
        //    //    return RandomMessagesContext[message.RandomIndex];
        //    //}


        //    return null;
        //}

        public void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, TLPeerBase peer, Action<IList<TLMessage>> callback)
        {
            if (messages == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = new List<TLMessage>();
            if (_database == null) Initialize();

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var cachedMessage = GetCachedMessage(message) as TLMessage;

                if (cachedMessage != null)
                {
                    _database.UpdateSendingMessage(message, cachedMessage, peer);
                    result.Add(cachedMessage);
                }
                else
                {
                    var previousMsg = i == 0 ? previousMessage : messages[i - 1];
                    var isLastMsg = i == messages.Count - 1;
                    _database.AddSendingMessage(message, previousMsg, isLastMsg, isLastMsg);
                    result.Add(message);
                }
            }

            _database.Commit();

            TLUtils.WritePerformance("SyncSendingMessages time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncSendingMessageId(long randomId, int id, Action<TLMessage> callback)
        {
            var timer = Stopwatch.StartNew();

            TLMessage result = null;
            if (_database == null) Initialize();

            var cachedMessage = GetMessage(randomId) as TLMessage;
            if (cachedMessage != null)
            {
                cachedMessage.Id = id;
                _database.UpdateSendingMessageContext(cachedMessage);
                result = cachedMessage;

                // send at background task and GetDialogs was invoked before getDifference
                // remove duplicates
                var dialog = GetDialog(cachedMessage);
                if (dialog != null)
                {
                    lock (dialog.MessagesSyncRoot)
                    {
                        var count = 0;
                        for (int i = 0; i < dialog.Messages.Count; i++)
                        {
                            if (dialog.Messages[i].Id == id)
                            {
                                count++;

                                if (count > 1)
                                {
                                    dialog.Messages.RemoveAt(i--);
                                }
                            }
                        }
                    }
                }
            }

            _database.Commit();

            TLUtils.WritePerformance("SyncSendingMessageId time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncSendingMessage(TLMessage message, TLMessageBase previousMessage, TLPeerBase peer, Action<TLMessage> callback)
        {
            if (message == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = message;
            if (_database == null) Initialize();

            var cachedMessage = GetCachedMessage(message);

            if (cachedMessage != null)
            {
                _database.UpdateSendingMessage(message, cachedMessage, peer);
                result = (TLMessage)cachedMessage;
            }
            else
            {
                _database.AddSendingMessage(message, previousMessage);

                // forwarding
                // TODO: Additional signature
                //var messagesContainer = message.Reply as TLMessagesContainter;               
                //if (messagesContainer != null)
                //{
                //    var messages = messagesContainer.FwdMessages;
                //    if (messages != null)
                //    {
                //        for (var i = 0; i < messages.Count; i++)
                //        {
                //            var fwdMessage = messages[i];
                //            var previousMsg = i == 0 ? message : messages[i - 1];
                //            var isLastMsg = i == messages.Count - 1;
                //            _database.AddSendingMessage(fwdMessage, previousMsg, peer, isLastMsg, isLastMsg);
                //        }
                //    }
                //}     
            }

            _database.Commit();

            TLUtils.WritePerformance("SyncSendingMessage time: " + timer.Elapsed);
            callback(result);
        }

        // TODO: Secrets: 
        //public void SyncSendingDecryptedMessage(int? chatId, int? date, long? randomId, Action<TLDecryptedMessageBase> callback)
        //{
        //    TLDecryptedMessageBase result = null;
        //    if (_database == null) Init();

        //    if (DecryptedMessagesContext != null)
        //    {
        //        result = GetCachedDecryptedMessage(randomId);
        //    }

        //    if (result == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    _database.UpdateSendingDecryptedMessage(chatId, date, result);

        //    _database.Commit();

        //    callback(result);
        //}

        // TODO: Secrets: 
        //public void SyncDecryptedMessage(TLDecryptedMessageBase message, TLEncryptedChatBase peer, Action<TLDecryptedMessageBase> callback)
        //{
        //    if (message == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    var timer = Stopwatch.StartNew();

        //    var result = message;
        //    if (_database == null) Init();

        //    TLDecryptedMessageBase cachedMessage = null;

        //    if (DecryptedMessagesContext != null)
        //    {
        //        cachedMessage = GetCachedDecryptedMessage(message);
        //    }

        //    if (cachedMessage != null)
        //    {
        //        // update fields
        //        if (message.GetType() == cachedMessage.GetType())
        //        {
        //            cachedMessage.Update(message);
        //        }

        //        result = cachedMessage;
        //    }
        //    else
        //    {
        //        // add object to cache
        //        _database.AddDecryptedMessage(message, peer);
        //    }

        //    _database.Commit();

        //    TLUtils.WritePerformance("Sync DecryptedMessage time: " + timer.Elapsed);
        //    callback(result);
        //}

        public ExceptionInfo LastSyncMessageException { get; set; }

        public void SyncMessage(TLMessageBase message, TLPeerBase peer, Action<TLMessageBase> callback)
        {
            try
            {
                if (message == null)
                {
                    callback(null);
                    return;
                }

                var result = message;
                if (_database == null) Initialize();

                var cachedMessage = GetCachedMessage(message);

                if (cachedMessage != null)
                {
                    if (cachedMessage.RandomId != null)
                    {
                        _database.RemoveMessageFromContext(cachedMessage);

                        if (cachedMessage.Id != 0)
                        {
                            cachedMessage.RandomId = null;
                        }

                        _database.AddMessageToContext(cachedMessage);
                    }

                    if (message.GetType() == cachedMessage.GetType())
                    {
                        cachedMessage.Update(message);
                    }
                    else
                    {
                        _database.DeleteMessage(cachedMessage, peer);
                        _database.AddMessage(message);
                    }
                    result = cachedMessage;
                }
                else
                {
                    try
                    {
                        _database.AddMessage(message);
                    }
                    catch (Exception ex)
                    {
                        LastSyncMessageException = new ExceptionInfo { Exception = ex, Timestamp = DateTime.Now };
                        Execute.ShowDebugMessage("SyncMessage ex:\n" + ex);
                    }
                }

                _database.Commit();
                callback(result);
            }
            catch (Exception ex)
            {
                LastSyncMessageException = new ExceptionInfo
                {
                    Caption = "CacheService.SyncMessage",
                    Exception = ex,
                    Timestamp = DateTime.Now
                };

                TLUtils.WriteException("CacheService.SyncMessage", ex);
            }
        }

        public void SyncMessages(TLMessagesMessagesBase messages, TLPeerBase peer, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesMessagesBase> callback)
        {
            if (messages == null)
            {
                callback(new TLMessagesMessages());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = messages.GetEmptyObject();
            if (_database == null) Initialize();

            SyncChatsInternal(messages.Chats, result.Chats);
            SyncUsersInternal(messages.Users, result.Users);
            SyncMessagesInternal(peer, messages.Messages, result.Messages, notifyNewDialog, notifyTopMessageUpdated);

            _database.Commit();

            //TLUtils.WritePerformance("SyncMessages time: " + timer.Elapsed);
            callback(result);
        }


        public void SyncStatuses(TLVector<TLContactStatus> contactStatuses, Action<TLVector<TLContactStatus>> callback)
        {
            if (contactStatuses == null)
            {
                callback(new TLVector<TLContactStatus>());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = contactStatuses;
            if (_database == null) Initialize();

            foreach (var contactStatus in contactStatuses)
            {
                var contactStatus19 = contactStatus as TLContactStatus;
                if (contactStatus19 != null)
                {
                    var userId = contactStatus.UserId;
                    var user = GetUser(userId) as TLUser;
                    if (user != null)
                    {
                        user.Status = contactStatus19.Status;
                    }
                }
            }

            _database.Commit();

            //TLUtils.WritePerformance("SyncMessages time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncDifference(TLUpdatesDifference difference, Action<TLUpdatesDifference> callback, IList<ExceptionInfo> exceptions)
        {
            if (difference == null)
            {
                callback(new TLUpdatesDifference());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = (TLUpdatesDifference) difference.GetEmptyObject();
            if (_database == null) Initialize();

            SyncChatsInternal(difference.Chats, result.Chats, exceptions);
            SyncUsersInternal(difference.Users, result.Users, exceptions);
            SyncMessagesInternal(null, difference.NewMessages, result.NewMessages, false, false, exceptions);
            // TODO: Secrets: SyncEncryptedMessagesInternal(difference.State.Qts, difference.NewEncryptedMessages, result.NewEncryptedMessages, exceptions);

            _database.Commit();

            //TLUtils.WritePerformance("Sync difference time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncDifferenceWithoutUsersAndChats(TLUpdatesDifference difference, Action<TLUpdatesDifference> callback, IList<ExceptionInfo> exceptions)
        {
            if (difference == null)
            {
                callback(new TLUpdatesDifference());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = (TLUpdatesDifference)difference.GetEmptyObject();
            if (_database == null) Initialize();

            //SyncChatsInternal(difference.Chats, result.Chats, exceptions);
            //SyncUsersInternal(difference.Users, result.Users, exceptions);
            SyncMessagesInternal(null, difference.NewMessages, result.NewMessages, false, false, exceptions);
            // TODO: Secrets: SyncEncryptedMessagesInternal(difference.State.Qts, difference.NewEncryptedMessages, result.NewEncryptedMessages, exceptions);

            _database.Commit();

            //TLUtils.WritePerformance("Sync difference time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncEditedMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback)
        {
            try
            {
                if (message == null)
                {
                    callback.Invoke(null);
                }
                else
                {
                    var tLMessageBase = message;
                    if (_database == null)
                    {
                        Initialize();
                    }

                    var cachedMessage = GetCachedMessage(message);
                    if (cachedMessage != null)
                    {
                        if (cachedMessage.RandomId != null)
                        {
                            _database.RemoveMessageFromContext(cachedMessage);

                            if (cachedMessage.Id != 0)
                            {
                                cachedMessage.RandomId = null;
                            }

                            _database.AddMessageToContext(cachedMessage);
                        }
                        if (message.GetType() == cachedMessage.GetType())
                        {
                            cachedMessage.Edit(message);
                        }
                        else
                        {
                            _database.RemoveMessageFromContext(cachedMessage);
                            _database.AddMessage(message, notifyNewDialog, notifyTopMessageUpdated);
                        }
                        tLMessageBase = cachedMessage;
                    }
                    this._database.Commit();
                    callback.Invoke(tLMessageBase);
                }
            }
            catch (Exception ex)
            {
                LastSyncMessageException = new ExceptionInfo
                {
                    Caption = "CacheService.SyncMessage",
                    Exception = ex,
                    Timestamp = DateTime.Now
                };
                TLUtils.WriteException("CacheService.SyncMessage", ex);
            }
        }

        private void SyncMessageInternal(TLPeerBase peer, TLMessageBase message, out TLMessageBase result)
        {
            var cachedMessage =  (TLMessage)GetCachedMessage(message);
            if (cachedMessage != null)
            {
                if (cachedMessage.RandomId != null)
                {
                    _database.RemoveMessageFromContext(cachedMessage);

                    cachedMessage.RandomId = null;

                    _database.AddMessageToContext(cachedMessage);

                }

                // update fields
                if (message.GetType() == cachedMessage.GetType())
                {
                    cachedMessage.Update(message);
                    //_database.Storage.Modify(cachedMessage);
                }
                // or replace object
                else
                {
                    _database.DeleteMessage(cachedMessage, peer);
                    _database.AddMessage(message);
                }
                result = cachedMessage;
            }
            else
            {
                // add object to cache
                result = message;
                _database.AddMessage(message);
            }
        }

        private void SyncMessagesInternal(TLPeerBase peer, IEnumerable<TLMessageBase> messages, TLVector<TLMessageBase> result, bool notifyNewDialogs, bool notifyTopMessageUpdated, IList<ExceptionInfo> exceptions = null)
        {
            foreach (var message in messages)
            {
                try
                {
                    // for updates we have input message only and set peer to null by default
                    if (peer == null)
                    {
                        peer = TLUtils.GetPeerFromMessage(message);
                    }

                    var cachedMessage = (TLMessage)GetCachedMessage(message);
                    if (cachedMessage != null)
                    {
                        if (message.GetType() == cachedMessage.GetType())
                        {
                            cachedMessage.Update(message);
                        }
                        else
                        {
                            _database.DeleteMessage(cachedMessage, peer);
                            _database.AddMessage(message);
                        }
                        result.Add(cachedMessage);
                    }
                    else
                    {
                        result.Add(message); 
                        _database.AddMessage(message, notifyNewDialogs, notifyTopMessageUpdated);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions != null)
                    {
                        exceptions.Add(new ExceptionInfo
                        {
                            Caption = "UpdatesService.ProcessDifference Messages",
                            Exception = ex,
                            Timestamp = DateTime.Now
                        });
                    }

                    TLUtils.WriteException("UpdatesService.ProcessDifference Messages", ex);
                }
            }
        }

        #endregion

        #region Dialogs

        private void SyncDialogsInternal(TLMessagesDialogsBase dialogs, TLMessagesDialogsBase result)
        {
            // set TopMessage properties
            var timer = Stopwatch.StartNew();
            MergeMessagesAndDialogs(dialogs);
            //TLUtils.WritePerformance("Dialogs:: merge dialogs and messages " + timer.Elapsed);

            timer = Stopwatch.StartNew();
            foreach (TLDialog dialog in dialogs.Dialogs)
            {
                TLDialog cachedDialog = null;
                if (DialogsContext != null)
                {
                    cachedDialog = DialogsContext[dialog.Peer.Id] as TLDialog;
                }

                if (cachedDialog != null)
                {
                    var raiseTopMessageUpdated = cachedDialog.TopMessage != dialog.TopMessage;
                    cachedDialog.Update(dialog);
                    if (raiseTopMessageUpdated)
                    {
                        if (_eventAggregator != null)
                        {
                            _eventAggregator.Publish(new TopMessageUpdatedEventArgs(cachedDialog, cachedDialog.TopMessageItem));
                        }
                    }
                    result.Dialogs.Add(cachedDialog);
                }
                else
                {
                    // add object to cache
                    result.Dialogs.Add(dialog);
                    _database.AddDialog(dialog);
                }
            }
            //TLUtils.WritePerformance("Dialogs:: foreach dialogs " + timer.Elapsed);


            
            result.Messages = dialogs.Messages;
        }

        private void SyncChannelDialogsInternal(TLMessagesDialogsBase dialogs, TLMessagesDialogsBase result)
        {
            // set TopMessage properties
            var timer = Stopwatch.StartNew();
            MergeMessagesAndChannels(dialogs);
            //TLUtils.WritePerformance("Dialogs:: merge dialogs and messages " + timer.Elapsed);

            timer = Stopwatch.StartNew();
            foreach (TLDialog dialog in dialogs.Dialogs)
            {
                TLDialog cachedDialog = null;
                if (DialogsContext != null)
                {
                    cachedDialog = DialogsContext[dialog.Peer.Id] as TLDialog;
                }

                if (cachedDialog != null)
                {
                    var raiseTopMessageUpdated = cachedDialog.TopMessage != dialog.TopMessage;
                    cachedDialog.Update(dialog);
                    if (raiseTopMessageUpdated)
                    {
                        if (_eventAggregator != null)
                        {
                            _eventAggregator.Publish(new TopMessageUpdatedEventArgs(cachedDialog, cachedDialog.TopMessageItem));
                        }
                    }
                    result.Dialogs.Add(cachedDialog);
                }
                else
                {
                    // add object to cache
                    result.Dialogs.Add(dialog);
                    _database.AddDialog(dialog);
                }
            }
            //TLUtils.WritePerformance("Dialogs:: foreach dialogs " + timer.Elapsed);



            result.Messages = dialogs.Messages;
        }

        public void SyncDialogs(TLMessagesDialogsBase dialogs, Action<TLMessagesDialogsBase> callback)
        {
            if (dialogs == null)
            {
                callback(new TLMessagesDialogs());
                return;
            }

            
            var result = dialogs.GetEmptyObject();
            if (_database == null) Initialize();

            MergeNotifySettings(dialogs);

            // add or update chats, users and messages
            var timer = Stopwatch.StartNew();
            SyncChatsInternal(dialogs.Chats, result.Chats);
            //TLUtils.WritePerformance("Dialogs:: sync chats " + timer.Elapsed);

            timer = Stopwatch.StartNew();
            SyncUsersInternal(dialogs.Users, result.Users);
            //TLUtils.WritePerformance("Dialogs:: sync users " + timer.Elapsed);

            //SyncMessagesInternal(dialogs.Messages, result.Messages);
            timer = Stopwatch.StartNew();
            SyncDialogsInternal(dialogs, result);
            //TLUtils.WritePerformance("Dialogs:: sync dialogs " + timer.Elapsed);

            _database.Commit();

            TLUtils.WritePerformance("SyncDialogs time: " + timer.Elapsed);
            callback.SafeInvoke(result);
        }

        public void SyncChannelDialogs(TLMessagesDialogsBase dialogs, Action<TLMessagesDialogsBase> callback)
        {
            if (dialogs == null)
            {
                callback(new TLMessagesDialogs());
                return;
            }

            var result = dialogs.GetEmptyObject();
            if (_database == null) Initialize();

            MergeNotifySettings(dialogs);

            // add or update chats, users and messages
            var timer = Stopwatch.StartNew();
            SyncChatsInternal(dialogs.Chats, result.Chats);
            //TLUtils.WritePerformance("Dialogs:: sync chats " + timer.Elapsed);

            timer = Stopwatch.StartNew();
            SyncUsersInternal(dialogs.Users, result.Users);
            //TLUtils.WritePerformance("Dialogs:: sync users " + timer.Elapsed);

            //SyncMessagesInternal(dialogs.Messages, result.Messages);
            timer = Stopwatch.StartNew();
            SyncChannelDialogsInternal(dialogs, result);
            //TLUtils.WritePerformance("Dialogs:: sync dialogs " + timer.Elapsed);

            _database.Commit();

            TLUtils.WritePerformance("SyncDialogs time: " + timer.Elapsed);
            callback.SafeInvoke(result);
        }

        private void MergeNotifySettings(TLMessagesDialogsBase dialogs)
        {
            var chatsIndex = new Dictionary<int, TLChatBase>();
            foreach (var chat in dialogs.Chats)
            {
                chatsIndex[chat.Id] = chat;
            }

            var usersIndex = new Dictionary<int, TLUserBase>();
            foreach (var user in dialogs.Users)
            {
                usersIndex[user.Id] = user;
            }

            foreach (var dialog in dialogs.Dialogs)
            {
                if (dialog.NotifySettings != null)
                {
                    if (dialog.Peer is TLPeerChat)
                    {
                        TLChatBase chat;
                        if (chatsIndex.TryGetValue(dialog.Peer.Id, out chat))
                        {
                            chat.NotifySettings = dialog.NotifySettings;
                        }
                    }
                    else if (dialog.Peer is TLPeerUser)
                    {
                        TLUserBase user;
                        if (usersIndex.TryGetValue(dialog.Peer.Id, out user))
                        {
                            user.NotifySettings = dialog.NotifySettings;
                        }
                    }
                }
            }
        }

        public void MergeMessagesAndChannels(TLMessagesDialogsBase dialogs)
        {
            var dialogsCache = new Context<TLDialog>();
            var messagesCache = new Context<Context<TLMessageBase>>();
            try
            {

                foreach (var dialogBase in dialogs.Dialogs)
                {
                    var dialogChannel = dialogBase as TLDialog;
                    if (dialogChannel != null)
                    {
                        var channelId = dialogChannel.Peer.Id;
                        dialogsCache[channelId] = dialogChannel;
                    }
                }

                foreach (var messageBase in dialogs.Messages)
                {
                    var message = messageBase as TLMessage;
                    if (message != null)
                    {
                        var channelId = message.ToId.Id;
                        if (!message.IsOut)
                        {
                            TLDialog dialog;
                            if (dialogsCache.TryGetValue(channelId, out dialog))
                            {
                                if (dialog.ReadInboxMaxId < message.Id)
                                {
                                    // TODO: message.IsUnread = true;
                                }
                            }
                        }

                        Context<TLMessageBase> channelContext;
                        if (!messagesCache.TryGetValue(channelId, out channelContext))
                        {
                            channelContext = new Context<TLMessageBase>();
                            messagesCache[channelId] = channelContext;
                        }
                        channelContext[message.Id] = message;
                    }
                }

            }
            catch (Exception)
            {
                
            }

            try
            {
                foreach (var channelCache in messagesCache.Values)
                {

                    foreach (var message in channelCache.Values)
                    {
                        var cachedMessage = (TLMessage)GetCachedMessage(message);
                        if (cachedMessage != null)
                        {
                            // update fields
                            if (message.GetType() == cachedMessage.GetType())
                            {
                                cachedMessage.Update(message);
                                //_database.Storage.Modify(cachedMessage);
                            }
                            // or replace object
                            else
                            {
                                _database.AddMessageToContext(message);
                            }
                        }
                        else
                        {
                            // add object to cache
                            _database.AddMessageToContext(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }

            try
            {
                foreach (TLDialog dialog in dialogs.Dialogs)
                {
                    var peer = dialog.Peer;
                    if (peer is TLPeerUser)
                    {
                        dialog.With = UsersContext[peer.Id];
                    }
                    else if (peer is TLPeerChat)
                    {
                        dialog.With = ChatsContext[peer.Id];
                    }
                    else if (peer is TLPeerChannel)
                    {
                        dialog.With = ChatsContext[peer.Id];
                    }

                    dialog.TopMessageItem = messagesCache[peer.Id][dialog.TopMessage];
                    dialog.Messages = new ObservableCollection<TLMessageBase> { dialog.TopMessageItem };
                }
            }
            catch (Exception ex)
            {
                
            }


        }

        public void MergeMessagesAndDialogs(TLMessagesDialogsBase dialogs)
        {
            var messagesCache = dialogs.Messages.Where(x => x.Id != 0).ToDictionary(x => x.Id);

            foreach (var message in messagesCache.Values)
            {
                // TODO: (TLMessage)
                var cachedMessage = GetCachedMessage(message);
                if (cachedMessage != null)
                {
                    // update fields
                    if (message.GetType() == cachedMessage.GetType())
                    {
                        cachedMessage.Update(message);
                        //_database.Storage.Modify(cachedMessage);
                    }
                    // or replace object
                    else
                    {
                        _database.AddMessageToContext(message);
                    }
                }
                else
                {
                    // add object to cache
                    _database.AddMessageToContext(message);
                }
            }

            // TODO...
            //foreach (TLDialog dialog in dialogs.Dialogs)
            foreach (TLDialog dialog in dialogs.Dialogs)
            {
                var peer = dialog.Peer;
                if (peer is TLPeerUser)
                {
                    dialog.With = UsersContext[peer.Id];
                }
                else if (peer is TLPeerChat)
                {
                    dialog.With = ChatsContext[peer.Id];
                }

                dialog.TopMessageItem = messagesCache[dialog.TopMessage];
                dialog.Messages = new ObservableCollection<TLMessageBase> { dialog.TopMessageItem };
            }
        }

        #endregion

        #region Users

        public void SyncUserLink(TLContactsLink link, Action<TLContactsLink> callback)
        {
            if (link == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            TLUserBase result;
            if (_database == null) Initialize();

            SyncUserInternal(link.User, out result);
            link.User = result;

            _database.Commit();

            TLUtils.WritePerformance("SyncUser time: " + timer.Elapsed);
            callback(link);
        }

        public void SyncUser(TLUserFull userFull, Action<TLUserFull> callback)
        {
            if (userFull == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            TLUserBase result;
            if (_database == null) Initialize();

            SyncUserInternal(userFull.ToUser(), out result);
            userFull.User = result;

            var dialog = GetDialog(new TLPeerUser { Id = userFull.User.Id });
            if (dialog != null)
            {
                dialog.NotifySettings = userFull.NotifySettings;
            }

            _database.Commit();

            //TLUtils.WritePerformance("SyncUserFull time: " + timer.Elapsed);
            
            callback.SafeInvoke(userFull);
        }


        public void SyncUser(TLUserBase user, Action<TLUserBase> callback)
        {
            if (user == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            TLUserBase result;
            if (_database == null) Initialize();

            SyncUserInternal(user, out result);

            _database.Commit();

            TLUtils.WritePerformance("SyncUser time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback)
        {
            if (users == null)
            {
                callback(new TLVector<TLUserBase>());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = new TLVector<TLUserBase>();
            if (_database == null) Initialize();

            SyncUsersInternal(users, result);

            _database.Commit();

            TLUtils.WritePerformance("SyncUsers time: " + timer.Elapsed);
            callback(result);
        }

        public void SyncUsersAndChats(TLVector<TLUserBase> users, TLVector<TLChatBase> chats, Action<Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>> callback)
        {
            if (users == null && chats == null)
            {
                callback(new Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>(null, null));
                return;
            }

            var timer = Stopwatch.StartNew();

            var usersResult = new TLVector<TLUserBase>();
            var chatsResult = new TLVector<TLChatBase>();
            if (_database == null) Initialize();

            SyncUsersInternal(users, usersResult);
            SyncChatsInternal(chats, chatsResult);

            _database.Commit();

            TLUtils.WritePerformance("SyncUsersAndChats time: " + timer.Elapsed);
            callback(new Tuple<TLVector<TLUserBase>, TLVector<TLChatBase>>(usersResult, chatsResult));
        }

        private void SyncUserInternal(TLUserBase user, out TLUserBase result)
        {
            TLUserBase cachedUser = null;
            if (UsersContext != null)
            {
                cachedUser = UsersContext[user.Id];
            }

            if (cachedUser != null)
            {
                // update fields
                if (user.GetType() == cachedUser.GetType())
                {
                    cachedUser.Update(user);
                    result = cachedUser;
                }
                // or replace object
                else
                {
                    _database.ReplaceUser(user.Id, user);
                    result = user;
                }
            }
            else
            {
                // add object to cache
                result = user;
                _database.AddUser(user);
            }
        }

        private void SyncUsersInternal(TLVector<TLUserBase> users, TLVector<TLUserBase> result, IList<ExceptionInfo> exceptions = null)
        {
            foreach (var user in users)
            {
                try
                {
                    TLUserBase cachedUser = null;
                    if (UsersContext != null)
                    {
                        cachedUser = UsersContext[user.Id];
                    }

                    if (cachedUser != null)
                    {
                        // update fields
                        if (user.GetType() == cachedUser.GetType())
                        {
                            cachedUser.Update(user);
                            result.Add(cachedUser);
                        }
                        // or replace object
                        else
                        {
                            _database.ReplaceUser(user.Id, user);
                            result.Add(user);
                        }
                    }
                    else
                    {
                        // add object to cache
                        result.Add(user);
                        _database.AddUser(user);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions != null)
                    {
                        exceptions.Add(new ExceptionInfo
                        {
                            Caption = "UpdatesService.ProcessDifference Users",
                            Exception = ex,
                            Timestamp = DateTime.Now
                        });
                    }

                    TLUtils.WriteException("UpdatesService.ProcessDifference Users", ex);
                }
            }
        }

        #endregion

        #region SecretChats

        // TODO: Secrets: 
        //private void SyncEncryptedChatInternal(TLEncryptedChatBase chat, out TLEncryptedChatBase result)
        //{
        //    try
        //    {
        //        TLEncryptedChatBase cachedChat = null;
        //        if (EncryptedChatsContext != null)
        //        {
        //            cachedChat = EncryptedChatsContext[chat.Index];
        //        }

        //        if (cachedChat != null)
        //        {
        //            // update fields
        //            if (chat.GetType() == cachedChat.GetType())
        //            {
        //                cachedChat.Update(chat);
        //                result = cachedChat;
        //            }
        //            // or replace object
        //            else
        //            {
        //                var chatWaiting = cachedChat as TLEncryptedChatWaiting;
        //                if (chatWaiting != null)
        //                {
        //                    var encryptedChat = chat as TLEncryptedChat;
        //                    if (encryptedChat != null)
        //                    {
        //                        chat.A = cachedChat.A;
        //                        chat.P = cachedChat.P;
        //                        chat.G = cachedChat.G;

        //                        if (!TLUtils.CheckGaAndGb(encryptedChat.GAorB.Data, chat.P.Data))
        //                        {
        //                            result = chat;
        //                            return;
        //                        }

        //                        var gbBytes = encryptedChat.GAorB.ToBytes();
        //                        var authKey = MTProtoService.GetAuthKey(chat.A.Data, gbBytes, chat.P.ToBytes());
        //                        chat.Key = string.FromBigEndianData(authKey);

        //                        var authKeyFingerprint = Utils.ComputeSHA1(authKey);
        //                        chat.KeyFingerprint = new long?(BitConverter.ToInt64(authKeyFingerprint, 12));
        //                    }
        //                    else
        //                    {
        //                        if (cachedChat.Key != null) chat.Key = cachedChat.Key;
        //                        if (cachedChat.KeyFingerprint != null) chat.KeyFingerprint = cachedChat.KeyFingerprint;
        //                    }
        //                }
        //                //chat.A = cachedChat.A;
        //                //chat.P = cachedChat.P;
        //                //chat.G = cachedChat.G;

        //                //var encryptedChat = chat as TLEncryptedChat;
        //                //if (encryptedChat != null)
        //                //{
        //                //    var gbBytes = encryptedChat.GAorB.ToBytes();
        //                //    var authKey = MTProtoService.GetAuthKey(chat.A.Data, gbBytes, chat.P.ToBytes());
        //                //    chat.Key = string.FromBigEndianData(authKey);

        //                //    var authKeyFingerprint = Utils.ComputeSHA1(authKey);
        //                //    chat.KeyFingerprint = new long?(BitConverter.ToInt64(authKeyFingerprint, 12));
        //                //}
        //                //else
        //                //{
        //                //    if (cachedChat.Key != null) chat.Key = cachedChat.Key;
        //                //    if (cachedChat.KeyFingerprint != null) chat.KeyFingerprint = cachedChat.KeyFingerprint;
        //                //}

        //                //Helpers.Execute.ShowDebugMessage(string.Format("InMemoryCacheService.SyncEncryptedChatInternal {0}!={1}", cachedChat.GetType(), chat.GetType()));

        //                _database.ReplaceEncryptedChat(chat.Index, chat);

        //                result = chat;
        //            }
        //        }
        //        else
        //        {
        //            // add object to cache
        //            result = chat;
        //            _database.AddEncryptedChat(chat);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        result = null;
        //    }
        //}

        // TODO: Secrets: 
        //public void SyncEncryptedChat(TLEncryptedChatBase encryptedChat, Action<TLEncryptedChatBase> callback)
        //{
        //    if (encryptedChat == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    TLEncryptedChatBase chatResult;
        //    if (_database == null) Init();

        //    SyncEncryptedChatInternal(encryptedChat, out chatResult);

        //    _database.Commit();

        //    callback.SafeInvoke(chatResult);
        //}

        // TODO: Secrets: 
        //public void SyncEncryptedMessagesInternal(int? qts, TLVector<TLEncryptedMessageBase> messages, TLVector<TLEncryptedMessageBase> result, IList<ExceptionInfo> exceptions = null)
        //{
        //    foreach (var message in messages)
        //    {
        //        try
        //        {
        //            var encryptedChat = GetEncryptedChat(message.ChatId) as TLEncryptedChat;
        //            if (encryptedChat == null)
        //            {
        //                result.Add(message);
        //                continue;
        //            }

        //            bool commitChat;
        //            var decryptedMessage = UpdatesService.GetDecryptedMessage(encryptedChat, message, qts, out commitChat);
        //            if (commitChat)
        //            {
        //                Commit();
        //            }
        //            if (decryptedMessage == null) continue;

        //            var syncMessageFlag = UpdatesService.IsSyncRequierd(decryptedMessage);

        //            // в фоне для быстрого обновления
        //            Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(decryptedMessage));

        //            if (syncMessageFlag)
        //            {
        //                SyncDecryptedMessage(decryptedMessage, encryptedChat, cachedMessage => { });
        //            }

        //            result.Add(message);
        //        }
        //        catch (Exception ex)
        //        {
        //            if (exceptions != null)
        //            {
        //                exceptions.Add(new ExceptionInfo
        //                {
        //                    Caption = "UpdatesService.ProcessDifference EncryptedMessages",
        //                    Exception = ex,
        //                    Timestamp = DateTime.Now
        //                });
        //            }

        //            TLUtils.WriteException("UpdatesService.ProcessDifference EncryptedMessages", ex);
        //        }
        //    }
        //}
        #endregion

        #region Chats

        public void AddChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback)
        {
            if (chats == null)
            {
                callback(null);
                return;
            }

            if (_database == null) Initialize();

            foreach (var chat in chats)
            {
                TLChatBase cachedChat = null;
                if (ChatsContext != null)
                {
                    cachedChat = ChatsContext[chat.Id];
                }

                if (cachedChat == null)
                {
                    _database.AddChat(chat);
                }
            }

            _database.Commit();

            callback.SafeInvoke(chats);
        }

        public void SyncChat(TLMessagesChatFull messagesChatFull, Action<TLMessagesChatFull> callback)
        {
            if (messagesChatFull == null)
            {
                callback(null);
                return;
            }

            var usersResult = new TLVector<TLUserBase>(messagesChatFull.Users.Count);
            var chatsResult = new TLVector<TLChatBase>(messagesChatFull.Chats.Count);
            var currentChat = messagesChatFull.Chats.First(x => x.Id == messagesChatFull.FullChat.Id);
            TLChatBase chatResult;
            if (_database == null) Initialize();

            SyncUsersInternal(messagesChatFull.Users, usersResult);
            messagesChatFull.Users = usersResult;

            SyncChatsInternal(messagesChatFull.Chats, chatsResult);
            messagesChatFull.Chats = chatsResult;

            SyncChatInternal(messagesChatFull.FullChat.ToChat(currentChat), out chatResult);

            var channel = currentChat as TLChannel;
            var dialog = GetDialog(channel != null ? (TLPeerBase)new TLPeerChannel { Id = messagesChatFull.FullChat.Id } : new TLPeerChat { Id = messagesChatFull.FullChat.Id });
            if (dialog != null)
            {
                dialog.NotifySettings = messagesChatFull.FullChat.NotifySettings;
            }

            _database.Commit();

            //TLUtils.WritePerformance("SyncChatFull time: " + timer.Elapsed);

            callback.SafeInvoke(messagesChatFull);
        }

        public void SyncChats(TLVector<TLChatBase> chats, Action<TLVector<TLChatBase>> callback)
        {
            if (chats == null)
            {
                callback(new TLVector<TLChatBase>());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = new TLVector<TLChatBase>();
            if (_database == null) Initialize();

            SyncChatsInternal(chats, result);

            _database.Commit();

            TLUtils.WritePerformance("SyncChats time: " + timer.Elapsed);
            callback(result);
        }

        private void SyncChatsInternal(TLVector<TLChatBase> chats, TLVector<TLChatBase> result, IList<ExceptionInfo> exceptions = null)
        {
            foreach (var chat in chats)
            {
                try
                {
                    TLChatBase cachedChat = null;
                    if (ChatsContext != null)
                    {
                        cachedChat = ChatsContext[chat.Id];
                    }

                    if (cachedChat != null)
                    {
                        // update fields
                        if (chat.GetType() == cachedChat.GetType())
                        {
                            cachedChat.Update(chat);
                        }
                        // or replace object
                        else
                        {
                            _database.ReplaceChat(chat.Id, chat);
                        }
                        result.Add(cachedChat);
                    }
                    else
                    {
                        // add object to cache
                        result.Add(chat);
                        _database.AddChat(chat);
                    }
                }
                catch (Exception ex)
                {
                    if (exceptions != null)
                    {
                        exceptions.Add(new ExceptionInfo
                        {
                            Caption = "UpdatesService.ProcessDifference Chats",
                            Exception = ex,
                            Timestamp = DateTime.Now
                        });
                    }

                    TLUtils.WriteException("UpdatesService.ProcessDifference Chats", ex);
                }
            }
        }

        private void SyncChatInternal(TLChatBase chat, out TLChatBase result)
        {
            TLChatBase cachedChat = null;
            if (ChatsContext != null)
            {
                cachedChat = ChatsContext[chat.Id];
            }

            if (cachedChat != null)
            {
                // update fields
                if (chat.GetType() == cachedChat.GetType())
                {
                    cachedChat.Update(chat);
                }
                // or replace object
                else
                {
                    _database.ReplaceChat(chat.Id, chat);
                }
                result = cachedChat;
            }
            else
            {
                // add object to cache
                result = chat;
                _database.AddChat(chat);
            }
        }
        #endregion

        #region Broadcasts

        // DEPRECATED: 
        //public void SyncBroadcast(TLBroadcastChat broadcast, Action<TLBroadcastChat> callback)
        //{
        //    if (broadcast == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    var timer = Stopwatch.StartNew();

        //    TLBroadcastChat result;
        //    if (_database == null) Init();

        //    SyncBroadcastInternal(broadcast, out result);

        //    _database.Commit();

        //    TLUtils.WritePerformance("SyncBroadcast time: " + timer.Elapsed);
        //    callback(result);
        //}

        // DEPRECATED: 
        //private void SyncBroadcastInternal(TLBroadcastChat chat, out TLBroadcastChat result)
        //{
        //    TLBroadcastChat cachedBroadcast = null;
        //    if (BroadcastsContext != null)
        //    {
        //        cachedBroadcast = BroadcastsContext[chat.Index];
        //    }

        //    if (cachedBroadcast != null)
        //    {
        //        // update fields
        //        if (chat.GetType() == cachedBroadcast.GetType())
        //        {
        //            cachedBroadcast.Update(chat);
        //        }
        //        // or replace object
        //        else
        //        {
        //            _database.ReplaceBroadcast(chat.Index, chat);
        //        }
        //        result = cachedBroadcast;
        //    }
        //    else
        //    {
        //        // add object to cache
        //        result = chat;
        //        _database.AddBroadcast(chat);
        //    }
        //}
        #endregion

        #region Contacts

        public void AddUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback)
        {
            if (users == null)
            {
                callback(null);
                return;
            }

            if (_database == null) Initialize();

            foreach (var user in users)
            {
                TLUserBase cachedUser = null;
                if (UsersContext != null)
                {
                    cachedUser = UsersContext[user.Id];
                }

                if (cachedUser == null)
                {
                    _database.AddUser(user);
                }
            }

            _database.Commit();

            callback.SafeInvoke(users);
        }

        public void SyncContacts(TLContactsImportedContacts contacts, Action<TLContactsImportedContacts> callback)
        {
            if (contacts == null)
            {
                callback(new TLContactsImportedContacts());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = contacts.GetEmptyObject();
            if (_database == null) Initialize();

            SyncContactsInternal(contacts, result);

            _database.Commit();

            TLUtils.WritePerformance("SyncImportedContacts time: " + timer.Elapsed);
            callback(result);
        }

        // TODO: No idea: 
        //public void SyncStatedMessage(TLStatedMessageBase statedMessage, Action<TLStatedMessageBase> callback)
        //{
        //    if (statedMessage == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    var timer = Stopwatch.StartNew();

        //    var result = statedMessage.GetEmptyObject();
        //    if (_database == null) Init();

        //    SyncChatsInternal(statedMessage.Chats, result.Chats);
        //    SyncUsersInternal(statedMessage.Users, result.Users);
        //    TLMessageBase message;
        //    SyncMessageInternal(TLUtils.GetPeerFromMessage(statedMessage.Message), statedMessage.Message, out message);
        //    result.Message = message;

        //    var messageCommon = message as TLMessageCommon;
        //    if (messageCommon != null)
        //    {
        //        var dialog = GetDialog(messageCommon);
        //        if (dialog != null)
        //        {
        //            var oldMessage = dialog.Messages.FirstOrDefault(x => x.Index == message.Index);
        //            if (oldMessage != null)
        //            {
        //                dialog.Messages.Remove(oldMessage);
        //                dialog.Messages.Insert(0, message);
        //                dialog._topMessage = message;
        //                dialog.TopMessageId = message.Id;
        //                dialog.TopMessageRandomId = message.RandomId;
        //                _eventAggregator.Publish(new TopMessageUpdatedEventArgs(dialog, message));
        //            }
        //        }
        //    }

        //    _database.Commit(); 


        //    TLUtils.WritePerformance("SyncStatedMessage time: " + timer.Elapsed);
        //    callback(result);
        //}

        // TODO: No idea: 
        //public void SyncStatedMessages(TLStatedMessagesBase statedMessages, Action<TLStatedMessagesBase> callback)
        //{
        //    if (statedMessages == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    var timer = Stopwatch.StartNew();

        //    var result = statedMessages.GetEmptyObject();
        //    if (_database == null) Init();

        //    SyncChatsInternal(statedMessages.Chats, result.Chats);
        //    SyncUsersInternal(statedMessages.Users, result.Users);

        //    foreach (var m in statedMessages.Messages)
        //    {
        //        TLMessageBase message;
        //        SyncMessageInternal(TLUtils.GetPeerFromMessage(m), m, out message);
        //        result.Messages.Add(message);
        //    }


        //    _database.Commit();


        //    TLUtils.WritePerformance("SyncStatedMessages time: " + timer.Elapsed);
        //    callback(result);
        //}

        public void DeleteDialog(TLDialog dialog)
        {
            if (dialog != null)
            {
                _database.DeleteDialog(dialog);

                _database.Commit();
            }
        }


        public void DeleteUser(int id)
        {
            _database.DeleteUser(id);
            _database.Commit();
        }

        public void DeleteChat(int id)
        {
            _database.DeleteChat(id);
            _database.Commit();            
        }

        public void DeleteMessages(TLVector<long> randomIds)
        {
            if (randomIds == null || randomIds.Count == 0) return;

            foreach (var id in randomIds)
            {
                var message = _database.RandomMessagesContext[id];
                if (message != null)
                {
                    var peer = TLUtils.GetPeerFromMessage(message);

                    if (peer != null)
                    {
                        _database.DeleteMessage(message, peer);
                    }
                }
            }

            _database.Commit();
        }

        // TODO: Secrets: 
        //public void DeleteDecryptedMessages(TLVector<long> randomIds)
        //{
        //    foreach (var id in randomIds)
        //    {
        //        var message = _database.DecryptedMessagesContext[id.Value];
        //        if (message != null)
        //        {
        //            var peer = TLUtils.GetPeerFromMessage(message);

        //            if (peer != null)
        //            {
        //                _database.DeleteDecryptedMessage(message, peer);
        //            }
        //        }
        //    }

        //    _database.Commit();
        //}

        // TODO: Secrets: 
        //public void ClearDecryptedHistoryAsync(int chatId)
        //{
        //    _database.ClearDecryptedHistory(chatId);

        //    _database.Commit();
        //}

        // DEPRECATED: 
        //public void ClearBroadcastHistoryAsync(int chatId)
        //{
        //    _database.ClearBroadcastHistory(chatId);

        //    _database.Commit();
        //}

        public void DeleteMessages(TLPeerBase peer, TLMessageBase lastItem, TLVector<int> messages)
        {
            if (messages == null || messages.Count == 0) return;

            _database.DeleteMessages(peer, lastItem, messages);

            _database.Commit();
        }

        public void DeleteMessages(TLVector<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            foreach (var id in ids)
            {
                var message = _database.MessagesContext[id];
                if (message != null)
                {
                    var peer = TLUtils.GetPeerFromMessage(message);

                    if (peer != null)
                    {
                        _database.DeleteMessage(message, peer);
                    }
                }
            }

            _database.Commit();
        }

        public void DeleteChannelMessages(int channelId, TLVector<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            foreach (var id in ids)
            {
                var channelContext = _database.ChannelsContext[channelId];
                if (channelContext != null)
                {
                    var message = channelContext[id];
                    if (message != null)
                    {
                        var peer = TLUtils.GetPeerFromMessage(message);

                        if (peer != null)
                        {
                            _database.DeleteMessage(message, peer);
                        }
                    }
                }
            }

            _database.Commit();
        }

        private void SyncContactsInternal(TLContactsImportedContacts contacts, TLContactsImportedContacts result)
        {
            var cache = contacts.Users.ToDictionary(x => x.Id);
            foreach (var importedContact in contacts.Imported)
            {
                if (cache.ContainsKey(importedContact.UserId))
                {
                    cache[importedContact.UserId].ClientId = importedContact.ClientId;
                }
            }


            foreach (var user in contacts.Users)
            {
                TLUserBase cachedUser = null;
                if (UsersContext != null)
                {
                    cachedUser = UsersContext[user.Id];
                }

                if (cachedUser != null)
                {
                    // update fields
                    if (user.GetType() == cachedUser.GetType())
                    {
                        cachedUser.Update(user);
                        result.Users.Add(cachedUser);
                    }
                    // or replace object
                    else
                    {
                        _database.ReplaceUser(user.Id, user);
                        result.Users.Add(user);
                    }
                }
                else
                {
                    // add object to cache
                    result.Users.Add(user);
                    _database.AddUser(user);
                }

            }

            result.Imported = contacts.Imported;
        }

        public void SyncContacts(TLContactsContactsBase contacts, Action<TLContactsContactsBase> callback)
        {
            if (contacts == null)
            {
                callback(new TLContactsContacts());
                return;
            }

            if (contacts is TLContactsContactsNotModified)
            {
                callback(contacts);
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = contacts.GetEmptyObject();
            if (_database == null) Initialize();

            SyncContactsInternal((TLContactsContacts)contacts, (TLContactsContacts)result);

            _database.Commit();

            TLUtils.WritePerformance("SyncContacts time: " + timer.Elapsed);
            callback(result);
        }

        private void SyncContactsInternal(TLContactsContacts contacts, TLContactsContacts result)
        {
            var contactsCache = new Dictionary<int, TLContact>();
            foreach (var contact in contacts.Contacts)
            {
                contactsCache[contact.UserId] = contact;
            }

            foreach (var user in contacts.Users)
            {
                user.Contact = contactsCache[user.Id];

                TLUserBase cachedUser = null;
                if (UsersContext != null)
                {
                    cachedUser = UsersContext[user.Id];
                }

                if (cachedUser != null)
                {
                    // update fields
                    if (user.GetType() == cachedUser.GetType())
                    {
                        cachedUser.Update(user);
                        result.Users.Add(cachedUser);
                    }
                    // or replace object
                    else
                    {
                        _database.ReplaceUser(user.Id, user);
                        result.Users.Add(user);
                    }
                }
                else
                {
                    // add object to cache
                    result.Users.Add(user);
                    _database.AddUser(user);
                }

            }

            result.Contacts = contacts.Contacts;
        }

        #endregion

        #region Config

        private void CheckDisabledFeature(TLConfig config, string featureKey, Action callback, Action<TLDisabledFeature> faultCallback = null)
        {
            var config23 = config as TLConfig;
            if (config23 != null)
            {
                var disabledFeatures = config23.DisabledFeatures;
                if (disabledFeatures != null)
                {
                    var disabledFeature = disabledFeatures.FirstOrDefault(x => string.Equals(x.Feature.ToString(), featureKey, StringComparison.OrdinalIgnoreCase));
                    if (disabledFeature != null)
                    {
                        faultCallback.SafeInvoke(disabledFeature);
                        return;
                    }

                    callback.SafeInvoke();
                }
            }
        }

        private void IsBigChat(int count, Action<bool> callback)
        {
            GetConfigAsync(config =>
            {
                var config23 = config as TLConfig;
                if (config23 != null)
                {
                    callback.SafeInvoke(count > config23.ChatBigSize);
                    return;
                }

                callback.SafeInvoke(false);
            });
        }

        private void SelectFeatureKey(TLObject with, string pmKey, string chatKey, string bigChatKey, Action<TLConfig, string> callback)
        {
            var featureKey = string.Empty;

            var channel = with as TLChannel;
            //var broadcast = with as TLBroadcastChat;
            var chat = with as TLChat;
            var user = with as TLUserBase;

            GetConfigAsync(
                config =>
                {
                    if (channel != null)
                    {
                        var participantsCount = channel.ParticipantIds != null ? channel.ParticipantIds.Count : 0;

                        IsBigChat(participantsCount, result =>
                        {
                            featureKey = result ? bigChatKey : chatKey;

                            callback.SafeInvoke(config, featureKey);
                        });

                        return;
                    }

                    //if (broadcast != null)
                    //{
                    //    var participantsCount = broadcast.ParticipantIds.Count;

                    //    IsBigChat(participantsCount, result =>
                    //    {
                    //        featureKey = result ? bigChatKey : chatKey;

                    //        callback.SafeInvoke(config, featureKey);
                    //    });

                    //    return;
                    //}

                    if (chat != null)
                    {
                        var participantsCount = chat.ParticipantsCount;

                        IsBigChat(participantsCount, result =>
                        {
                            featureKey = result ? bigChatKey : chatKey;

                            callback.SafeInvoke(config, featureKey);
                        });

                        return;
                    }

                    if (user != null)
                    {
                        featureKey = pmKey;
                        callback.SafeInvoke(config, featureKey);
                        return;
                    }

                    callback.SafeInvoke(config, featureKey);
                });
        }

        public void CheckDisabledFeature(string featureKey, Action callback, Action<TLDisabledFeature> faultCallback)
        {
            GetConfigAsync(config => CheckDisabledFeature(config, featureKey, callback, faultCallback));
        }

        public void CheckDisabledFeature(TLObject with, string featurePMMessage, string featureChatMessage, string featureBigChatMessage, Action callback, Action<TLDisabledFeature> faultCallback)
        {
            SelectFeatureKey(with, featurePMMessage, featureChatMessage, featureBigChatMessage, (config, featureKey) => CheckDisabledFeature(config, featureKey, callback, faultCallback));
        }

        private TLConfig _config;
        public void GetConfigAsync(Action<TLConfig> callback)
        {
#if SILVERLIGHT || WIN_RT
            if (_config == null)
            {
                _config = SettingsHelper.GetValue(Constants.ConfigKey) as TLConfig;
            }
#endif
            callback.SafeInvoke(_config);
        }

        public void SetConfig(TLConfig config)
        {
            _config = config;
#if SILVERLIGHT || WIN_RT
            SettingsHelper.SetValue(Constants.ConfigKey, config);
#endif
        }

        #endregion

        public IList<TLMessageBase> GetSendingMessages()
        {
            return RandomMessagesContext.Values.ToList();
        }

        public IList<TLMessageBase> GetResendingMessages()
        {
            return _database.ResendingMessages;
        } 

        public IList<TLMessageBase> GetMessages()
        {
            var result = new List<TLMessageBase>();
            foreach (var d in _database.Dialogs)
            {
                var dialog = d as TLDialog;
                if (dialog != null)
                {
                    foreach (var message in dialog.Messages)
                    {
                        result.Add(message);
                    }
                }
                else
                {

                    //var encryptedDialog = d as TLEncryptedDialog;
                    //if (encryptedDialog != null)
                    //{
                    //    foreach (var message in encryptedDialog.Messages)
                    //    {
                    //        result.Add(message);
                    //    }
                    //}
                }
            }

            return result;
        }

        public void Commit()
        {
            if (_database != null)
            {
                _database.Commit();
            }
        }

        public bool TryCommit()
        {
            if (_database != null && _database.HasChanges)
            {
                _database.CommitInternal();
                //Helpers.Execute.ShowDebugMessage("TryCommit result=true");
                return true;
            }

            return false;
        }

        public void SaveSnapshot(string toDirectoryName)
        {
            if (_database != null)
            {
                _database.SaveSnapshot(toDirectoryName);
            }
        }

        public void LoadSnapshot(string fromDirectoryName)
        {
            if (_database != null)
            {
                _database.LoadSnapshot(fromDirectoryName);
                _database.Open();
            }
        }
    }
}
