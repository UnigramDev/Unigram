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

namespace Unigram.ViewModels
{

    public class DialogViewModel : UnigramViewModelBase
    {
        int ChatType=-1;
        int counter = 0;
        //0 if private, 1 if group, 2 if supergroup/channel
        int loadCount =15;
        int loaded = 0;
        public TLPeerBase peer;
        public TLInputPeerBase inputPeer;
        public MessageCollection Messages { get; private set; } = new MessageCollection();
        public string DialogTitle;
        public string debug;
        public DialogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

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
            List<string> fetchedList = new List<string>();
            var x = await ProtoService.GetHistoryAsync(null, inputPeer, peer, false, loaded, int.MaxValue, loadCount);
            TLVector<TLMessageBase> y = x.Value.Messages;
            foreach (var item in y)
            {                
                Messages.Insert(0, item);
                counter++;
            }
            loaded += loadCount;
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
                DialogTitle = Item.FullName;
                peer = new TLPeerUser { Id = SettingsHelper.UserId };
                inputPeer = new TLInputPeerUser { UserId = user.Id };
                Peer = new TLInputPeerUser { UserId = user.Id };
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

            CacheService.SyncSendingMessage(message, previousMessage, toId, async (m) =>
            {
                await ProtoService.SendMessageAsync(message);
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

                var history = CacheService.GetHistory(ProtoService.CurrentUserId, TLUtils.InputPeerToPeer(Peer, ProtoService.CurrentUserId), 15);
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
                group = new MessageGroup(this, item.From, item.FromId, item.ToId);
                Groups.Insert(0, group);
            }

            group.Insert(index, item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);

            var group = GroupForIndex(index);
            if (group != null)
            {
                group.RemoveAt(index);
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

        public MessageGroup(MessageCollection parent, TLUser from, int? fromId, TLPeerBase toId)
        {
            _parent = parent.Groups;

            From = from;
            FromId = fromId;
            ToId = toId;
        }

        public TLUser From { get; private set; }

        public int? FromId { get; private set; }

        public TLPeerBase ToId { get; private set; }

        public int FirstIndex
        {
            get
            {
                var count = 0;
                var index = _parent.IndexOf(this);
                if (index > 0)
                {
                    count = _parent[index - 1].LastIndex;
                }

                return count;
            }
        }

        public int LastIndex
        {
            get
            {
                return FirstIndex + Count - 1;
            }
        }

        protected override void InsertItem(int index, TLMessageBase item)
        {
            index -= FirstIndex;
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            index -= FirstIndex;
            base.RemoveItem(index);
        }
    }
}
