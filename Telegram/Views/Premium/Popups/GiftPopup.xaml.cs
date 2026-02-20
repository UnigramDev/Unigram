//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
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
        public GiftGroup(GiftGroupType key, IEnumerable<AvailableGift> source)
        {
            Type = key;
            ItemsSource = source.ToList();
        }

        public GiftGroup(GiftGroupType key, ObservableCollection<ReceivedGift> source)
        {
            Type = key;
            ItemsSource = source;
        }

        public GiftGroupType Type { get; }

        public object ItemsSource { get; }
    }

    public sealed partial class GiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly MessageSender _receiverId;
        private readonly Chat _chat;

        private readonly DiffObservableCollection<AvailableGift> _gifts = new(Constants.DiffOptions);

        public GiftPopup(IClientService clientService, INavigationService navigationService, User user, UserFullInfo fullInfo)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _receiverId = new MessageSenderUser(user.Id);
            clientService.TryGetChatFromUser(user.Id, out _chat);

            Photo.Source = ProfilePictureSource.User(clientService, user);

            if (user.Id != clientService.Options.MyId)
            {
                TextBlockHelper.SetMarkdown(PremiumInfo, string.Format(Strings.Gift2PremiumInfo, user.FirstName));

                StarsTitle.Text = Strings.Gift2Stars;
                TextBlockHelper.SetMarkdown(StarsInfo, string.Format(Strings.Gift2StarsInfo, user.FirstName));

                AddLink(PremiumInfo, Strings.Gift2PremiumInfoLink, PremiumInfoLink_Click);
                AddLink(StarsInfo, Strings.Gift2StarsInfoLink, StarsInfoLink_Click);

                InitializeOptions(clientService);
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

        private void AddLink(TextBlock block, string text, TypedEventHandler<Hyperlink, HyperlinkClickEventArgs> handler)
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

        private async void InitializeGifts(IClientService clientService, bool birthday, bool self)
        {
            var navigation = new ObservableCollection<GiftGroup>();

            var response = await clientService.SendAsync(new GetAvailableGifts());
            if (response is AvailableGifts gifts)
            {
                var all = new List<AvailableGift>();
                var remaining = new List<AvailableGift>();

                foreach (var gift in gifts.Gifts)
                {
                    if (gift.Gift.IsForBirthday && birthday)
                    {
                        all.Add(gift);
                    }
                    else
                    {
                        remaining.Add(gift);
                    }
                }

                all.AddRange(remaining);

                navigation.Add(new GiftGroup(GiftGroupType.All, all));

                if (all.Any(x => x.MinResaleStarCount > 0))
                {
                    navigation.Add(new GiftGroup(GiftGroupType.Collectibles, all.Where(x => x.MinResaleStarCount > 0)));
                }

                Navigation.ItemsSource = navigation;
                Navigation.SelectedIndex = 0;
            }

            if (self)
            {
                return;
            }

            response = await _clientService.SendAsync(new GetReceivedGifts(_clientService.MyId, 0, false, false, true, true, true, false, false, true, false, string.Empty, 50));
            if (response is ReceivedGifts receivedGifts && receivedGifts.Gifts.Count > 0)
            {
                var transferable = new List<ReceivedGift>();
                var now = DateTime.Now.ToTimestamp();

                foreach (var gift in receivedGifts.Gifts)
                {
                    if (gift.Gift is SentGiftUpgraded upgraded && gift.CanBeTransferred && gift.NextTransferDate < now)
                    {
                        transferable.Add(gift);
                    }
                }

                if (transferable.Count > 0)
                {
                    navigation.Insert(Math.Min(navigation.Count, 1), new GiftGroup(GiftGroupType.Mine, new ReceivedGiftsCollection(_clientService, transferable, receivedGifts.NextOffset)));
                }
            }
        }

        public partial class ReceivedGiftsCollection : ObservableCollection<ReceivedGift>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;

            private string _nextOffset;

            public ReceivedGiftsCollection(IClientService clientService, IList<ReceivedGift> gifts, string nextOffset)
                : base(gifts)
            {
                _clientService = clientService;
                _nextOffset = string.IsNullOrEmpty(nextOffset) ? null : nextOffset;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var totalCount = 0u;
                    var now = DateTime.Now.ToTimestamp();

                    var response = await _clientService.SendAsync(new GetReceivedGifts(_clientService.MyId, 0, false, false, true, true, true, false, false, true, false, _nextOffset, 50));
                    if (response is ReceivedGifts gifts)
                    {
                        foreach (var gift in gifts.Gifts)
                        {
                            if (gift.Gift is SentGiftUpgraded && gift.CanBeTransferred && gift.NextTransferDate < now)
                            {
                                Add(gift);
                            }

                            totalCount++;
                        }

                        _nextOffset = string.IsNullOrEmpty(gifts.NextOffset) ? null : gifts.NextOffset;
                    }

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                });
            }

            public bool HasMoreItems => _nextOffset != null;
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ContentDialogResult confirm = ContentDialogResult.Primary;

            if (e.ClickedItem is AvailableGift available)
            {
                if (available is AvailableGift { Gift.UserLimits.RemainingCount: 0, MinResaleStarCount: 0 })
                {
                    ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.Gift2PerUserLimit, available.Gift.UserLimits.TotalCount), new DelayedFileSource(_clientService, available.Gift.Sticker));
                    return;
                }
                else if (available.Gift.NextSendDate > _clientService.UnixTime)
                {
                    var response = await _clientService.SendAsync(new CanSendGift(available.Gift.Id));
                    if (response is CanSendGiftResultFail fail)
                    {
                        _navigationService.ShowPopup(fail.Reason, Strings.GiftLocked, Strings.OK);
                        return;
                    }
                }
            }
            else if (e.ClickedItem is ReceivedGift receivedGift)
            {
                confirm = await TransferGiftPopup.ShowAsync(XamlRoot, _clientService, receivedGift, _chat, false);

                if (confirm == ContentDialogResult.Primary)
                {
                    Hide();

                    var response = await _clientService.SendAsync(new TransferGift(receivedGift.ReceivedGiftId, _receiverId, receivedGift.TransferStarCount));
                    if (response is Ok && receivedGift.Gift is SentGiftUpgraded upgraded)
                    {
                        ToastPopup.Show(XamlRoot, string.Format(Strings.Gift2TransferredText, upgraded.Gift.ToName(), _chat.Title));
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                }

                return;
            }

            Hide();

            if (e.ClickedItem is AvailableGift availableGift)
            {
                if (availableGift.Gift.UserLimits != null && availableGift.Gift.UserLimits.RemainingCount == 0 && availableGift.MinResaleStarCount == 0)
                {
                    ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.Gift2PerUserLimit, availableGift.Gift.UserLimits.TotalCount), new DelayedFileSource(_clientService, availableGift.Gift.Sticker));
                }
                else if (availableGift.Gift.IsPremium && !_clientService.IsPremium)
                {
                    await _navigationService.ShowPopupAsync(new Views.Premium.Popups.PromoPopup(_clientService, availableGift));
                    confirm = ContentDialogResult.None;
                }
                else if (availableGift.Gift.OverallLimits == null || availableGift.Gift.OverallLimits.RemainingCount > 0)
                {
                    confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, availableGift.Gift, _receiverId));
                }
                else if (availableGift.MinResaleStarCount > 0)
                {
                    confirm = await _navigationService.ShowPopupAsync(new ResoldGiftsPopup(_clientService, _navigationService, availableGift, _receiverId));
                }
                else
                {
                    await _navigationService.ShowPopupAsync(new ReceivedGiftPopup(_clientService, _navigationService, availableGift.Gift));
                    confirm = ContentDialogResult.None;
                }
            }
            else if (e.ClickedItem is PremiumGiftPaymentOption option && _receiverId is MessageSenderUser user)
            {
                confirm = await _navigationService.ShowPopupAsync(new SendGiftPopup(_clientService, _navigationService, option, user.UserId));
            }

            if (confirm != ContentDialogResult.Primary)
            {
                await this.ShowQueuedAsync(XamlRoot);
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
                    receivedGiftCell.UpdateGift(_clientService, availableGift);
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
                if (group.ItemsSource is List<AvailableGift> groupSource)
                {
                    if (ScrollingHost.ItemsSource is DiffObservableCollection<AvailableGift>)
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
