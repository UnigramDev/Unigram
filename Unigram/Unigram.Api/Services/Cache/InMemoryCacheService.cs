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

        private Context<TLEncryptedChatBase> EncryptedChatsContext
        {
            get { return _database != null ? _database.EncryptedChatsContext : null; }
        } 

        private Context<TLMessageBase> MessagesContext
        {
            get { return _database != null ? _database.MessagesContext : null; }
        }

        private Context<Context<TLMessageBase>> ChannelsContext
        {
            get { return _database != null ? _database.ChannelsContext : null; }
        }

        // TODO: Encrypted 
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

        public void Init()
        {
            var stopwatch = Stopwatch.StartNew();

            _database = new InMemoryDatabase(_eventAggregator);
            _database.Open();

            Debug.WriteLine("{0} {1}", stopwatch.Elapsed, "open database time");
        }

        private readonly ITelegramEventAggregator _eventAggregator;

        public static ICacheService Current { get; protected set; }

        public InMemoryCacheService(ITelegramEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            Current = this;
        }

        public IList<TLDialog> GetDialogs()
        {
            var result = new List<TLDialog>();

            if (_database == null) Init();

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
            Execute.BeginOnThreadPool(
                () =>
                {
                    var result = new List<TLDialog>();

                    if (_database == null) Init();

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

            if (_database == null) Init();

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

            if (_database == null) Init();

            if (UsersContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();

            var contacts = new List<TLUserBase>();

            try
            {
                contacts = _database.UsersContext.Values.OfType<TLUser>().Where(x => x != null && (x.IsContact || x.IsSelf)).Cast<TLUserBase>().ToList();
                //contacts = _database.UsersContext.Values.Where(x => x.Contact != null).ToList();

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

            if (_database == null) Init();

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

                var unsortedContacts = _database.UsersContext.Values.OfType<TLUser>().Where(x => x != null && x.IsContact).ToList();
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

                    if (_database == null) Init();

                    if (UsersContext == null)
                    {
                        callback(result);
                        return;
                    }
                    var timer = Stopwatch.StartNew();

                    IList<TLUserBase> contacts = new List<TLUserBase>();

                    try
                    {
                        contacts = _database.UsersContext.Values.OfType<TLUser>().Where(x => x != null && x.IsContact).Cast<TLUserBase>().ToList();
                        //contacts = _database.UsersContext.Values.Where(x => x.Contact != null).ToList();

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

            if (_database == null) Init();

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

                    if (_database == null) Init();

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

        public TLChatBase GetChat(int? id)
        {
            if (_database == null)
            {
                Init();
            }

            return ChatsContext[id.Value];
        }

        // TODO: Encrypted 
        //public TLEncryptedChatBase GetEncryptedChat(int? id)
        //{
        //    if (_database == null)
        //    {
        //        Init();
        //    }

        //    return EncryptedChatsContext[id.Value];
        //}

        public TLUserBase GetUser(int? id)
        {
            if (_database == null)
            {
                Init();
            }

            return UsersContext[id.Value];
        }

        public TLUserBase GetUser(TLUserProfilePhoto photo)
        {
            var usersShapshort = new List<TLUserBase>(UsersContext.Values);

            return usersShapshort.OfType<TLUser>().FirstOrDefault(x => x.Photo == photo);
        }

        public TLUserBase GetUser(string username)
        {
            var usersShapshort = new List<TLUserBase>(UsersContext.Values);

            // TODO: before TLUser was ITLUserName, but I think we don't need it anymore
            return usersShapshort.FirstOrDefault(x => x is TLUser && ((TLUser)x).Username != null && string.Equals(((TLUser)x).Username, username, StringComparison.OrdinalIgnoreCase));
        }

        public TLMessageBase GetMessage(int? id, int? channelId = null)
        {
            if (channelId != null)
            {
                var channelContext = ChannelsContext[channelId.Value];
                if (channelContext != null)
                {
                    return channelContext[id.Value];
                }

                return null;
            }

            return MessagesContext[id.Value];
        }

        public TLMessageBase GetMessage(long? randomId)
        {
            return RandomMessagesContext[randomId.Value];
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
                        var currentWebPage = webPageMedia.WebPage;
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
                            var currentWebPage = webPageMedia.WebPage;
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
                        var currentWebPage = webPageMedia.WebPage;
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

        public TLDialog GetDialog(TLMessageCommonBase message)
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
            return _database.GetDialog(peer) as TLDialog;

            //return _database.Dialogs.OfType<TLDialog>().FirstOrDefault(x => x.WithId == peer.Id.Value && x.IsChat == peer is TLPeerChat);
        }

        // TODO: Encrypted 
        //public TLDialog GetEncryptedDialog(int? chatId)
        //{
        //    return _database.Dialogs.OfType<TLEncryptedDialog>().FirstOrDefault(x => x.Index == chatId.Value);
        //}

        public TLChat GetChat(TLChatPhoto chatPhoto)
        {
            return _database.ChatsContext.Values.FirstOrDefault(x => x is TLChat && ((TLChat)x).Photo == chatPhoto) as TLChat;
        }

        public TLChannel GetChannel(string username)
        {
            var chatsSnapshort = new List<TLChatBase>(_database.ChatsContext.Values);

            return chatsSnapshort.FirstOrDefault(x => x is TLChannel && ((TLChannel)x).Username != null && string.Equals(((TLChannel)x).Username, username, StringComparison.OrdinalIgnoreCase)) as TLChannel;
        }

        public TLChannel GetChannel(TLChatPhoto chatPhoto)
        {
            var chatsSnapshort = new List<TLChatBase>(_database.ChatsContext.Values);

            return chatsSnapshort.FirstOrDefault(x => x is TLChannel && ((TLChannel)x).Photo == chatPhoto) as TLChannel;
        }

        public IList<TLMessageBase> GetHistory(int dialogIndex)
        {
            var result = new List<TLMessageBase>();

            if (_database == null) Init();

            // TODO: Encrypted 
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
                        .OfType<TLMessageCommonBase>()
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

        #region TODO: Encrypted
        //public TLDecryptedMessageBase GetDecryptedMessage(int? chatId, long? randomId)
        //{
        //    TLDecryptedMessageBase result = null;

        //    if (_database == null) Init();

        //    if (MessagesContext == null || DialogsContext == null)
        //    {
        //        return result;
        //    }

        //    IList<TLDecryptedMessageBase> msgs = new List<TLDecryptedMessageBase>();
        //    try
        //    {
        //        var dialog = DialogsContext[chatId.Value] as TLEncryptedDialog;

        //        if (dialog != null)
        //        {
        //            msgs = dialog.Messages.ToList();
        //            foreach (var message in msgs)
        //            {
        //                if (message.RandomIndex == randomId.Value)
        //                {
        //                    return message;
        //                }
        //            }
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
        //        TLUtils.WriteException(e);
        //    }

        //    return result;
        //}

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

        //public IList<TLDecryptedMessageBase> GetUnreadDecryptedHistory(int dialogIndex)
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
        //    for (var i = 0; i < msgs.Count; i++)
        //    {
        //        if (!msgs[i].Out.Value && msgs[i].Unread.Value)
        //        {
        //            returnedMessages.Add(msgs[i]);
        //        }
        //    }

        //    return returnedMessages;
        //}

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
        #endregion

        public IList<TLMessageBase> GetHistory(TLPeerBase peer, int maxId, int limit = Constants.CachedMessagesCount)
        {
            var result = new List<TLMessageBase>();

            if (_database == null) Init();

            if (MessagesContext == null)
            {
                return result;
            }

            IList<TLMessageBase> msgs = new List<TLMessageBase>();
            try
            {
                var withId = peer.Id;
                var dialogBase = _database.Dialogs.FirstOrDefault(x => x.WithId == withId && peer.TypeId == x.Peer.TypeId);

                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    msgs = dialog.Messages
                        .OfType<TLMessageCommonBase>()
                        .Cast<TLMessageBase>()
                        .ToList();
                }
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR:", LogSeverity.Error);
                TLUtils.WriteException(e);
            }

            var count = 0;
            var startPosition = -1;
            var resultMsgs = new List<TLMessageBase>();
            for (var i = 0; i < msgs.Count && count < limit; i++)
            {
                var msg = msgs[i];
                if (startPosition == -1)
                {
                    if (msg.Id == 0 || msg.Id > maxId)
                    {
                        continue;
                    }

                    if (msg.Id == maxId)
                    {
                        startPosition = i;
                    }

                    if (msg.Id < maxId)
                    {
                        break;
                    }
                }

                resultMsgs.Add(msg);
                count++;
            }

            return resultMsgs;
        }

        public IList<TLMessageBase> GetHistory(TLPeerBase peer, int limit = Constants.CachedMessagesCount)
        {
            var result = new List<TLMessageBase>();

            if (_database == null) Init();

            if (MessagesContext == null)
            {
                return result;
            }
            var timer = Stopwatch.StartNew();


            IList<TLMessageBase> msgs = new List<TLMessageBase>();
            try
            {
                var withId = peer.Id;
                var dialogBase = _database.Dialogs.FirstOrDefault(x => x.WithId == withId && peer.TypeId == x.Peer.TypeId);

                var dialog = dialogBase as TLDialog;
                if (dialog != null)
                {
                    msgs = dialog.Messages
                        .OfType<TLMessageCommonBase>()
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

           // TLUtils.WritePerformance(string.Format("GetCachedHistory time ({0}): {1}", _database.CountRecords<TLMessageBase>(), timer.Elapsed));
            return msgs.Take(limit).ToList();
        }

        public void GetHistoryAsync(TLPeerBase peer, Action<IList<TLMessageBase>> callback, int limit = Constants.CachedMessagesCount)
        {
            Execute.BeginOnThreadPool(
                () =>
                {
                    var history = GetHistory(peer, limit);
                    callback.SafeInvoke(history);
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
                }

                return null;
            }

            if (message.Id != 0 && MessagesContext != null && MessagesContext.ContainsKey(message.Id))
            {
                return MessagesContext[message.Id];
            }

            if ((message.RandomId ?? 0) != 0 && RandomMessagesContext != null && RandomMessagesContext.ContainsKey(message.RandomId.Value))
            {
                return RandomMessagesContext[message.RandomId.Value];
            }

            return null;
        }

        #region TODO: Encrypted
        //private TLDecryptedMessageBase GetCachedDecryptedMessage(long? randomId)
        //{
        //    if (randomId != null && DecryptedMessagesContext != null && DecryptedMessagesContext.ContainsKey(randomId.Value))
        //    {
        //        return DecryptedMessagesContext[randomId.Value];
        //    }

        //    return null;
        //}

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
        #endregion

        public void SyncSendingMessages(IList<TLMessage> messages, TLMessageBase previousMessage, Action<IList<TLMessage>> callback)
        {
            if (messages == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = new List<TLMessage>();
            if (_database == null) Init();

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var cachedMessage = GetCachedMessage(message) as TLMessage;

                if (cachedMessage != null)
                {
                    _database.UpdateSendingMessage(message, cachedMessage);
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

        public void SyncSendingMessageId(long randomId, int id, Action<TLMessageCommonBase> callback)
        {
            var timer = Stopwatch.StartNew();

            TLMessage result = null;
            if (_database == null) Init();

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

        public void SyncSendingMessage(TLMessageCommonBase message, TLMessageBase previousMessage, Action<TLMessageCommonBase> callback)
        {
            if (message == null)
            {
                callback(null);
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = message;
            if (_database == null) Init();

            var cachedMessage = GetCachedMessage(message);

            if (cachedMessage != null)
            {
                _database.UpdateSendingMessage(message, cachedMessage);
                result = (TLMessage)cachedMessage;
            }
            else
            {
                _database.AddSendingMessage(message, previousMessage);

                // TODO: forwarding
                // forwarding
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
                //            _database.AddSendingMessage(fwdMessage, previousMsg, isLastMsg, isLastMsg);
                //        }
                //    }
                //}     
            }

            _database.Commit();

            TLUtils.WritePerformance("SyncSendingMessage time: " + timer.Elapsed);
            callback(result);
        }

        #region TODO: Encrypted
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

        //public void SyncDecryptedMessages(IList<Tuple<TLDecryptedMessageBase, TLObject>> tuples, TLEncryptedChatBase peer, Action<IList<Tuple<TLDecryptedMessageBase, TLObject>>> callback)
        //{
        //    if (tuples == null)
        //    {
        //        callback(null);
        //        return;
        //    }

        //    var timer = Stopwatch.StartNew();

        //    var result = tuples;
        //    if (_database == null) Init();

        //    foreach (var tuple in tuples)
        //    {
        //        TLDecryptedMessageBase cachedMessage = null;

        //        if (DecryptedMessagesContext != null)
        //        {
        //            cachedMessage = GetCachedDecryptedMessage(tuple.Item1);
        //        }

        //        if (cachedMessage != null)
        //        {
        //            // update fields
        //            if (tuple.Item1.GetType() == cachedMessage.GetType())
        //            {
        //                cachedMessage.Update(tuple.Item1);
        //            }

        //            tuple.Item1 = cachedMessage;
        //        }
        //        else
        //        {
        //            // add object to cache
        //            _database.AddDecryptedMessage(tuple.Item1, peer);
        //        }
        //    }

        //    _database.Commit();

        //    TLUtils.WritePerformance("Sync DecryptedMessage time: " + timer.Elapsed);
        //    callback(result);
        //}

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
        #endregion

        public ExceptionInfo LastSyncMessageException { get; set; }

        public void SyncMessage(TLMessageBase message, Action<TLMessageBase> callback)
        {
            SyncMessage(message, true, true, callback);
        }

        public void SyncEditedMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback)
        {
            try
            {
                if (message == null)
                {
                    callback(null);
                    return;
                }

                var result = message;
                if (_database == null) Init();

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

                    if (message.TypeId == cachedMessage.TypeId)
                    {
                        cachedMessage.Edit(message);
                    }
                    else
                    {
                        _database.RemoveMessageFromContext(cachedMessage);
                        _database.AddMessage(message, notifyNewDialog, notifyTopMessageUpdated);
                    }
                    result = cachedMessage;
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

        public void SyncMessage(TLMessageBase message, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessageBase> callback)
        {
            try
            {
                if (message == null)
                {
                    callback(null);
                    return;
                }

                var result = message;
                if (_database == null) Init();

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

                    if (message.TypeId == cachedMessage.TypeId)
                    {
                        cachedMessage.Update(message);
                    }
                    else
                    {
                        _database.DeleteMessage(cachedMessage);
                        _database.AddMessage(message, notifyNewDialog, notifyTopMessageUpdated);
                    }
                    result = cachedMessage;
                }
                else
                {
                    try
                    {
                        _database.AddMessage(message, notifyNewDialog, notifyTopMessageUpdated);
                    }
                    catch (Exception ex)
                    {
                        LastSyncMessageException = new ExceptionInfo { Exception = ex, Timestamp = DateTime.Now };
                        Helpers.Execute.ShowDebugMessage("SyncMessage ex:\n" + ex);
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

        public void SyncPeerMessages(TLPeerBase peer, TLMessagesMessagesBase messages, bool notifyNewDialog, bool notifyTopMessageUpdated, Action<TLMessagesMessagesBase> callback)
        {
            if (messages == null)
            {
                callback(new TLMessagesMessages());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = messages.GetEmptyObject();
            if (_database == null) Init();

            ProcessPeerReading(peer, messages);

            SyncChatsInternal(messages.Chats, result.Chats);
            SyncUsersInternal(messages.Users, result.Users);
            SyncMessagesInternal(peer, messages.Messages, result.Messages, notifyNewDialog, notifyTopMessageUpdated);

            _database.Commit();

            //TLUtils.WritePerformance("SyncPeerMessages time: " + timer.Elapsed);
            callback(result);
        }

        private void ProcessPeerReading(TLPeerBase peer, TLMessagesMessagesBase messages)
        {
            ITLReadMaxId readMaxId = null;
            if (peer is TLPeerUser)
            {
                readMaxId = GetUser(peer.Id) as ITLReadMaxId;
            }
            else if (peer is TLPeerChat)
            {
                readMaxId = GetChat(peer.Id) as ITLReadMaxId;
            }
            else if (peer is TLPeerChannel)
            {
                readMaxId = GetChat(peer.Id) as ITLReadMaxId;
            }

            if (readMaxId != null)
            {
                foreach (var message in messages.Messages)
                {
                    var messageCommon = message as TLMessageCommonBase;
                    if (messageCommon != null)
                    {
                        if (messageCommon.IsOut 
                            && readMaxId.ReadOutboxMaxId != null 
                            && readMaxId.ReadOutboxMaxId > 0
                            && readMaxId.ReadOutboxMaxId < messageCommon.Id)
                        {
                            messageCommon.SetUnreadSilent(true);
                        }
                        else if (!messageCommon.IsOut
                            && readMaxId.ReadInboxMaxId != null
                            && readMaxId.ReadInboxMaxId > 0
                            && readMaxId.ReadInboxMaxId < messageCommon.Id)
                        {
                            messageCommon.SetUnreadSilent(true);
                        }
                    }
                }
            }
        }

        public void AddMessagesToContext(TLMessagesMessagesBase messages, Action<TLMessagesMessagesBase> callback)
        {
            if (messages == null)
            {
                callback(new TLMessagesMessages());
                return;
            }

            var timer = Stopwatch.StartNew();

            var result = messages.GetEmptyObject();
            if (_database == null) Init();

            SyncChatsInternal(messages.Chats, result.Chats);
            SyncUsersInternal(messages.Users, result.Users);
            foreach (var message in messages.Messages)
            {
                if (GetCachedMessage(message) == null)
                {
                    _database.AddMessageToContext(message);
                }
            }

            _database.Commit();

            //TLUtils.WritePerformance("SyncPeerMessages time: " + timer.Elapsed);
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
            if (_database == null) Init();

            foreach (var contactStatus in contactStatuses)
            {
                var contactStatus19 = contactStatus as TLContactStatus;
                if (contactStatus19 != null)
                {
                    var userId = contactStatus.UserId;
                    var user = GetUser(userId) as TLUser;
                    if (user != null)
                    {
                        // TODO: user._status = contactStatus19.Status;
                        user.Status = contactStatus19.Status;
                    }
                }
            }

            _database.Commit();

            //TLUtils.WritePerformance("SyncPeerMessages time: " + timer.Elapsed);
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
            if (_database == null) Init();

            SyncChatsInternal(difference.Chats, result.Chats, exceptions);
            SyncUsersInternal(difference.Users, result.Users, exceptions);
            SyncMessagesInternal(null, difference.NewMessages, result.NewMessages, false, false, exceptions);
            // TODO: Encrypted SyncEncryptedMessagesInternal(difference.State.Qts, difference.NewEncryptedMessages, result.NewEncryptedMessages, exceptions);

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
            if (_database == null) Init();

            //SyncChatsInternal(difference.Chats, result.Chats, exceptions);
            //SyncUsersInternal(difference.Users, result.Users, exceptions);

            foreach (var messageBase in difference.NewMessages)
            {
                MTProtoService.ProcessSelfMessage(messageBase);
            }

            SyncMessagesInternal(null, difference.NewMessages, result.NewMessages, false, false, exceptions);
            // TODO: Encrypted SyncEncryptedMessagesInternal(difference.State.Qts, difference.NewEncryptedMessages, result.NewEncryptedMessages, exceptions);

            _database.Commit();

            //TLUtils.WritePerformance("Sync difference time: " + timer.Elapsed);
            callback(result);
        }

        private void SyncMessageInternal(TLPeerBase peer, TLMessageBase message, out TLMessageBase result)
        {
            TLMessageCommonBase cachedMessage = null;
            //if (MessagesContext != null)
            {
                cachedMessage = (TLMessageCommonBase) GetCachedMessage(message);
                //cachedMessage = (TLMessage)MessagesContext[message.Index];
            }

            if (cachedMessage != null)
            {
                if (cachedMessage.RandomId != null)
                {
                    _database.RemoveMessageFromContext(cachedMessage);

                    cachedMessage.RandomId = null;

                    _database.AddMessageToContext(cachedMessage);

                }

                // update fields
                if (message.TypeId == cachedMessage.TypeId)
                {
                    cachedMessage.Update(message);
                    //_database.Storage.Modify(cachedMessage);
                }
                // or replace object
                else
                {
                    _database.DeleteMessage(cachedMessage);
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
            TLChannel channel = null;
            long? readInboxMaxId;
            if (peer is TLPeerChannel)
            {
                channel = GetChat(peer.Id) as TLChannel;
            }

            foreach (var message in messages)
            {
                try
                {
                    // for updates we have input message only and set peer to null by default
                    if (peer == null)
                    {
                        peer = TLUtils.GetPeerFromMessage(message);
                        
                        if (peer is TLPeerChannel)
                        {
                            channel = GetChat(peer.Id) as TLChannel;
                            if (channel != null)
                            {
                                readInboxMaxId = channel.ReadInboxMaxId;
                                if (readInboxMaxId != null)
                                {
                                    var messageCommon = message as TLMessageCommonBase;
                                    if (messageCommon != null && !messageCommon.IsOut &&
                                        messageCommon.Id > readInboxMaxId.Value)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                            }
                        }
                    }

                    var cachedMessage = (TLMessageCommonBase)GetCachedMessage(message);

                    if (cachedMessage != null)
                    {
                        if (message.TypeId == cachedMessage.TypeId)
                        {
                            cachedMessage.Update(message);
                        }
                        else
                        {
                            _database.DeleteMessage(cachedMessage);
                            _database.AddMessage(message);
                        }
                        result.Add(cachedMessage);
                    }
                    else
                    {
                        if (peer != null)
                        {
                            if (channel != null)
                            {
                                readInboxMaxId = channel.ReadInboxMaxId;
                                if (readInboxMaxId != null)
                                {
                                    var messageCommon = message as TLMessageCommonBase;
                                    if (messageCommon != null && !messageCommon.IsOut &&
                                        messageCommon.Id > readInboxMaxId.Value)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                            }
                        }

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
            MergeMessagesAndChannels(dialogs);

            //Debug.WriteLine("messages.getDialogs sync dialogs merge messages and channels elapsed=" + stopwatch.Elapsed);

            foreach (TLDialog dialog in dialogs.Dialogs)
            {
                //Debug.WriteLine("messages.getDialogs sync dialogs start get cached elapsed=" + stopwatch.Elapsed);
                TLDialog cachedDialog = null;
                if (DialogsContext != null)
                {
                    cachedDialog = DialogsContext[dialog.Index] as TLDialog;
                }
                //Debug.WriteLine("messages.getDialogs sync dialogs stop get cached elapsed=" + stopwatch.Elapsed);

                if (cachedDialog != null)
                {
                    //Debug.WriteLine("messages.getDialogs sync dialogs start update cached elapsed=" + stopwatch.Elapsed);
                    var raiseTopMessageUpdated = cachedDialog.TopMessage == null || cachedDialog.TopMessage != dialog.TopMessage;
                    cachedDialog.Update(dialog);
                    //Debug.WriteLine("messages.getDialogs sync dialogs stop update cached elapsed=" + stopwatch.Elapsed);
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
                    //Debug.WriteLine("messages.getDialogs sync dialogs start add none cached elapsed=" + stopwatch.Elapsed);
                    // add object to cache
                    result.Dialogs.Add(dialog);
                    _database.AddDialog(dialog);

                    //Debug.WriteLine("messages.getDialogs sync dialogs stop add none cached elapsed=" + stopwatch.Elapsed);
                }
            }

            //Debug.WriteLine("messages.getDialogs sync dialogs foreach elapsed=" + stopwatch.Elapsed);


            
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
                    cachedDialog = DialogsContext[dialog.Index] as TLDialog;
                }

                if (cachedDialog != null)
                {
                    var raiseTopMessageUpdated = cachedDialog.TopMessage == null || cachedDialog.TopMessage != dialog.TopMessage;
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
            if (_database == null) Init();

            //Debug.WriteLine("messages.getDialogs after init elapsed=" + stopwatch.Elapsed);

            MergeReadMaxIdAndNotifySettings(dialogs);

            //Debug.WriteLine("messages.getDialogs merge notify settings elapsed=" + stopwatch.Elapsed);

            SyncChatsInternal(dialogs.Chats, result.Chats);

            //Debug.WriteLine("messages.getDialogs sync chats elapsed=" + stopwatch.Elapsed);

            SyncUsersInternal(dialogs.Users, result.Users);

            //Debug.WriteLine("messages.getDialogs sync users elapsed=" + stopwatch.Elapsed);

            SyncDialogsInternal(dialogs, result);

            //Debug.WriteLine("messages.getDialogs end sync dialogs elapsed=" + stopwatch.Elapsed);

            _database.Commit();

            //Debug.WriteLine("messages.getDialogs after commit elapsed=" + stopwatch.Elapsed);

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
            if (_database == null) Init();

            MergeReadMaxIdAndNotifySettings(dialogs);

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

        private void MergeReadMaxIdAndNotifySettings(TLMessagesDialogsBase dialogs)
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
                    if (dialog.Peer is TLPeerChannel)
                    {
                        TLChatBase chat;
                        if (chatsIndex.TryGetValue(dialog.Index, out chat))
                        {
                            chat.NotifySettings = dialog.NotifySettings;
                        }
                    }
                    else if (dialog.Peer is TLPeerChat)
                    {
                        TLChatBase chat;
                        if (chatsIndex.TryGetValue(dialog.Index, out chat))
                        {
                            chat.NotifySettings = dialog.NotifySettings;
                        }
                    }
                    else if (dialog.Peer is TLPeerUser)
                    {
                        TLUserBase user;
                        if (usersIndex.TryGetValue(dialog.Index, out user))
                        {
                            user.NotifySettings = dialog.NotifySettings;
                        }
                    }
                }

                var dialog53 = dialog as ITLReadMaxId;
                if (dialog53 != null)
                {
                    if (dialog.Peer is TLPeerChannel)
                    {
                        TLChatBase chatBase;
                        if (chatsIndex.TryGetValue(dialog.Index, out chatBase))
                        {
                            var chat = chatBase as ITLReadMaxId;
                            if (chat != null)
                            {
                                chat.ReadInboxMaxId = dialog53.ReadInboxMaxId;
                                chat.ReadOutboxMaxId = dialog53.ReadOutboxMaxId;
                            }
                        }
                    }
                    else if (dialog.Peer is TLPeerChat)
                    {
                        TLChatBase chatBase;
                        if (chatsIndex.TryGetValue(dialog.Index, out chatBase))
                        {
                            var chat = chatBase as ITLReadMaxId;
                            if (chat != null)
                            {
                                chat.ReadInboxMaxId = dialog53.ReadInboxMaxId;
                                chat.ReadOutboxMaxId = dialog53.ReadOutboxMaxId;
                            }
                        }
                    }
                    else if (dialog.Peer is TLPeerUser)
                    {
                        TLUserBase userBase;
                        if (usersIndex.TryGetValue(dialog.Index, out userBase))
                        {
                            var user = userBase as ITLReadMaxId;
                            if (user != null)
                            {
                                user.ReadInboxMaxId = dialog53.ReadInboxMaxId;
                                user.ReadOutboxMaxId = dialog53.ReadOutboxMaxId;
                            }
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
                    var dialog = dialogBase as TLDialog;
                    if (dialog != null)
                    {
                        var peerId = dialog.Peer.Id;
                        dialogsCache[peerId] = dialog;
                    }
                }

                foreach (var messageBase in dialogs.Messages)
                {
                    var message = messageBase as TLMessageCommonBase;
                    if (message != null)
                    {
                        var peerId = message.ToId is TLPeerUser && !message.IsOut? message.FromId.Value : message.ToId.Id;
                        if (!message.IsOut)
                        {
                            TLDialog dialog;
                            if (dialogsCache.TryGetValue(peerId, out dialog))
                            {
                                var dialogChannel = dialog as TLDialog; // TODO: TLDialogChannel;
                                if (dialogChannel != null && dialogChannel.ReadInboxMaxId < message.Id)
                                {
                                    message.SetUnreadSilent(true);
                                }
                            }
                        }

                        Context<TLMessageBase> dialogContext;
                        if (!messagesCache.TryGetValue(peerId, out dialogContext))
                        {
                            dialogContext = new Context<TLMessageBase>();
                            messagesCache[peerId] = dialogContext;
                        }
                        dialogContext[message.Id] = message;
                    }
                }
            }
            catch (Exception ex)
            {
                
            }

            try
            {

                foreach (var dialogCache in messagesCache.Values)
                {

                    foreach (var message in dialogCache.Values)
                    {
                        TLMessageCommonBase cachedMessage = null;
                        //if (MessagesContext != null)
                        {
                            cachedMessage = (TLMessageCommonBase)GetCachedMessage(message);
                            //cachedMessage = (TLMessage)MessagesContext[message.Index];
                        }

                        if (cachedMessage != null)
                        {
                            // update fields
                            if (message.TypeId == cachedMessage.TypeId)
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

                foreach (var dialogBase in dialogs.Dialogs)
                {
                    var peer = dialogBase.Peer;
                    if (peer is TLPeerUser)
                    {
                        dialogBase._with = UsersContext[peer.Id];
                    }
                    else if (peer is TLPeerChat)
                    {
                        dialogBase._with = ChatsContext[peer.Id];
                    }
                    else if (peer is TLPeerChannel)
                    {
                        dialogBase._with = ChatsContext[peer.Id];
                    }

                    var dialog = dialogBase as TLDialog;
                    if (dialog != null)
                    {
                        dialog._topMessageItem = messagesCache[peer.Id][dialogBase.TopMessage];
                        dialog.Messages = new ObservableCollection<TLMessageBase> { dialog.TopMessageItem };
                    }

                    //var dialogChannel = dialogBase as TLDialogChannel;
                    //if (dialog != null)
                    //{
                    //    dialog._topMessage = messagesCache[peer.Id.Value][dialogBase.TopMessageId.Value];
                    //    dialog.Messages = new ObservableCollection<TLMessageBase> { dialog.TopMessage };
                    //}
                }
            }
            catch (Exception ex)
            {

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
            if (_database == null) Init();

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
            if (_database == null) Init();

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
            if (_database == null) Init();

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
            if (_database == null) Init();

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
            if (_database == null) Init();

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
                var user45 = user as TLUser;
                var isMinUser = user45 != null && user45.IsMin;

                // update fields
                if (user.TypeId == cachedUser.TypeId)
                {
                    cachedUser.Update(user);
                    result = cachedUser;
                }
                else if (isMinUser)
                {
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
                        var user45 = user as TLUser;
                        var isMinUser = user45 != null && user45.IsMin;

                        // update fields
                        if (user.TypeId == cachedUser.TypeId)
                        {
                            cachedUser.Update(user);
                            result.Add(cachedUser);
                        }
                        else if (isMinUser)
                        {
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

        #region TODO: Encrypted SecretChats

        //private void SyncEncryptedChatInternal(TLEncryptedChatBase chat, out TLEncryptedChatBase result)
        //{
        //    try
        //    {
        //        TLEncryptedChatBase cachedChat = null;
        //        if (EncryptedChatsContext != null)
        //        {
        //            cachedChat = EncryptedChatsContext[chat.Id];
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
        //                        chat.Key = TLString.FromBigEndianData(authKey);

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
        //                //    chat.Key = TLString.FromBigEndianData(authKey);

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

        //            //var dialog = GetEncryptedDialog(encryptedChat.Id) as TLEncryptedDialog;
        //            //if (dialog != null)
        //            //{

        //            //}

        //            bool commitChat;
        //            var decryptedMessage = UpdatesService.GetDecryptedMessage(MTProtoService.Instance.CurrentUserId, encryptedChat, message, qts, out commitChat);
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
        //                SyncDecryptedMessage(decryptedMessage, encryptedChat, cachedMessage =>
        //                {
        //                    var hasMessagesGap = true;
        //                    var decryptedMessage17 = decryptedMessage as ISeqNo;
        //                    var decryptedMessageService = decryptedMessage as TLDecryptedMessageService;
        //                    var encryptedChat17 = encryptedChat as TLEncryptedChat17;
        //                    var encryptedChat20 = encryptedChat as TLEncryptedChat20;
        //                    var encryptedChat8 = encryptedChat;

        //                    // ttl
        //                    if (decryptedMessageService != null)
        //                    {
        //                        var readMessagesAction = decryptedMessageService.Action as TLDecryptedMessageActionReadMessages;
        //                        if (readMessagesAction != null)
        //                        {
        //                            var items = GetDecryptedHistory(encryptedChat.Id.Value, 100);
        //                            foreach (var randomId in readMessagesAction.RandomIds)
        //                            {
        //                                foreach (var item in items)
        //                                {
        //                                    if (item.RandomId.Value == randomId.Value)
        //                                    {
        //                                        item.Status = TLMessageState.Read;
        //                                        if (item.TTL != null && item.TTL.Value > 0)
        //                                        {
        //                                            item.DeleteDate = new long?(DateTime.Now.Ticks + encryptedChat.MessageTTL.Value * TimeSpan.TicksPerSecond);
        //                                        }

        //                                        var m = item as TLDecryptedMessage17;
        //                                        if (m != null)
        //                                        {
        //                                            var decryptedMediaPhoto = m.Media as TLDecryptedMessageMediaPhoto;
        //                                            if (decryptedMediaPhoto != null)
        //                                            {
        //                                                if (decryptedMediaPhoto.TTLParams == null)
        //                                                {
        //                                                    var ttlParams = new TTLParams();
        //                                                    ttlParams.IsStarted = true;
        //                                                    ttlParams.Total = m.TTL.Value;
        //                                                    ttlParams.StartTime = DateTime.Now;
        //                                                    ttlParams.Out = m.Out.Value;

        //                                                    decryptedMediaPhoto._ttlParams = ttlParams;
        //                                                }
        //                                            }

        //                                            var decryptedMediaVideo17 = m.Media as TLDecryptedMessageMediaVideo17;
        //                                            if (decryptedMediaVideo17 != null)
        //                                            {
        //                                                if (decryptedMediaVideo17.TTLParams == null)
        //                                                {
        //                                                    var ttlParams = new TTLParams();
        //                                                    ttlParams.IsStarted = true;
        //                                                    ttlParams.Total = m.TTL.Value;
        //                                                    ttlParams.StartTime = DateTime.Now;
        //                                                    ttlParams.Out = m.Out.Value;

        //                                                    decryptedMediaVideo17._ttlParams = ttlParams;
        //                                                }
        //                                            }

        //                                            var decryptedMediaAudio17 = m.Media as TLDecryptedMessageMediaAudio17;
        //                                            if (decryptedMediaAudio17 != null)
        //                                            {
        //                                                if (decryptedMediaAudio17.TTLParams == null)
        //                                                {
        //                                                    var ttlParams = new TTLParams();
        //                                                    ttlParams.IsStarted = true;
        //                                                    ttlParams.Total = m.TTL.Value;
        //                                                    ttlParams.StartTime = DateTime.Now;
        //                                                    ttlParams.Out = m.Out.Value;

        //                                                    decryptedMediaAudio17._ttlParams = ttlParams;
        //                                                }
        //                                            }

        //                                            var decryptedMediaDocument45 = m.Media as TLDecryptedMessageMediaDocument45;
        //                                            if (decryptedMediaDocument45 != null && (m.IsVoice() || m.IsVideo()))
        //                                            {
        //                                                if (decryptedMediaDocument45.TTLParams == null)
        //                                                {
        //                                                    var ttlParams = new TTLParams();
        //                                                    ttlParams.IsStarted = true;
        //                                                    ttlParams.Total = m.TTL.Value;
        //                                                    ttlParams.StartTime = DateTime.Now;
        //                                                    ttlParams.Out = m.Out.Value;

        //                                                    decryptedMediaDocument45._ttlParams = ttlParams;
        //                                                }

        //                                                var message45 = m as TLDecryptedMessage45;
        //                                                if (message45 != null)
        //                                                {
        //                                                    message45.SetListened();
        //                                                }
        //                                                decryptedMediaDocument45.NotListened = false;
        //                                            }
        //                                        }
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                        }
        //                    }

        //                    UpdatesService.ProcessPFS(MTProtoService.Instance.SendEncryptedServiceAsync, this, _eventAggregator, encryptedChat20, decryptedMessageService);

        //                    if (decryptedMessage17 != null)
        //                    {
        //                        // если чат уже обновлен до нового слоя, то проверяем rawInSeqNo
        //                        if (encryptedChat17 != null)
        //                        {
        //                            var chatRawInSeqNo = encryptedChat17.RawInSeqNo.Value;
        //                            var messageRawInSeqNo = UpdatesService.GetRawInFromReceivedMessage(MTProtoService.Instance.CurrentUserId, encryptedChat17, decryptedMessage17);

        //                            if (messageRawInSeqNo == chatRawInSeqNo)
        //                            {
        //                                hasMessagesGap = false;
        //                                encryptedChat17.RawInSeqNo = new int?(encryptedChat17.RawInSeqNo.Value + 1);
        //                                SyncEncryptedChat(encryptedChat17, r => { });
        //                            }
        //                            else
        //                            {
        //                                Helpers.Execute.ShowDebugMessage(string.Format("TLUpdateNewEncryptedMessage messageRawInSeqNo != chatRawInSeqNo + 1 chatId={0} chatRawInSeqNo={1} messageRawInSeqNo={2}", encryptedChat17.Id, chatRawInSeqNo, messageRawInSeqNo));
        //                            }
        //                        }
        //                        // обновляем до нового слоя при получении любого сообщения с более высоким слоем
        //                        else if (encryptedChat8 != null)
        //                        {
        //                            hasMessagesGap = false;

        //                            var newLayer = Constants.SecretSupportedLayer;
        //                            if (decryptedMessageService != null)
        //                            {
        //                                var actionNotifyLayer = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
        //                                if (actionNotifyLayer != null)
        //                                {
        //                                    if (actionNotifyLayer.Layer.Value <= Constants.SecretSupportedLayer)
        //                                    {
        //                                        newLayer = actionNotifyLayer.Layer.Value;
        //                                    }
        //                                }
        //                            }

        //                            var layer = new int?(newLayer);
        //                            var rawInSeqNo = 1;      // только что получил сообщение по новому слою
        //                            var rawOutSeqNo = 0;

        //                            UpdatesService.UpgradeSecretChatLayerAndSendNotification(MTProtoService.Instance.SendEncryptedServiceAsync, this, _eventAggregator, encryptedChat8, layer, rawInSeqNo, rawOutSeqNo);
        //                        }
        //                    }
        //                    else if (decryptedMessageService != null)
        //                    {
        //                        hasMessagesGap = false;
        //                        var notifyLayerAction = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
        //                        if (notifyLayerAction != null)
        //                        {
        //                            if (encryptedChat17 != null)
        //                            {
        //                                var newLayer = Constants.SecretSupportedLayer;
        //                                if (notifyLayerAction.Layer.Value <= Constants.SecretSupportedLayer)
        //                                {
        //                                    newLayer = notifyLayerAction.Layer.Value;
        //                                }

        //                                var layer = new int?(newLayer);
        //                                var rawInSeqNo = 0;
        //                                var rawOutSewNo = 0;

        //                                UpdatesService.UpgradeSecretChatLayerAndSendNotification(MTProtoService.Instance.SendEncryptedServiceAsync, this, _eventAggregator, encryptedChat17, layer, rawInSeqNo, rawOutSewNo);
        //                            }
        //                            else if (encryptedChat8 != null)
        //                            {
        //                                var newLayer = Constants.SecretSupportedLayer;
        //                                if (notifyLayerAction.Layer.Value <= Constants.SecretSupportedLayer)
        //                                {
        //                                    newLayer = notifyLayerAction.Layer.Value;
        //                                }

        //                                var layer = new int?(newLayer);
        //                                var rawInSeqNo = 0;
        //                                var rawOutSewNo = 0;

        //                                UpdatesService.UpgradeSecretChatLayerAndSendNotification(MTProtoService.Instance.SendEncryptedServiceAsync, this, _eventAggregator, encryptedChat8, layer, rawInSeqNo, rawOutSewNo);
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        hasMessagesGap = false;
        //                    }

        //                    if (hasMessagesGap)
        //                    {
        //                        Helpers.Execute.ShowDebugMessage("catch gap " + decryptedMessage);
        //                        //return true;
        //                    }

        //                    var decryptedMessageService17 = decryptedMessage as TLDecryptedMessageService17;
        //                    if (decryptedMessageService17 != null)
        //                    {
        //                        var resendAction = decryptedMessageService17.Action as TLDecryptedMessageActionResend;
        //                        if (resendAction != null)
        //                        {
        //                            Helpers.Execute.ShowDebugMessage(string.Format("TLDecryptedMessageActionResend start_seq_no={0} end_seq_no={1}", resendAction.StartSeqNo, resendAction.EndSeqNo));

        //                            //_cacheService.GetDecryptedHistory()
        //                        }

        //                    }


        //                });
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

            if (_database == null) Init();

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
            if (_database == null) Init();

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
            if (_database == null) Init();

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
                        var channel49 = chat as TLChannel;
                        var isMinChannel = channel49 != null && channel49.IsMin;

                        // update fields
                        if (chat.TypeId == cachedChat.TypeId)
                        {
                            cachedChat.Update(chat);
                        }
                        else if (isMinChannel)
                        {

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
                var channel49 = chat as TLChannel;
                var isMinChannel = channel49 != null && channel49.IsMin;

                // update fields
                if (chat.TypeId == cachedChat.TypeId)
                {
                    cachedChat.Update(chat);
                }
                else if (isMinChannel)
                {
                    
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

        #region Contacts

        public void AddUsers(TLVector<TLUserBase> users, Action<TLVector<TLUserBase>> callback)
        {
            if (users == null)
            {
                callback(null);
                return;
            }

            if (_database == null) Init();

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
            if (_database == null) Init();

            SyncContactsInternal(contacts, result);

            _database.Commit();

            TLUtils.WritePerformance("SyncImportedContacts time: " + timer.Elapsed);
            callback(result);
        }

        #region TODO: No idea
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

        //    var messageCommon = message as TLMessage;
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
        #endregion

        public void DeleteDialog(TLDialog dialog)
        {
            if (dialog != null)
            {
                _database.DeleteDialog(dialog);

                _database.Commit();
            }
        }

        public void ClearDialog(TLPeerBase peer)
        {
            if (peer != null)
            {
                _database.ClearDialog(peer);

                _database.Commit();
            }
        }

        public void DeleteUser(int? id)
        {
            _database.DeleteUser(id);
            _database.Commit();
        }

        public void DeleteChat(int? id)
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
                        _database.DeleteMessage(message);
                    }
                }
            }

            _database.Commit();
        }

        // TODO: Encrypted 
        //public void DeleteDecryptedMessages(TLVector<long> randomIds)
        //{
        //    foreach (var id in randomIds)
        //    {
        //        var message = _database.DecryptedMessagesContext[id];
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

        // TODO: Encrypted 
        //public void ClearDecryptedHistoryAsync(int? chatId)
        //{
        //    _database.ClearDecryptedHistory(chatId);

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
                    _database.DeleteMessage(message);
                }
            }

            _database.Commit();
        }

        public void DeleteUserHistory(TLPeerChannel channel, TLPeerUser user)
        {
            if (channel == null || user == null) return;

            _database.DeleteUserHistory(channel, user);

            _database.Commit();
        }

        public void DeleteChannelMessages(int channelId, TLVector<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            var channelContext = _database.ChannelsContext[channelId];
            if (channelContext != null)
            {
                var peer = new TLPeerChannel { Id = channelId };

                var messages = new List<TLMessageBase>();
                foreach (var id in ids)
                {
                    var message = channelContext[id];
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }
                
                _database.DeleteMessages(messages, peer);
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
                    var user45 = user as TLUser;
                    var isMinUser = user45 != null && user45.IsMin;

                    // update fields
                    if (user.TypeId == cachedUser.TypeId)
                    {
                        cachedUser.Update(user);
                        result.Users.Add(cachedUser);
                    }
                    else if (isMinUser)
                    {
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
            if (_database == null) Init();

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
                    var user45 = user as TLUser;
                    var isMinUser = user45 != null && user45.IsMin;

                    // update fields
                    if (user.TypeId == cachedUser.TypeId)
                    {
                        cachedUser.Update(user);
                        result.Users.Add(cachedUser);
                    }
                    else if (isMinUser)
                    {
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
            var chat = with as TLChat;
            var user = with as TLUserBase;

            GetConfigAsync(
                config =>
                {
                    if (channel != null)
                    {
                        var participantsCount = channel.ParticipantIds != null? channel.ParticipantIds.Count : 0;

                        IsBigChat(participantsCount, result =>
                        {
                            featureKey = result ? bigChatKey : chatKey;

                            callback.SafeInvoke(config, featureKey);
                        });

                        return;
                    }

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

        public TLConfig GetConfig()
        {
#if SILVERLIGHT || WIN_RT
            if (_config == null)
            {
                _config = SettingsHelper.GetValue(Constants.ConfigKey) as TLConfig;
            }
#endif
            return _config;
        }


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

        public void ClearConfigImportAsync()
        {
            GetConfigAsync(config =>
            {
                foreach (var option in config.DCOptions)
                {
                    option.IsAuthorized = false;
                    //if (config.ThisDC.Value != option.Id.Value)
                    //{
                    //    option.IsAuthorized = false;
                    //}
                    //else
                    //{
                    //    option.IsAuthorized = true;
                    //}
                }

                SetConfig(config);
            });
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

        public void CompressAsync(Action callback)
        {
            if (_database != null)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    _database.Compress();

                    callback.SafeInvoke();
                });
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

        public void SaveTempSnapshot(string toDirectoryName)
        {
            if (_database != null)
            {
                _database.SaveTempSnapshot(toDirectoryName);
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
