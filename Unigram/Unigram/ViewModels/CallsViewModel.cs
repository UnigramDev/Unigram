using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public class CallsViewModel : TLViewModelBase
    {
        public CallsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemsCollection(protoService, cacheService);

            CallDeleteCommand = new RelayCommand<TLCallGroup>(CallDeleteExecute);
        }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : IncrementalCollection<TLCallGroup>
        {
            private readonly IProtoService _protoService;
            private readonly ICacheService _cacheService;

            private long _lastMaxId;
            private bool _hasMoreItems = true;

            public ItemsCollection(IProtoService protoService, ICacheService cacheService)
            {
                _protoService = protoService;
                _cacheService = cacheService;
            }

            public override async Task<IList<TLCallGroup>> LoadDataAsync()
            {
                var response = await _protoService.SendAsync(new SearchCallMessages(_lastMaxId, 50, false)); //(new TLInputPeerEmpty(), null, null, new TLInputMessagesFilterPhoneCalls(), 0, 0, 0, _lastMaxId, 50);
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        _lastMaxId = messages.MessagesValue.Min(x => x.Id);
                    }
                    else
                    {
                        _hasMoreItems = false;
                    }

                    List<TLCallGroup> groups = new List<TLCallGroup>();
                    List<Message> currentMessages = null;
                    Chat currentChat = null;
                    User currentPeer = null;
                    bool currentFailed = false;
                    DateTime? currentTime = null;

                    foreach (var message in messages.MessagesValue)
                    {
                        var chat = _protoService.GetChat(message.ChatId);
                        if (chat == null)
                        {
                            continue;
                        }

                        var call = message.Content as MessageCall;
                        if (call == null)
                        {
                            continue;
                        }

                        var peer = _protoService.GetUser(chat);
                        if (peer == null)
                        {
                            continue;
                        }

                        var outgoing = message.IsOutgoing;
                        var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;
                        var failed = !outgoing && missed;
                        var time = BindConvert.Current.DateTime(message.Date);

                        if (currentPeer != null)
                        {
                            if (currentPeer.Id == peer.Id && currentFailed == failed && currentTime.Value.Date == time.Date)
                            {
                                currentMessages.Add(message);
                                continue;
                            }
                            else
                            {
                                groups.Add(new TLCallGroup(currentMessages, currentChat.Id, currentPeer, currentFailed));
                            }
                        }

                        currentChat = chat;
                        currentPeer = peer;
                        currentMessages = new List<Message> { message };
                        currentFailed = failed;
                        currentTime = time;
                    }

                    if (currentMessages?.Count > 0)
                    {
                        groups.Add(new TLCallGroup(currentMessages, currentChat.Id, currentPeer, currentFailed));
                    }

                    return groups;
                }

                return new TLCallGroup[0];
            }

            protected override bool GetHasMoreItems()
            {
                return _hasMoreItems;
            }
        }

        #region Context menu

        public RelayCommand<TLCallGroup> CallDeleteCommand { get; }
        private async void CallDeleteExecute(TLCallGroup group)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ConfirmDeleteCallLog, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new DeleteMessages(group.ChatId, group.Items.Select(x => x.Id).ToArray(), false));
            if (response is Ok)
            {
                Items.Remove(group);
            }
        }

        #endregion
    }

    public class TLCallGroup
    {
        public TLCallGroup(IEnumerable<Message> messages, long chatId, User peer, bool failed)
        {
            Items = new ObservableCollection<Message>(messages);
            ChatId = chatId;
            Peer = peer;
            IsFailed = failed;
        }

        public ObservableCollection<Message> Items { get; private set; }

        public User Peer { get; private set; }

        public long ChatId { get; private set; }

        public bool IsFailed { get; private set; }

        public override string ToString()
        {
            if (Items.Count > 1)
            {
                return string.Format("{0} ({1}) - {2}", Peer.GetFullName(), Items.Count, DisplayType);
            }

            return string.Format("{0} - {1}", Peer.GetFullName(), DisplayType);
        }

        private string _displayType;
        public string DisplayType
        {
            get
            {
                if (_displayType == null)
                    _displayType = GetDisplayType();

                return _displayType;
            }
        }

        public Message Message
        {
            get
            {
                return Items.FirstOrDefault();
            }
        }

        private enum TLCallDisplayType
        {
            Outgoing,
            Incoming,
            Cancelled,
            Missed
        }

        private string GetDisplayType()
        {
            if (IsFailed)
            {
                return Strings.Resources.CallMessageIncomingMissed;
            }

            var finalType = string.Empty;
            var types = new List<TLCallDisplayType>();
            foreach (var message in Items)
            {
                var call = message.Content as MessageCall;
                if (call == null)
                {
                    continue;
                }

                var outgoing = message.IsOutgoing;
                var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

                var type = missed ? (outgoing ? TLCallDisplayType.Cancelled : TLCallDisplayType.Missed) : (outgoing ? TLCallDisplayType.Outgoing : TLCallDisplayType.Incoming);

                if (types.Contains(type)) { }
                else
                {
                    types.Add(type);
                }
            }

            if (types.Count > 1)
            {
                while (types.Contains(TLCallDisplayType.Cancelled))
                {
                    types.Remove(TLCallDisplayType.Cancelled);
                }
            }

            var typesArray = types.OrderBy(x => (int)x);
            foreach (var typeValue in typesArray)
            {
                var type = StringForDisplayType(typeValue);
                if (finalType.Length == 0)
                {
                    finalType = type;
                }
                else
                {
                    finalType += $", {type}";
                }
            }

            if (Items.Count == 1)
            {
                var message = Items[0];

                var call = message.Content as MessageCall;
                if (call == null)
                {
                    return string.Empty;
                }

                var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

                var duration = missed || call.Duration < 1 ? null : Locale.FormatCallDuration(call.Duration);
                finalType = duration != null ? string.Format("{0} ({1})", finalType, duration) : finalType;
            }

            return finalType;
        }

        private string StringForDisplayType(TLCallDisplayType type)
        {
            switch (type)
            {
                case TLCallDisplayType.Outgoing:
                    return Strings.Resources.CallMessageOutgoing;
                case TLCallDisplayType.Incoming:
                    return Strings.Resources.CallMessageIncoming;
                case TLCallDisplayType.Cancelled:
                    return Strings.Resources.CallMessageOutgoingMissed;
                case TLCallDisplayType.Missed:
                    return Strings.Resources.CallMessageIncomingMissed;
                default:
                    return null;
            }
        }
    }
}
