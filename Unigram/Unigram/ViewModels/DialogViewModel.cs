using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;
using Template10.Common;
using Telegram.Api.Helpers;
using Unigram.Core.Services;
using Telegram.Api.Services.Updates;
using Telegram.Api.Transport;
using Telegram.Api.Services.Connection;
using System.Threading;
using GalaSoft.MvvmLight.Command;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;
using Windows.UI.Xaml;
using Unigram.Converters;
using Windows.UI.Xaml.Media;

namespace Unigram.ViewModels
{

    public class DialogViewModel : UnigramViewModelBase
    {
        int ChatType=-1;
        //0 if private, 1 if group, 2 if supergroup/channel
        int loadCount =15;
        int loaded = 0;
        public TLPeerBase peer;
        public TLInputPeerBase inputPeer;
        public MessageCollection Messages { get; private set; } = new MessageCollection();
        public string DialogTitle;
        public string LastSeen;
        public Visibility LastSeenVisible;
        public string debug;
        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }
        public object photo;
        public string SendTextHolder;
        public TLUser user;
        private TLUserBase _item;
        public TLUserBase Item
        {
            get
            {                
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }
        public TLUserBase Self
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }
        public TLInputPeerChannel channel;
        private TLPeerChannel _channelItem;
        public TLPeerChannel channelItem
         {
            get
            {
                return _channelItem;
            }
             set
            {
                Set(ref _channelItem, value);
            }
        }
        public TLInputPeerChat chat;
        private TLPeerChat _chatItem;
        public TLPeerChat chatItem
        {
            get
            {
                return _chatItem;
            }
            set
            {
                Set(ref _chatItem, value);
            }
        }

        private TLInputPeerBase _peerBase;
        public TLInputPeerBase selfPeerBase
        {
            get
            {
                return _peerBase;
            }
            set
            {
                Set(ref _peerBase, value);
            }
        }
        private TLInputPeerBase _peer;
        public TLInputPeerBase Peer
        {
            get
            {
                return _peer;
            }
            set
            {
                Set(ref _peer, value);
            }
        }
        public async Task FetchMessages(TLPeerBase peer, TLInputPeerBase inputPeer)
        {
            var result = await ProtoService.GetHistoryAsync(inputPeer, peer, false, loaded, int.MaxValue, loadCount);
            if (result.IsSucceeded)
            {
                ProcessReplies(result.Value.Messages);

                foreach (var item in result.Value.Messages)
                {
                    Messages.Insert(0, item);
                }
            }

            loaded += loadCount;
        }

