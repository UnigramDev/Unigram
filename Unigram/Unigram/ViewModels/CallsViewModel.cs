using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Template10.Utils;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Strings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class CallsViewModel : UnigramViewModelBase
    {
        public CallsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ItemsCollection(protoService, cacheService);

            CallDeleteCommand = new RelayCommand<TLCallGroup>(CallDeleteExecute);
        }

        public ItemsCollection Items { get; private set; }

        public class ItemsCollection : IncrementalCollection<TLCallGroup>
        {
            private readonly IMTProtoService _protoService;
            private readonly ICacheService _cacheService;

            private int _lastMaxId;

            public ItemsCollection(IMTProtoService protoService, ICacheService cacheService)
            {
                _protoService = protoService;
                _cacheService = cacheService;
            }

            public override async Task<IList<TLCallGroup>> LoadDataAsync()
            {
                var response = await _protoService.SearchAsync(new TLInputPeerEmpty(), null, null, new TLInputMessagesFilterPhoneCalls(), 0, 0, 0, _lastMaxId, 50);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count > 0)
                    {
                        _lastMaxId = result.Messages.Min(x => x.Id);
                    }

                    List<TLCallGroup> groups = new List<TLCallGroup>();
                    List<TLMessageService> currentMessages = null;
                    TLUser currentPeer = null;
                    bool currentFailed = false;
                    DateTime? currentTime = null;

                    foreach (TLMessageService message in result.Messages)
                    {
                        var action = message.Action as TLMessageActionPhoneCall;

                        var peer = _cacheService.GetUser(message.IsOut ? message.ToId.Id : message.FromId) as TLUser;
                        if (peer == null)
                        {
                            peer = result.Users.FirstOrDefault(x => x.Id == (message.IsOut ? message.ToId.Id : message.FromId)) as TLUser;
                        }

                        if (peer == null)
                        {
                            continue;
                        }

                        var outgoing = message.IsOut;
                        var reason = action.Reason;
                        var missed = reason is TLPhoneCallDiscardReasonMissed || reason is TLPhoneCallDiscardReasonBusy;
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
                                groups.Add(new TLCallGroup(currentMessages, currentPeer, currentFailed));
                            }
                        }

                        currentPeer = peer;
                        currentMessages = new List<TLMessageService> { message };
                        currentFailed = failed;
                        currentTime = time;
                    }

                    if (currentMessages?.Count > 0)
                    {
                        groups.Add(new TLCallGroup(currentMessages, currentPeer, currentFailed));
                    }

                    return groups;
                }

                return new TLCallGroup[0];
            }
        }

        #region Context menu

        public RelayCommand<TLCallGroup> CallDeleteCommand { get; }
        private async void CallDeleteExecute(TLCallGroup group)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.ConfirmDeleteCallLog, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var messages = new TLVector<int>(group.Items.Select(x => x.Id).ToList());

            Task<MTProtoResponse<TLMessagesAffectedMessages>> task;

            var peer = group.Message.Parent.ToInputPeer();
            if (peer is TLInputPeerChannel channelPeer)
            {
                task = ProtoService.DeleteMessagesAsync(new TLInputChannel { ChannelId = channelPeer.ChannelId, AccessHash = channelPeer.AccessHash }, messages);
            }
            else
            {
                task = ProtoService.DeleteMessagesAsync(messages, false);
            }

            var response = await task;
            if (response.IsSucceeded)
            {
                var cachedMessages = new TLVector<long>();
                var remoteMessages = new TLVector<int>();
                for (int i = 0; i < messages.Count; i++)
                {
                    if (group.Items[i].RandomId.HasValue && group.Items[i].RandomId != 0L)
                    {
                        cachedMessages.Add(group.Items[i].RandomId.Value);
                    }
                    if (group.Items[i].Id > 0)
                    {
                        remoteMessages.Add(group.Items[i].Id);
                    }
                }

                CacheService.DeleteMessages(peer.ToPeer(), null, remoteMessages);
                CacheService.DeleteMessages(cachedMessages);

                Items.Remove(group);
            }
        }

        #endregion
    }

    public class TLCallGroup
    {
        public TLCallGroup(IEnumerable<TLMessageService> messages, TLUser peer, bool failed)
        {
            Items = new ObservableCollection<TLMessageService>(messages);
            Peer = peer;
            IsFailed = failed;
        }

        public ObservableCollection<TLMessageService> Items { get; private set; }

        public TLUser Peer { get; private set; }

        public bool IsFailed { get; private set; }

        public override string ToString()
        {
            if (Items.Count > 1)
            {
                return string.Format("{0} ({1}) - {2}", Peer.FullName, Items.Count, DisplayType);
            }

            return string.Format("{0} - {1}", Peer.FullName, DisplayType);
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

        public TLMessageService Message
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
                return Strings.Android.CallMessageIncomingMissed;
            }

            var finalType = string.Empty;
            var types = new List<TLCallDisplayType>();
            foreach (var message in Items)
            {
                var action = message.Action as TLMessageActionPhoneCall;
                var outgoing = message.IsOut;
                var reason = action.Reason;
                var missed = reason is TLPhoneCallDiscardReasonMissed || reason is TLPhoneCallDiscardReasonBusy;

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
                var action = message.Action as TLMessageActionPhoneCall;
                var reason = action.Reason;
                var missed = reason is TLPhoneCallDiscardReasonMissed || reason is TLPhoneCallDiscardReasonBusy;

                var callDuration = action.Duration ?? 0;
                var duration = missed || callDuration < 1 ? null : LocaleHelper.FormatCallDuration(callDuration);
                finalType = duration != null ? string.Format("{0} ({1})", finalType, duration) : finalType;
            }

            return finalType;
        }

        private string StringForDisplayType(TLCallDisplayType type)
        {
            switch (type)
            {
                case TLCallDisplayType.Outgoing:
                    return Strings.Android.CallMessageOutgoing;
                case TLCallDisplayType.Incoming:
                    return Strings.Android.CallMessageIncoming;
                case TLCallDisplayType.Cancelled:
                    return Strings.Android.CallMessageOutgoingMissed;
                case TLCallDisplayType.Missed:
                    return Strings.Android.CallMessageIncomingMissed;
                default:
                    return null;
            }
        }
    }
}
