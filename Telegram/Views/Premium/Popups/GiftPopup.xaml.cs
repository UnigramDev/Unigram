//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Controls.Cells;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Premium.Popups
{
    public enum GiftGroupType
    {
        All,
        Mine,
        Collectibles
    }

    public partial class GiftGroup
    {
        public GiftGroup(GiftGroupType key, List<object> source)
        {
            Type = key;
            ItemsSource = source;
        }

        public GiftGroup(GiftGroupType key, ObservableCollection<ReceivedGift> source)
        {
            Type = key;
            ItemsSource = source;
        }

        public GiftGroupType Type { get; }

        public object ItemsSource { get; }
    }

    /// <summary>
    /// The cell that opens a gift's resale listings. A gift still in stock has it next to the
    /// cell that buys it new; a sold out one has only this.
    /// </summary>
    public partial class AvailableGiftResale
    {
        public AvailableGiftResale(AvailableGift gift)
        {
            Gift = gift;
        }

        public AvailableGift Gift { get; }
    }

    public sealed partial class GiftPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly MessageSender _receiverId;
        private readonly Chat _chat;

        private readonly DiffObservableCollection<object> _gifts = new();

        private readonly GiftGroupType _initialGroup;

        /// <summary>
        /// What the receiving user accepts; null for the current user and for channels.
        /// </summary>
        private readonly AcceptedGiftTypes _accepted;

        public GiftPopup(IClientService clientService, INavigationService navigationService, User user, UserFullInfo fullInfo, GiftGroupType initialGroup = GiftGroupType.All)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _initialGroup = initialGroup;

            _receiverId = new MessageSenderUser(user.Id);
            clientService.TryGetChatFromUser(user.Id, out _chat);

            Photo.Source = ProfilePictureSource.User(clientService, user);

            if (user.Id != clientService.Options.MyId)
            {
                _accepted = fullInfo.GiftSettings.AcceptedGiftTypes;

                if (_accepted.PremiumSubscription)
                {
                    TextBlockHelper.SetMarkdown(PremiumInfo, string.Format(Strings.Gift2PremiumInfo, user.FirstName));
                    AddLink(PremiumInfo, Strings.Gift2PremiumInfoLink, PremiumInfoLink_Click);

                    InitializeOptions(clientService);
                }
                else
                {
                    PremiumTitle.Visibility = Visibility.Collapsed;
                    PremiumInfo.Visibility = Visibility.Collapsed;
                }

                StarsTitle.Text = Strings.Gift2Stars;
                TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsInfo, user.FirstName));

                AddLink(StarsInfo, Strings.Gift2StarsInfoLink, StarsInfoLink_Click);
            }
            else
            {
                PremiumTitle.Visibility = Visibility.Collapsed;
                PremiumInfo.Visibility = Visibility.Collapsed;

                StarsTitle.Text = Strings.Gift2StarsSelf;
                TextBlockHelper.SetMarkdown(StarsInfo, Strings.Gift2StarsSelfInfo1 + "\n\n" + Strings.Gift2StarsSelfInfo2);
            }

            ScrollingHost.ItemsSource = _gifts;

            InitializeGifts(clientService, fullInfo.Birthdate?.Day == DateTime.Today.Day
                    && fullInfo.Birthdate?.Month == DateTime.Today.Month, user.Id == clientService.Options.MyId);
        }

        public GiftPopup(IClientService clientService, INavigationService navigationService, Chat chat)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _receiverId = chat.ToMessageSender();
            _chat = chat;

            Photo.Source = ProfilePictureSource.Chat(clientService, chat);

            PremiumTitle.Visibility = Visibility.Collapsed;
            PremiumInfo.Visibility = Visibility.Collapsed;

            StarsTitle.Text = Strings.Gift2StarsChannel;
            TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsChannelInfo, chat.Title));

            AddLink(StarsInfo, Strings.Gift2StarsInfoLink, StarsInfoLink_Click);

            ScrollingHost.ItemsSource = _gifts;

            InitializeGifts(clientService, false, false);
        }

        private void AddLink(TextBlock block, string text, HyperlinkClickHandler handler)
        {
            var hyperlink = new Hyperlink();
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Inlines.Add(text);
            hyperlink.Click += handler;

            block.Inlines.Add(" ");
            block.Inlines.Add(hyperlink);
        }

        private void PremiumInfoLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.ShowPromo();
        }

        private void StarsInfoLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            // TODO: stars promo
        }

        private async void InitializeOptions(IClientService clientService)
        {
            PremiumOptions.ItemsSource = new[]
            {
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
                new PremiumGiftPaymentOption(string.Empty, 0, 0, 0, 0, string.Empty, null),
            };

            var response = await clientService.SendAsync(new GetPremiumGiftPaymentOptions());
            if (response is PremiumGiftPaymentOptions options)
            {
                PremiumOptions.ItemsSource = options.Options
                    .OrderBy(x => x.MonthCount)
                    .ToList();
            }
        }

        /// <summary>
        /// False, after saying so, when the user takes no gift of any kind and the picker would be empty.
        /// </summary>
        public static bool CanReceiveGifts(IClientService clientService, INavigationService navigationService, User user, UserFullInfo fullInfo)
        {
            var accepted = fullInfo.GiftSettings.AcceptedGiftTypes;
            if (user.Id == clientService.Options.MyId || accepted.PremiumSubscription || accepted.UnlimitedGifts || accepted.LimitedGifts || accepted.UpgradedGifts)
            {
                return true;
            }

            navigationService.ShowToast(string.Format(Strings.UserDisallowedGifts, user.FirstName), ToastPopupIcon.Error);
            return false;
        }

        private bool IsAccepted(Gift gift)
        {
            // Channels can't take part in auctions.
            if (_receiverId is MessageSenderChat && gift.AuctionInfo != null)
            {
                return false;
            }
            else if (_accepted == null)
            {
                return true;
            }

            // A limited gift the user refuses can still arrive upgraded, the upgrade paid with it.
            return gift.OverallLimits != null
                ? _accepted.LimitedGifts || gift.UpgradeStarCount > 0 && _accepted.UpgradedGifts
                : _accepted.UnlimitedGifts;
        }

        private async void InitializeGifts(IClientService clientService, bool birthday, bool self)
        {
            var navigation = new ObservableCollection<GiftGroup>();

            var response = await clientService.SendAsync(new GetAvailableGifts());
            if (response is AvailableGifts gifts)
            {
                // Sold out gifts go last; on the user's birthday, birthday gifts lead the rest.
                var sorted = gifts.Gifts
                    .Where(x => IsAccepted(x.Gift))
                    .OrderBy(x => x.Gift.OverallLimits?.RemainingCount == 0 ? 1 : 0)
                    .ThenBy(x => birthday && x.Gift.IsForBirthday ? 0 : 1);

                // Resales are upgraded gifts, so a user who refuses those gets none.
                var resales = _accepted?.UpgradedGifts is not false;

                var all = new List<object>();
                var collectibles = new List<object>();

                foreach (var gift in sorted)
                {
                    if (resales && gift.ResaleCount > 0)
                    {
                        var resale = new AvailableGiftResale(gift);

                        if (gift.Gift.OverallLimits?.RemainingCount != 0)
                        {
                            all.Add(gift);
                        }

                        all.Add(resale);
                        collectibles.Add(resale);
                    }
                    else
                    {
                        all.Add(gift);

                        if (gift.Gift.IsPremium || gift.Gift.NextSendDate != 0)
                        {
                            collectibles.Add(gift);
                        }
                    }
                }

                if (all.Count > 0)
                {
                    navigation.Add(new GiftGroup(GiftGroupType.All, all));
                    navigation.Add(new GiftGroup(GiftGroupType.Collectibles, collectibles));
                }

                Navigation.ItemsSource = navigation;

                if (navigation.Count > 0)
                {
                    Navigation.SelectedIndex = Math.Max(0, navigation.IndexOf(navigation.FirstOrDefault(x => x.Type == _initialGroup)));
                }
            }

            if (self || _accepted?.UpgradedGifts is false)
            {
                UpdateStarsVisibility(navigation);
                return;
            }

            response = await _clientService.SendAsync(new GetReceivedGifts(_clientService.MyId, 0, false, false, true, true, true, false, false, true, false, string.Empty, 50));
            if (response is ReceivedGifts receivedGifts && receivedGifts.Gifts.Count > 0)
            {
                navigation.Insert(Math.Min(navigation.Count, 1), new GiftGroup(GiftGroupType.Mine, new ReceivedGiftsCollection(_clientService, receivedGifts.Gifts, receivedGifts.NextOffset)));

                if (navigation.Count == 1)
                {
                    Navigation.SelectedIndex = 0;
                }
            }

            UpdateStarsVisibility(navigation);
        }

        private void UpdateStarsVisibility(ObservableCollection<GiftGroup> navigation)
        {
            var visibility = navigation.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            StarsTitle.Visibility = visibility;
            StarsInfo.Visibility = visibility;
            Navigation.Visibility = visibility;
        }

        public partial class ReceivedGiftsCollection : IncrementalCollection<ReceivedGift>
        {
            private readonly IClientService _clientService;

            private string _nextOffset;

            public ReceivedGiftsCollection(IClientService clientService, IEnumerable<ReceivedGift> gifts, string nextOffset)
                : base(gifts)
            {
                _clientService = clientService;
                _nextOffset = nextOffset;

                HasMoreItems = nextOffset.Length > 0;
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;
                var hasMoreItems = false;

                var response = await _clientService.SendAsync(new GetReceivedGifts(_clientService.MyId, 0, false, false, true, true, true, false, false, true, false, _nextOffset, 50));
                if (response is ReceivedGifts gifts)
                {
                    foreach (var gift in gifts.Gifts)
                    {
                        Add(gift);
                        totalCount++;
                    }

                    _nextOffset = gifts.NextOffset;
                    hasMoreItems = gifts.NextOffset.Length > 0;
                }

                return new IncrementalLoadResult(totalCount, hasMoreItems);
            }
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ContentDialogResult confirm = ContentDialogResult.None;

            if (e.ClickedItem is ReceivedGift receivedGift)
            {
                if (await TransferGiftPopup.ConfirmTransferAsync(_clientService, _navigationService, receivedGift, _chat))
                {
                    Hide();
                    await TransferGiftPopup.TransferAsync(_clientService, _navigationService, receivedGift, _chat);
                }

                return;
            }
            else if (e.ClickedItem is AvailableGiftResale resale)
            {
                confirm = await _navigationService.ShowPopupAsync(new ResoldGiftsPopup(_clientService, _navigationService, resale.Gift, _receiverId));
            }
            else if (e.ClickedItem is AvailableGift availableGift)
            {
                if (availableGift.Gift.OverallLimits?.RemainingCount == 0)
                {
                    await _navigationService.ShowPopupAsync(new ReceivedGiftPopup(_clientService, _navigationService, availableGift.Gift));
                    return;
                }
                else if (availableGift.Gift.UserLimits?.RemainingCount == 0)
                {
                    ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.Gift2PerUserLimit, availableGift.Gift.UserLimits.TotalCount), new DelayedFileSource(_clientService, availableGift.Gift.Sticker));
                    return;
                }
                else if (availableGift.Gift.NextSendDate > _clientService.UnixTime)
                {
                    // Once the server allows a locked gift, it goes straight to the send sheet, Premium or not.
                    var response = await _clientService.SendAsync(new CanSendGift(availableGift.Gift.Id));
                    if (response is CanSendGiftResultFail fail)
                    {
                        _navigationService.ShowPopup(fail.Reason, Strings.GiftLocked, Strings.OK);
                        return;
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                        return;
                    }
                }
                else if (availableGift.Gift.IsPremium && !_clientService.IsPremium)
                {
                    await _navigationService.ShowPopupAsync(new PromoPopup(_clientService, availableGift));
                    return;
                }

                confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, availableGift.Gift, _receiverId, _accepted));
            }
            else if (e.ClickedItem is PremiumGiftPaymentOption option && _receiverId is MessageSenderUser user)
            {
                confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, option, user.UserId));
            }

            if (confirm == ContentDialogResult.Primary)
            {
                Hide();
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell receivedGiftCell)
            {
                if (args.Item is AvailableGift availableGift)
                {
                    // The send sheet forces the upgrade on a limited gift the user takes only unique.
                    var upgrade = availableGift.Gift.OverallLimits != null && _accepted?.LimitedGifts is false;
                    receivedGiftCell.UpdateGift(_clientService, availableGift, false, upgrade);
                }
                else if (args.Item is AvailableGiftResale resale)
                {
                    receivedGiftCell.UpdateGift(_clientService, resale.Gift, true, false);
                }
                else if (args.Item is ReceivedGift receivedGift)
                {
                    receivedGiftCell.UpdateGift(_clientService, receivedGift, true);
                }
            }
            else if (args.ItemContainer.ContentTemplateRoot is PremiumGiftCell premiumGiftCell && args.Item is PremiumGiftPaymentOption option)
            {
                premiumGiftCell.UpdatePremiumGift(_clientService, option);
            }

            args.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Navigation.SelectedItem is GiftGroup group)
            {
                if (group.ItemsSource is List<object> groupSource)
                {
                    if (ScrollingHost.ItemsSource is DiffObservableCollection<object>)
                    {
                        _gifts.ReplaceDiff(groupSource);
                    }
                    else
                    {
                        _gifts.Clear();
                        _gifts.AddRange(groupSource);

                        ScrollingHost.ItemsSource = _gifts;
                    }
                }
                else
                {
                    ScrollingHost.ItemsSource = group.ItemsSource;
                }
            }
        }

        public static string ConvertGiftGroupStarCountText(GiftGroupType type)
        {
            return type switch
            {
                GiftGroupType.All => Strings.Gift2TabAll,
                GiftGroupType.Mine => Strings.Gift2TabMine,
                GiftGroupType.Collectibles => Strings.Gift2TabCollectibles,
                _ => "???"
            };
        }
    }
}
