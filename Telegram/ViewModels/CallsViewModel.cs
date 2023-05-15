//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class CallsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        public CallsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<TLCallGroup>(this);
        }

        public IncrementalCollection<TLCallGroup> Items { get; }

        private string _nextOffset = string.Empty;
        private bool _hasMoreItems = true;

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new SearchCallMessages(_nextOffset, 50, false)); //(new TLInputPeerEmpty(), null, null, new TLInputMessagesFilterPhoneCalls(), 0, 0, 0, _lastMaxId, 50);
            if (response is FoundMessages messages)
            {
                _nextOffset = messages.NextOffset;
                _hasMoreItems = messages.NextOffset.Length > 0;

                List<Message> currentMessages = null;
                Chat currentChat = null;
                User currentPeer = null;
                bool currentFailed = false;
                DateTime? currentTime = null;

                foreach (var message in messages.Messages)
                {
                    var chat = ClientService.GetChat(message.ChatId);
                    if (chat == null)
                    {
                        continue;
                    }

                    var call = message.Content as MessageCall;
                    if (call == null)
                    {
                        continue;
                    }

                    var peer = ClientService.GetUser(chat);
                    if (peer == null)
                    {
                        continue;
                    }

                    var outgoing = message.IsOutgoing;
                    var missed = call.DiscardReason is CallDiscardReasonMissed or CallDiscardReasonDeclined;
                    var failed = !outgoing && missed;
                    var time = Formatter.ToLocalTime(message.Date);

                    if (currentPeer != null)
                    {
                        if (currentPeer.Id == peer.Id && currentFailed == failed && currentTime.Value.Date == time.Date)
                        {
                            currentMessages.Add(message);
                            continue;
                        }
                        else
                        {
                            Items.Add(new TLCallGroup(currentMessages, currentChat.Id, currentPeer, currentFailed));
                            totalCount++;
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
                    Items.Add(new TLCallGroup(currentMessages, currentChat.Id, currentPeer, currentFailed));
                    totalCount++;
                }
            }

            return new LoadMoreItemsResult { Count = totalCount };
        }

        public bool HasMoreItems => _hasMoreItems;

        #region Context menu

        public async void DeleteCall(TLCallGroup group)
        {
            var popup = new MessagePopup
            {
                Title = Strings.DeleteCalls,
                Message = Strings.DeleteSelectedCallsText,
                PrimaryButtonText = Strings.Delete,
                SecondaryButtonText = Strings.Cancel,
                CheckBoxLabel = Strings.DeleteCallsForEveryone
            };

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new DeleteMessages(group.ChatId, group.Items.Select(x => x.Id).ToArray(), popup.IsChecked == true));
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
                return string.Format("{0} ({1}) - {2}", Peer.FullName(), Items.Count, DisplayType);
            }

            return string.Format("{0} - {1}", Peer.FullName(), DisplayType);
        }

        private string _displayType;
        public string DisplayType => _displayType ??= GetDisplayType();

        public Message Message => Items.FirstOrDefault();

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
                return Strings.CallMessageIncomingMissed;
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
                var missed = call.DiscardReason is CallDiscardReasonMissed or CallDiscardReasonDeclined;

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

                var missed = call.DiscardReason is CallDiscardReasonMissed or CallDiscardReasonDeclined;

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
                    return Strings.CallMessageOutgoing;
                case TLCallDisplayType.Incoming:
                    return Strings.CallMessageIncoming;
                case TLCallDisplayType.Cancelled:
                    return Strings.CallMessageOutgoingMissed;
                case TLCallDisplayType.Missed:
                    return Strings.CallMessageIncomingMissed;
                default:
                    return null;
            }
        }
    }
}