        public async void ProcessReplies(IList<TLMessageBase> messages)
        {
            var replyIds = new TLVector<int>();
            var replyToMsgs = new List<TLMessage>();

            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i] as TLMessage;
                if (message != null)
                {
                    var replyId = message.ReplyToMsgId;
                    if (replyId != null && replyId.Value != 0)
                    {
                        var channelId = new int?();
                        var channel = message.ToId as TLPeerChat;
                        if (channel != null)
                        {
                            channelId = channel.Id;
                        }

                        var reply = CacheService.GetMessage(replyId.Value, channelId);
                        if (reply != null)
                        {
                            messages[i].Reply = reply;
                        }
                        else
                        {
                            replyIds.Add(replyId.Value);
                            replyToMsgs.Add(message);
                        }
                    }

                    //if (message.NotListened && message.Media != null)
                    //{
                    //    message.Media.NotListened = true;
                    //}
                }
            }

            if (replyIds.Count > 0)
            {
                Task<MTProtoResponse<TLMessagesMessagesBase>> task = null;

                if (Peer is TLInputPeerChannel)
                {
                    var first = replyToMsgs.FirstOrDefault();
                    if (first.ToId is TLPeerChat)
                    {
                        task = ProtoService.GetMessagesAsync(replyIds);
                    }
                    else
                    {
                        var peer = Peer as TLInputPeerChannel;
                        task = ProtoService.GetMessagesAsync(new TLInputChannel { ChannelId = peer.ChannelId, AccessHash = peer.AccessHash }, replyIds);
                    }
                }
                else
                {
                    task = ProtoService.GetMessagesAsync(replyIds);
                }

                var result = await task;
                if (result.IsSucceeded)
                {
                    CacheService.AddChats(result.Value.Chats, (results) => { });
                    CacheService.AddUsers(result.Value.Users, (results) => { });

                    for (int j = 0; j < result.Value.Messages.Count; j++)
                    {
                        for (int k = 0; k < replyToMsgs.Count; k++)
                        {
                            var message = replyToMsgs[k];
                            if (message != null && message.ReplyToMsgId.Value == result.Value.Messages[j].Id)
                            {
                                replyToMsgs[k].Reply = result.Value.Messages[j];
                                replyToMsgs[k].RaisePropertyChanged(() => replyToMsgs[k].ReplyInfo);
                            }
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.getMessages error " + result.Error);
                }
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            loaded = 0;
            channel = parameter as TLInputPeerChannel;
            chat = parameter as TLInputPeerChat;
            user = parameter as TLUser;
            if (user != null)
            {
                //Happy Birthday Alexmitter xD
                Messages.Clear();
                Item = user;
                photo = (TLUserProfilePhotoBase)user.Photo;
                DialogTitle = Item.FullName;
                LastSeen = LastSeenHelper.getLastSeen(user).Item1;
                LastSeenVisible = Visibility.Visible;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };
                Peer = new TLInputPeerUser { UserId = user.Id, AccessHash = user.AccessHash ?? 0 };
                await FetchMessages(peer,inputPeer);
                ChatType = 0;
            }
            else if (channel != null)
            {
                TLInputChannel x=new TLInputChannel();                
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var channelDetails = await ProtoService.GetFullChannelAsync(x);                
                DialogTitle = channelDetails.Value.Chats[0].FullName;
                photo = (TLChatPhotoBase)channelDetails.Value.Chats[0].Photo;
                LastSeenVisible = Visibility.Collapsed;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerChannel { ChannelId = x.ChannelId, AccessHash = x.AccessHash };
                Peer = new TLInputPeerChannel { ChannelId = x.ChannelId, AccessHash = x.AccessHash };
                await FetchMessages(peer, inputPeer);
                channelItem = new TLPeerChannel { Id = channel.ChannelId };
                ChatType = 2;
            }
            else if (chat != null)
            {
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                DialogTitle = chatDetails.Value.Chats[0].FullName;
                photo = (TLChatPhotoBase)chatDetails.Value.Chats[0].Photo;
                LastSeenVisible = Visibility.Collapsed;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerChat { ChatId = chat.ChatId, AccessHash = chat.AccessHash };
                Peer = new TLInputPeerChat { ChatId = chat.ChatId, AccessHash = chat.AccessHash };
                await FetchMessages(peer, inputPeer);
                chatItem = new TLPeerChat { Id = chat.ChatId };
                ChatType = 1;
            }
        }


        public RelayCommand SendCommand => new RelayCommand(SendMessage);
        private void SendMessage()
        {
            var messageText = SendTextHolder;

            TLPeerBase toId = null;

            switch (ChatType)
            {
                case 0:
                    toId = new TLPeerUser { Id = int.Parse(Item.Id.ToString()) };
                    break;
                case 1:
                    toId = new TLPeerChat { Id = int.Parse(chatItem.Id.ToString()) };
                    break;
                case 2:
                    toId = new TLPeerChannel { Id = int.Parse(channelItem.Id.ToString()) };
                    break;
            }

            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
            var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, messageText, new TLMessageMediaEmpty(), TLLong.Random(), 0);
            var previousMessage = InsertSendingMessage(message);

            CacheService.SyncSendingMessage(message, previousMessage, async (m) =>
            {
                await ProtoService.SendMessageAsync(message, () =>
                {
                    // TODO
                });
            });
        }

        private TLMessageBase InsertSendingMessage(TLMessage message, bool useReplyMarkup = false)
        {
            TLMessageBase result;
            if (Messages.Count > 0)
            {
                //if (useReplyMarkup && _replyMarkupMessage != null)
                //{
                //    var chat = With as TLChatBase;
                //    if (chat != null)
                //    {
                //        message.ReplyToMsgId = _replyMarkupMessage.Id;
                //        message.Reply = _replyMarkupMessage;
                //    }

                //    Execute.BeginOnUIThread(() =>
                //    {
                //        if (Reply != null)
                //        {
                //            Reply = null;
                //            SetReplyMarkup(null);
                //        }
                //    });
                //}

                //var messagesContainer = Reply as TLMessagesContainter;
                //if (Reply != null)
                //{
                //    if (Reply.Index != 0)
                //    {
                //        message.ReplyToMsgId = Reply.Id;
                //        message.Reply = Reply;
                //    }
                //    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //    {
                //        message.Reply = Reply;
                //    }

                //    var tLMessage = Reply as TLMessage31;
                //    if (tLMessage != null)
                //    {
                //        var replyMarkup = tLMessage.ReplyMarkup;
                //        if (replyMarkup != null)
                //        {
                //            replyMarkup.HasResponse = true;
                //        }
                //    }

                //    Execute.BeginOnUIThread(delegate
                //    {
                //        Reply = null;
                //    });
                //}

                result = Messages.LastOrDefault();
                Messages.Add(message);

                //if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //{
                //    foreach (var fwdMessage in messagesContainer.FwdMessages)
                //    {
                //        Messages.Insert(0, fwdMessage);
                //    }
                //}

                //for (int i = 1; i < Messages.Count; i++)
                //{
                //    var serviceMessage = Messages[i] as TLMessageService;
                //    if (serviceMessage != null)
                //    {
                //        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                //        if (unreadAction != null)
                //        {
                //            Messages.RemoveAt(i);
                //            break;
                //        }
                //    }
                //}
            }
            else
            {
                //var messagesContainer = Reply as TLMessagesContainter;
                //if (Reply != null)
                //{
                //    if (Reply.Index != 0)
                //    {
                //        message.ReplyToMsgId = Reply.Id;
                //        message.Reply = Reply;
                //    }
                //    else if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //    {
                //        message.Reply = Reply;
                //    }
                //    Reply = null;
                //}

                Messages.Clear();
                Messages.Add(message);

                var history = CacheService.GetHistory(TLUtils.InputPeerToPeer(Peer, ProtoService.CurrentUserId), 15);
                result = history.FirstOrDefault();

                for (int j = 0; j < history.Count; j++)
                {
                    Messages.Add(history[j]);
                }

                //if (messagesContainer != null && !string.IsNullOrEmpty(message.Message.ToString()))
                //{
                //    foreach (var fwdMessage in messagesContainer.FwdMessages)
                //    {
                //        Messages.Insert(0, fwdMessage);
                //    }
                //}

                //for (int k = 1; k < Messages.Count; k++)
                //{
                //    var serviceMessage = Messages[k] as TLMessageService;
                //    if (serviceMessage != null)
                //    {
                //        var unreadAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                //        if (unreadAction != null)
                //        {
                //            Messages.RemoveAt(k);
                //            break;
                //        }
                //    }
                //}
            }
            return result;
        }
    }

    public class MessageCollection : ObservableCollection<TLMessageBase>
    {
        public ObservableCollection<MessageGroup> Groups { get; private set; } = new ObservableCollection<MessageGroup>();

        protected override void InsertItem(int index, TLMessageBase item)
        {
            base.InsertItem(index, item);

            var group = GroupForIndex(index);
            if (group == null || group?.FromId != item.FromId)
            {
                group = new MessageGroup(this, item.From, item.FromId, item.ToId, item is TLMessage ? ((TLMessage)item).IsOut : false);
                Groups.Insert(index == 0 ? 0 : Groups.Count, group); // TODO: should not be 0 all the time
            }

            group.Insert(index - group.FirstIndex, item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);

            var group = GroupForIndex(index);
            if (group != null)
            {
                group.RemoveAt(index - group.FirstIndex);
            }
        }

        private MessageGroup GroupForIndex(int index)
        {
            if (index == 0)
            {
                return Groups.FirstOrDefault();
            }
            else if (index == Count - 1)
            {
                return Groups.LastOrDefault();
            }
            else
            {
                return Groups.FirstOrDefault(x => x.FirstIndex >= index && x.LastIndex <= index);
            }
        }
    }

    public class MessageGroup : ObservableCollection<TLMessageBase>
    {
        private ObservableCollection<MessageGroup> _parent;

        public MessageGroup(MessageCollection parent, TLUser from, int? fromId, TLPeerBase toId, bool isOut)
        {
            _parent = parent.Groups;

            From = from;
            FromId = fromId;
            ToId = toId;
            IsOut = isOut;
        }

        public TLUser From { get; private set; }

        public int? FromId { get; private set; }

        public TLPeerBase ToId { get; private set; }

        public bool IsOut { get; private set; }

        public int FirstIndex
        {
            get
            {
                var count = 0;
                var index = _parent.IndexOf(this);
                if (index > 0)
                {
                    count = _parent[index - 1].LastIndex + 1;
                }

                return count;
            }
        }

        public int LastIndex
        {
            get
            {
                return FirstIndex + Math.Max(0, Count - 1);
            }
        }

        protected override void InsertItem(int index, TLMessageBase item)
        {
            // TODO: experimental
            if (index == 0)
            {
                if (Count > 0)
                    this[0].IsFirst = false;

                item.IsFirst = true;
            }

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
        }
    }
}
