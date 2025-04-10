//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileGiftsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner
    {
        private MessageSender _senderId;
        private string _nextOffsetId = string.Empty;

        private List<string> _pinnedGifts = new List<string>();

        public ProfileGiftsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<ReceivedGift>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    _senderId = new MessageSenderChat(chat.Id);
                }
                else
                {
                    _senderId = new MessageSenderUser(user.Id);
                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateGiftIsSaved>(this, Handle)
                .Subscribe<UpdateGiftIsSold>(Handle)
                .Subscribe<UpdateGiftUpgraded>(Handle);
        }

        private void Handle(UpdateGiftIsSaved update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                receivedGift.IsSaved = update.IsSaved;

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, receivedGift);
            });
        }

        private void Handle(UpdateGiftIsSold update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                if (receivedGift.IsPinned)
                {
                    _pinnedGifts.Remove(receivedGift.ReceivedGiftId);
                }

                Items.Remove(receivedGift);
            });
        }

        private void Handle(UpdateGiftUpgraded update)
        {
            BeginOnUIThread(() =>
            {
                var receivedGift = Items.FirstOrDefault(x => x.ReceivedGiftId == update.ReceivedGiftId);
                if (receivedGift == null)
                {
                    return;
                }

                var index = Items.IndexOf(receivedGift);
                Items.Remove(receivedGift);
                Items.Insert(index, update.Gift);
            });
        }

        private bool _excludeUnsaved;
        public bool ExcludeUnsaved
        {
            get => _excludeUnsaved;
            set
            {
                if (Set(ref _excludeUnsaved, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeSaved;
        public bool ExcludeSaved
        {
            get => _excludeSaved;
            set
            {
                if (Set(ref _excludeSaved, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeUnlimited;
        public bool ExcludeUnlimited
        {
            get => _excludeUnlimited;
            set
            {
                if (Set(ref _excludeUnlimited, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeLimited;
        public bool ExcludeLimited
        {
            get => _excludeLimited;
            set
            {
                if (Set(ref _excludeLimited, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _excludeUpgraded;
        public bool ExcludeUpgraded
        {
            get => _excludeUpgraded;
            set
            {
                if (Set(ref _excludeUpgraded, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        private bool _sortByPrice;
        public bool SortByPrice
        {
            get => _sortByPrice;
            set
            {
                if (Set(ref _sortByPrice, value))
                {
                    HasMoreItems = true;
                    Items.Clear();
                }
            }
        }

        public IncrementalCollection<ReceivedGift> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetReceivedGifts(string.Empty, _senderId, _excludeUnsaved, _excludeSaved, _excludeUnlimited, _excludeLimited, _excludeUpgraded, _sortByPrice, _nextOffsetId, 50));
            if (response is ReceivedGifts gifts)
            {
                _nextOffsetId = gifts.NextOffset;

                foreach (var gift in gifts.Gifts)
                {
                    if (gift.IsPinned)
                    {
                        _pinnedGifts.Add(gift.ReceivedGiftId);
                    }

                    Items.Add(gift);
                    total++;
                }
            }

            HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public void OpenGift(ReceivedGift receivedGift)
        {
            if (receivedGift == null)
            {
                return;
            }

            ShowPopup(new ReceivedGiftPopup(ClientService, NavigationService, receivedGift, _senderId));
        }

        public bool IsOwned()
        {
            if (_senderId.IsUser(ClientService.Options.MyId))
            {
                return true;
            }
            else if (ClientService.TryGetSupergroup(_senderId, out Supergroup supergroup))
            {
                return supergroup.CanPostMessages();
            }

            return false;
        }

        public async void PinGift(ReceivedGift gift)
        {
            if (gift.IsPinned)
            {
                _pinnedGifts.Remove(gift.ReceivedGiftId);
            }
            else
            {
                if (_pinnedGifts.Count == ClientService.Options.PinnedGiftCountMax)
                {
                    ShowToast(Locale.Declension(Strings.R.GiftsPinLimit, ClientService.Options.PinnedGiftCountMax), ToastPopupIcon.Info);
                    return;
                }

                _pinnedGifts.Insert(0, gift.ReceivedGiftId);
            }

            var response = await ClientService.SendAsync(new SetPinnedGifts(_senderId, _pinnedGifts));
            if (response is Ok)
            {
                gift.IsPinned = !gift.IsPinned;

                if (gift.IsPinned)
                {
                    Items.Remove(gift);
                    Items.Insert(0, gift);
                }
                else
                {
                    var index = Items.BinarySearch(gift, (x, y) => _pinnedGifts.Contains(y.ReceivedGiftId) ? 1 : y.Date.CompareTo(x.Date));
                    if (index < 0 && (~index < Items.Count || !HasMoreItems))
                    {
                        Items.Remove(gift);
                        Items.Insert(~index, gift);
                    }
                }

                if (gift.IsPinned)
                {
                    ShowToast(string.Format("**{0}**\n{1}", Strings.Gift2PinnedTitle, Strings.Gift2PinnedSubtitle), ToastPopupIcon.Pin);
                }
            }
        }

        private int UnpinGiftComparison(ReceivedGift x, ReceivedGift y)
        {
            if (y.IsPinned)
            {
                return 1;
            }

            return y.Date.CompareTo(x.Date);
        }

        public void SetPinnedItems()
        {
            var receivedGiftIds = new List<string>();

            foreach (var item in Items)
            {
                if (item.IsPinned)
                {
                    receivedGiftIds.Add(item.ReceivedGiftId);
                }
                else
                {
                    break;
                }
            }

            if (receivedGiftIds.Count != _pinnedGifts.Count)
            {
                return;
            }

            ClientService.Send(new SetPinnedGifts(_senderId, receivedGiftIds));
        }

        public void SetPinnedItem(ReceivedGift gift)
        {
            var index = _pinnedGifts.IndexOf(gift.ReceivedGiftId);
            if (index >= 0 && index < Items.Count)
            {
                Items.Remove(gift);
                Items.Insert(index, gift);
            }
        }

        public void CopyGift(ReceivedGift gift)
        {
            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                MessageHelper.CopyLink(ClientService, XamlRoot, new InternalLinkTypeUpgradedGift(upgraded.Gift.Name));
            }
        }

        public void ShareGift(ReceivedGift gift)
        {
            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeUpgradedGift(upgraded.Gift.Name)));
            }
        }

        public async void ToggleGift(ReceivedGift gift)
        {
            var response = await ClientService.SendAsync(new ToggleGiftIsSaved(gift.ReceivedGiftId, !gift.IsSaved));
            if (response is Ok)
            {
                gift.IsSaved = !gift.IsSaved;
                Aggregator.Publish(new UpdateGiftIsSaved(gift.ReceivedGiftId, gift.IsSaved));

                if (gift.IsSaved)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(ClientService, gift.GetSticker()));
                }
            }
        }

        public void TransferGift(ReceivedGift gift)
        {
            NavigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(gift));
        }
    }
}
