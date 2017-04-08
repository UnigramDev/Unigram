using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Strings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class CallsViewModel : UnigramViewModelBase
    {
        public CallsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ItemsCollection(protoService, cacheService);
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
                var response = await _protoService.SearchAsync(new TLInputPeerEmpty(), null, new TLInputMessagesFilterPhoneCalls(), 0, 0, 0, _lastMaxId, 50);
                if (response.IsSucceeded)
                {
                    if (response.Result.Messages.Count > 0)
                    {
                        _lastMaxId = response.Result.Messages.Min(x => x.Id);
                    }

                    List<TLCallGroup> groups = new List<TLCallGroup>();
                    List<TLMessageService> currentMessages = null;
                    TLUser currentPeer = null;
                    bool currentFailed = false;
                    DateTime? currentTime = null;

                    foreach (TLMessageService message in response.Result.Messages)
                    {
                        var action = message.Action as TLMessageActionPhoneCall;

                        var peer = _cacheService.GetUser(message.IsOut ? message.ToId.Id : message.FromId) as TLUser;
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

                    return groups;
                }

                return new TLCallGroup[0];
            }
        }
    }

    public class TLCallGroup
    {
        public TLCallGroup(IEnumerable<TLMessageService> messages, TLUser peer, bool failed)
        {
            Items = new ObservableCollection<TLMessageService>(messages);
            Peer = peer;
            Failed = failed;
        }

        public ObservableCollection<TLMessageService> Items { get; private set; }

        public TLUser Peer { get; private set; }

        public bool Failed { get; private set; }

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
            if (Failed)
            {
                return AppResources.CallMissedShort;
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
                var duration = missed || callDuration < 1 ? null : BindConvert.Current.CallShortDuration(callDuration);
                finalType = duration != null ? string.Format(AppResources.CallTimeFormat, finalType, duration) : finalType;
            }

            return finalType;
        }

        private string StringForDisplayType(TLCallDisplayType type)
        {
            switch (type)
            {
                case TLCallDisplayType.Outgoing:
                    return AppResources.CallOutgoingShort;
                case TLCallDisplayType.Incoming:
                    return AppResources.CallIncomingShort;
                case TLCallDisplayType.Cancelled:
                    return AppResources.CallCanceledShort;
                case TLCallDisplayType.Missed:
                    return AppResources.CallMissedShort;
                default:
                    return null;
            }
        }
    }
}
