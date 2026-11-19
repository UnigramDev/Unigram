//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Supergroups;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Gifts.Popups;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceivedGiftPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private readonly StarTransaction _transaction;

        private readonly string _transactionId;

        private readonly ReceivedGift _gift;
        private readonly MessageSender _receiverId;

        private readonly MessageSender _sendGiftTo;

        /// <summary>
        /// Opened from the gift's message: the header names its sender and shows its text.
        /// </summary>
        private readonly bool _fromMessage;

        // The profile list the popup was opened from, if any: pinning edits that list's order.
        private readonly ProfileGiftsTabViewModel _profile;

        private TaskCompletionSource<long> _resaleStarCount;

        private TaskCompletionSource<UpgradedGiftValueInfo> _valueInfo;

        private GiftUpgradePreview _preview;
        private int _index;

        private enum GiftPage
        {
            Details,
            Upgrade,
            Wear
        }

        private GiftPage _page;

        private bool _shownWearInfo;

        // The upgrade price follows a schedule; the timer fires when its next entry takes effect.
        private DispatcherTimer _priceTimer;
        private long _upgradeStarCount;

        // The next gift in the profile list that can be upgraded, offered once this one is.
        private ReceivedGift _nextUpgrade;

        // Whether the current user can act for the gift's owner: the receiver of a regular gift,
        // the owner_id of an upgraded one. Recomputed when the gift is upgraded in place.
        private bool _owned;

        // A gift on the blockchain stays with the Telegram account that hosts it; that account can
        // wear and share it, but nothing that would move it.
        private bool _hosted;

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, MessageSender receiverId, MessageSender sendGiftTo, ProfileGiftsTabViewModel profile = null, bool fromMessage = false)
        {
            InitializeComponent();

            _profile = profile;
            _fromMessage = fromMessage;

            _clientService = clientService;
            _navigationService = navigationService;
            _aggregator = clientService.Session.Resolve<IEventAggregator>();

            _gift = gift;
            _receiverId = receiverId;
            _sendGiftTo = sendGiftTo;

            UpdateOwned();
            UpdateVisibility();

            ElementCompositionPreview.SetIsTranslationEnabled(UpgradedAnimatedPhoto, true);

            Closed += OnClosed;

            if (gift.Gift is SentGiftRegular regular)
            {
                if (gift.PrepaidUpgradeHash.Length > 0 || (gift.CanBeUpgraded && _owned))
                {
                    InitializeGift();
                }

                InitializeRegular(clientService, gift, regular.Gift, receiverId);
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                InitializeValue(upgraded.Gift);
                InitializeUpgraded(clientService, gift, upgraded.Gift);
            }
        }

        private void UpdateOwned()
        {
            _owned = _gift?.Gift switch
            {
                SentGiftRegular => IsOwned(_clientService, _receiverId),
                SentGiftUpgraded upgraded => IsOwned(_clientService, upgraded.Gift.OwnerId),
                _ => false
            };

            _hosted = _gift?.Gift is SentGiftUpgraded { Gift.HostId: not null } hosted
                && IsOwned(_clientService, hosted.Gift.HostId);
        }

        private void UpdateVisibility()
        {
            var upgraded = (_gift?.Gift as SentGiftUpgraded)?.Gift;
            var details = _page == GiftPage.Details;
            var wear = _page == GiftPage.Wear;

            // The upgrade page previews the upgraded look over a regular gift.
            var upgradedHeader = upgraded != null || !details;

            DismissButtonRequestedTheme = upgradedHeader ? ElementTheme.Dark : ElementTheme.Default;
            Header.Visibility = upgradedHeader ? Visibility.Collapsed : Visibility.Visible;
            UpgradedHeader.Visibility = upgradedHeader ? Visibility.Visible : Visibility.Collapsed;

            // The wear page turns the header into a preview of the owner's profile. The gift stays
            // laid out under the avatar, both are 140 tall, because PlayWearTransition flies it
            // into the badge and back and hides it through its visual.
            UpgradedFooter.Visibility = wear ? Visibility.Collapsed : Visibility.Visible;
            WearPhoto.Visibility = wear ? Visibility.Visible : Visibility.Collapsed;
            WearFooter.Visibility = wear ? Visibility.Visible : Visibility.Collapsed;

            DetailRoot.Visibility = details ? Visibility.Visible : Visibility.Collapsed;
            UpgradeRoot.Visibility = _page == GiftPage.Upgrade ? Visibility.Visible : Visibility.Collapsed;
            WearRoot.Visibility = wear ? Visibility.Visible : Visibility.Collapsed;

            RegularRoot.Visibility = upgraded != null ? Visibility.Collapsed : Visibility.Visible;
            UpgradedRoot.Visibility = upgraded != null ? Visibility.Visible : Visibility.Collapsed;

            Ribbon.Visibility = details && upgraded?.IsCrafted is true ? Visibility.Visible : Visibility.Collapsed;
            ResaleStarCountRoot.Visibility = details && upgraded?.ResaleParameters != null ? Visibility.Visible : Visibility.Collapsed;
            MoreButton.Visibility = details && (upgraded != null || CanToggleSaved || _gift?.CanBeDeleted is true || ProfileOwner != null) ? Visibility.Visible : Visibility.Collapsed;
            MoreButton.RequestedTheme = upgradedHeader ? ElementTheme.Dark : ElementTheme.Default;
        }

        private bool IsOwned(IClientService clientService, MessageSender receiverId)
        {
            if (receiverId.IsUser(clientService.Options.MyId))
            {
                return true;
            }
            else if (clientService.TryGetSupergroup(receiverId, out Supergroup supergroup))
            {
                return supergroup.CanPostMessages();
            }

            return false;
        }

        private void InitializeRegular(IClientService clientService, ReceivedGift receivedGift, Gift gift, MessageSender receiverId)
        {
            if (clientService.TryGetUser(receivedGift.SenderId, out User user))
            {
                FromPhoto.Source = ProfilePictureSource.User(clientService, user);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(receivedGift.SenderId, out Chat chat))
            {
                FromPhoto.Source = ProfilePictureSource.Chat(clientService, chat);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
            }
            else
            {
                FromPhoto.Source = ProfilePictureSourceText.GetGlyph(Icons.AuthorHiddenFilled, 5);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = Strings.StarsTransactionHidden;
            }

            From.Header = Strings.Gift2From;

            var sentByMe = receivedGift.SenderId?.IsUser(clientService.Options.MyId) is true;
            var forChannel = receiverId is MessageSenderChat;
            var self = _owned && sentByMe && !forChannel;

            // A refunded gift has nothing left to sell.
            var sellable = !receivedGift.WasRefunded
                && receivedGift.SellStarCount > 0
                && receivedGift.Date + clientService.Options.GiftSellPeriod > DateTime.Now.ToUnixTimeSeconds();

            Title.Text = self
                ? Strings.Gift2TitleSaved
                : _owned
                ? Strings.Gift2TitleReceived
                : sentByMe && !forChannel
                ? Strings.Gift2TitleSent
                : Strings.Gift2TitleProfile;

            SetSubtitle(receivedGift.WasRefunded ? null : GetRegularSubtitle(clientService, receivedGift, receiverId, sentByMe, forChannel, self, sellable));

            // The sender's own row would only say "you"; a channel's gift keeps it, since who
            // sent it is the point there.
            From.Visibility = !sentByMe || forChannel ? Visibility.Visible : Visibility.Collapsed;
            FromSendGift.Visibility = !sentByMe && !forChannel
                && clientService.TryGetUser(receivedGift.SenderId, out User sender)
                && sender.Type is UserTypeRegular
                ? Visibility.Visible
                : Visibility.Collapsed;

            // On the profile list the receiver is whose profile it is.
            if (_profile == null && receiverId != null && !receiverId.IsUser(clientService.Options.MyId))
            {
                if (clientService.TryGetUser(receiverId, out User receiverUser))
                {
                    ToPhoto.Source = ProfilePictureSource.User(clientService, receiverUser);
                    ToTitle.Text = receiverUser.FullName();
                }
                else if (clientService.TryGetChat(receiverId, out Chat receiverChat))
                {
                    ToPhoto.Source = ProfilePictureSource.Chat(clientService, receiverChat);
                    ToTitle.Text = receiverChat.Title;
                }

                To.Visibility = Visibility.Visible;
            }
            else
            {
                To.Visibility = Visibility.Collapsed;
            }

            RefundedNote.Visibility = receivedGift.WasRefunded ? Visibility.Visible : Visibility.Collapsed;

            if (!receivedGift.WasRefunded && receivedGift.IsPrivate && !self && (_owned || sentByMe))
            {
                var withText = receivedGift.Text?.Text.Length > 0;

                SenderHiddenNote.Text = _owned
                    ? withText ? Strings.Gift2InSenderMessageHidden2 : Strings.Gift2InSenderHidden2
                    : string.Format(withText ? Strings.Gift2OutSenderMessageHidden2 : Strings.Gift2OutSenderHidden2, clientService.GetTitle(receiverId, true));
                SenderHiddenNote.Visibility = Visibility.Visible;
            }
            else
            {
                SenderHiddenNote.Visibility = Visibility.Collapsed;
            }

            if (_owned)
            {
                if (sellable)
                {
                    Convert.Glyph = Locale.Declension(Strings.R.Gift2ButtonSell, receivedGift.SellStarCount);
                    Convert.Visibility = Visibility.Visible;
                }
                else
                {
                    Convert.Visibility = Visibility.Collapsed;
                }

                if (receivedGift.IsSaved)
                {
                    Info.Text = forChannel
                        ? Strings.Gift2ChannelProfileVisible3
                        : Strings.Gift2ProfileVisible4;
                }
                else
                {
                    Info.Text = forChannel
                        ? Strings.Gift2ChannelProfileInvisible3
                        : Strings.Gift2ProfileInvisible4;
                }

                if (receivedGift.CanBeUpgraded)
                {
                    PrimaryButtonText = receivedGift.PrepaidUpgradeStarCount > 0
                        ? Strings.Gift2UpgradeButtonFree
                        : Strings.Gift2UpgradeButtonGift;
                }
                else
                {
                    PrimaryButtonText = Strings.OK;
                }

                Info.Visibility = receivedGift.WasRefunded ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                Convert.Visibility = Visibility.Collapsed;
                Info.Visibility = Visibility.Collapsed;

                PrimaryButtonText = string.IsNullOrEmpty(receivedGift.PrepaidUpgradeHash)
                    ? Strings.OK
                    : Strings.Gift2GiftAnUpgrade;
            }

            AnimatedPhoto.LoopCount = 0;
            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Sticker);

            // A prepaid upgrade is part of what the gift cost, unless it was bought afterwards.
            var value = gift.StarCount + (receivedGift.IsUpgradeSeparate ? 0 : receivedGift.PrepaidUpgradeStarCount);
            StarCount.Text = value.ToString("N0");

            if (gift.OverallLimits != null && !receivedGift.WasRefunded)
            {
                Availability.Visibility = Visibility.Visible;
                Availability.Content = gift.RemainingText();
            }
            else
            {
                Availability.Visibility = Visibility.Collapsed;
            }

            if (clientService.TryGetChat(gift.PublisherChatId, out Chat publisherChat)
                && clientService.TryGetSupergroup(publisherChat, out Supergroup publisher)
                && publisher.HasActiveUsername(out string username))
            {
                var hyperlink = new Hyperlink();
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Inlines.Add($"@{username}");
                hyperlink.Click += Publisher_Click;

                var text = Strings.Gift2ReleasedBy.Replace("**", string.Empty);
                var index = text.IndexOf("{0}");

                var prefix = text.Substring(0, index);
                var suffix = text.Substring(index + 3);

                Subtitle.Inlines.Clear();
                Subtitle.Inlines.Add(prefix);
                Subtitle.Inlines.Add(hyperlink);
                Subtitle.Inlines.Add(suffix);
                Subtitle.Visibility = Visibility.Visible;
            }

            Date.Content = Formatter.DateAt(receivedGift.Date);

            // A refund takes the message with it.
            if (receivedGift.Text?.Text.Length > 0 && !receivedGift.WasRefunded)
            {
                TableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                TableRoot.CornerRadius = new CornerRadius(4, 4, 0, 0);

                CaptionRoot.Visibility = Visibility.Visible;
                Caption.SetText(clientService, receivedGift.Text);
            }
        }

        private void SetSubtitle(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                Subtitle.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBlockHelper.SetMarkdown(Subtitle, markdown);
                Subtitle.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// What the viewer can do with a regular gift, worded for who they are to it: whoever acts
        /// for the receiver, the sender, or anybody else, who is told nothing.
        /// </summary>
        private string GetRegularSubtitle(IClientService clientService, ReceivedGift receivedGift, MessageSender receiverId, bool sentByMe, bool forChannel, bool self, bool sellable)
        {
            var prepaidUpgrade = receivedGift.PrepaidUpgradeStarCount > 0;

            if (self)
            {
                return receivedGift.CanBeUpgraded
                    ? Strings.Gift2SelfInfoUpgrade
                    : receivedGift.SellStarCount > 0
                    ? Locale.Declension(Strings.R.Gift2SelfInfoConvert, receivedGift.SellStarCount)
                    : Strings.Gift2SelfInfo;
            }
            else if (!_owned && (!sentByMe || forChannel))
            {
                return null;
            }

            var name = clientService.GetTitle(receiverId, true);

            if (prepaidUpgrade && (!_owned || receivedGift.CanBeUpgraded))
            {
                return _owned
                    ? Strings.Gift2InfoInFreeUpgrade
                    : string.Format(Strings.Gift2InfoFreeUpgrade, name);
            }

            // A bot cannot sell what it is given, so a gift to one only ever stays or goes.
            var fromBot = clientService.TryGetUser(receiverId, out User receiver) && receiver.Type is UserTypeBot;

            if (_owned)
            {
                if (fromBot || !sellable)
                {
                    return receivedGift.IsSaved
                        ? forChannel ? Strings.Gift2Info2ChannelRemove : Strings.Gift2Info2BotRemove
                        : forChannel ? Strings.Gift2Info2ChannelKeep : Strings.Gift2Info2BotKeep;
                }

                return Locale.Declension(forChannel ? Strings.R.Gift2Info3Channel : Strings.R.Gift2Info3, receivedGift.SellStarCount);
            }

            if (receivedGift.CanBeUpgraded && prepaidUpgrade)
            {
                return string.Format(Strings.Gift2Info2OutUpgrade, name);
            }
            else if (fromBot || !sellable)
            {
                return string.Format(Strings.Gift2Info2OutExpired, name);
            }
            else if (receivedGift.IsSaved)
            {
                return string.Format(Strings.Gift2InfoOutPinned, name);
            }

            return Locale.Declension(Strings.R.Gift2InfoOut, receivedGift.SellStarCount, name);
        }

        private void InitializeUpgraded(IClientService clientService, ReceivedGift receivedGift, UpgradedGift gift)
        {
            _resaleStarCount = new();

            if (gift.RegularGiftId != 0)
            {
                InitializeResaleStarCount(gift.RegularGiftId);
            }
            else
            {
                // regular_gift_id can be 0 for a short time for old gifts loaded from the database.
                _clientService.Send(new GetAvailableGifts(), result =>
                {
                    var availableGift = result is AvailableGifts availableGifts
                        ? availableGifts.Gifts.FirstOrDefault(x => x.Title == gift.Title)
                        : null;

                    if (availableGift != null)
                    {
                        InitializeResaleStarCount(availableGift.Gift.Id);
                    }
                    else
                    {
                        _resaleStarCount.TrySetResult(0);
                    }
                });
            }

            UpgradedHeader.Update(clientService, gift);
            UpgradedAnimatedPhoto.Source = DelayedFileSource.FromSticker(clientService, gift.Model.Sticker);
            UpgradedTitle.Text = gift.Title;

            Ribbon.Text = Strings.GiftCrafted;

            if (clientService.TryGetChat(gift.PublisherChatId, out Chat publisherChat)
                && clientService.TryGetSupergroup(publisherChat, out Supergroup publisher)
                && publisher.HasActiveUsername(out string username))
            {
                UpgradedSubtitle.Visibility = Visibility.Collapsed;
                UpgradedPublisher.Visibility = Visibility.Visible;
                TextBlockHelper.SetMarkdown(UpgradedPublisherLabel, Locale.Declension(Strings.R.Gift2CollectionNumberBy, gift.Number, $"@{username}"));
            }
            else
            {
                UpgradedPublisher.Visibility = Visibility.Collapsed;
                UpgradedSubtitle.Visibility = Visibility.Visible;
                UpgradedSubtitle.Text = Locale.Declension(Strings.R.Gift2CollectionNumber, gift.Number);
            }

            // TDLib still names a private gift's sender to its receiver; the header leaves it out for everyone.
            var from = _fromMessage && !receivedGift.IsPrivate ? receivedGift.SenderId : null;

            if (from != null && UpgradedPublisher.Visibility == Visibility.Collapsed)
            {
                // The subtitle no longer carries the number, so the title does.
                UpgradedTitle.Text = gift.ToName();
                TextBlockHelper.SetMarkdown(UpgradedSubtitle, string.Format(Strings.UniqueGiftFrom, clientService.GetTitle(from, true)));
            }

            if (_fromMessage && receivedGift.Text?.Text.Length > 0)
            {
                UpgradedMessage.Visibility = Visibility.Visible;
                UpgradedMessageText.SetText(clientService, receivedGift.Text);

                if (from != null)
                {
                    UpgradedMessagePhoto.Visibility = Visibility.Visible;
                    UpgradedMessagePhoto.Source = ProfilePictureSource.MessageSender(clientService, from);
                }
                else
                {
                    UpgradedMessagePhoto.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                UpgradedMessage.Visibility = Visibility.Collapsed;
            }

            var hosted = gift.HostId != null;
            var holder = HolderOf(gift);

            if (gift.OwnerAddress.Length > 0)
            {
                // The tail stays whole so the address is cut in the middle, however much of it fits.
                var split = Math.Max(0, gift.OwnerAddress.Length - 6);

                UpgradedOwnerAddress.Header = hosted ? Strings.Gift2HostAddress : Strings.Gift2Owner;
                UpgradedOwnerAddress.Visibility = Visibility.Visible;
                UpgradedOwnerAddressStart.Text = gift.OwnerAddress.Substring(0, split);
                UpgradedOwnerAddressEnd.Text = gift.OwnerAddress.Substring(split);
            }
            else
            {
                UpgradedOwnerAddress.Visibility = Visibility.Collapsed;
            }

            UpgradedOwner.Header = hosted ? Strings.Gift2Host : Strings.Gift2Owner;
            UpgradedOwner.Visibility = hosted || gift.OwnerAddress.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (gift.OwnerAddress.Length > 0 && gift.GiftAddress.Length > 0)
            {
                // Above the table when a Telegram account hosts the gift, below it otherwise.
                Grid.SetRow(UpgradedBlockchain, hosted ? 0 : 3);
                UpgradedBlockchain.Visibility = Visibility.Visible;
            }
            else
            {
                UpgradedBlockchain.Visibility = Visibility.Collapsed;
            }

            if (clientService.TryGetUser(holder, out User user))
            {
                UpgradedFromPhoto.Source = ProfilePictureSource.User(clientService, user);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(holder, out Chat chat))
            {
                UpgradedFromPhoto.Source = ProfilePictureSource.Chat(clientService, chat);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = chat.Title;
            }
            else
            {
                UpgradedFromPhoto.Visibility = Visibility.Collapsed;
                UpgradedFromText.Text = gift.OwnerName;
            }

            From.Header = Strings.Gift2From;
            Title.Text = Strings.Gift2TitleReceived;

            UpdateAttributes(gift);

            if (gift.ValueAmount != 0)
            {
                UpgradedValue.Text = "~" + Locale.FormatCurrency(gift.ValueAmount, gift.ValueCurrency);
            }
            else
            {
                UpgradedValueRoot.Visibility = Visibility.Collapsed;
            }

            UpgradedQuantity.Content =
                Locale.Declension(Strings.R.Gift2QuantityIssued1, gift.TotalUpgradedCount) +
                Locale.Declension(Strings.R.Gift2QuantityIssued2, gift.MaxUpgradedCount);

            if (gift.OriginalDetails != null)
            {
                UpgradedTableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                UpgradedTableRoot.CornerRadius = new CornerRadius(4, 4, 0, 0);

                UpgradedCaptionRoot.Visibility = Visibility.Visible;

                var senderName = clientService.GetTitle(gift.OriginalDetails.SenderId);
                var senderText = senderName != null
                    ? new FormattedText(senderName, new[] { new TextEntity(0, senderName.Length, gift.OriginalDetails.SenderId.ToTextEntityType()) })
                    : null;

                var receiverName = clientService.GetTitle(gift.OriginalDetails.ReceiverId);
                var receiverText = receiverName != null
                    ? new FormattedText(receiverName, new[] { new TextEntity(0, receiverName.Length, gift.OriginalDetails.ReceiverId.ToTextEntityType()) })
                    : null;

                var date = Formatter.Date(gift.OriginalDetails.Date);
                var dateText = date.AsFormattedText();

                FormattedText text;

                var comment = gift.OriginalDetails.Text.Text.Length > 0;
                var self = gift.OriginalDetails.SenderId.AreTheSame(gift.OriginalDetails.ReceiverId);

                if (self)
                {
                    text = comment
                        ? ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelfComment, senderText, dateText, gift.OriginalDetails.Text)
                        : ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelf, senderText, dateText);
                }
                else if (senderText != null)
                {
                    text = comment
                        ? ClientEx.Format(Strings.Gift2AttributeOriginalDetailsComment, senderText, receiverText, dateText, gift.OriginalDetails.Text)
                        : ClientEx.Format(Strings.Gift2AttributeOriginalDetails, senderText, receiverText, dateText);
                }
                else
                {
                    text = comment
                        ? ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSenderComment, receiverText, dateText, gift.OriginalDetails.Text)
                        : ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSender, receiverText, dateText);
                }

                UpgradedCaption.SetText(clientService, text);
            }

            if ((_owned || _hosted) && receivedGift.ReceivedGiftId.Length > 0)
            {
                UpgradedButtons.Visibility = Visibility.Visible;

                // The host's Transfer stays in place, dimmed, and explains itself when clicked.
                TransferButton.Opacity = _owned ? 1 : 0.5;

                if (_owned)
                {
                    UpdateUpgradedInfo(receivedGift, gift);

                    UpgradedCaptionRemove.Visibility = receivedGift.DropOriginalDetailsStarCount > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                    if (gift.ResaleParameters != null)
                    {
                        ResellButton.Glyph = Icons.TagOffFilled;
                        ResellButton.Content = Strings.Gift2ActionUnlist;
                    }
                    else
                    {
                        ResellButton.Glyph = Icons.TagFilled;
                        ResellButton.Content = Strings.Gift2ActionResell;
                    }
                }
                else
                {
                    Info.Visibility = Visibility.Collapsed;

                    ResellButton.Glyph = Icons.ShareFilled;
                    ResellButton.Content = Strings.Gift2ActionShare;
                }

                UpdateWearButton(IsWorn(gift));
            }
            else
            {
                Info.Visibility = Visibility.Collapsed;
            }

            if (gift.ResaleParameters != null)
            {
                UpdateResalePrice(gift.ResaleParameters);
            }

            PrimaryButtonText = GetUpgradedPrimaryButtonText(gift);
        }

        private void UpdateAttributes(UpgradedGift gift)
        {
            UpgradedModel.Text = gift.Model.Name;
            UpgradedModelRarity.Glyph = gift.Model.Rarity.ToText();
            gift.Model.Rarity.ToColor(UpgradedModelRarity);
            UpgradedBackdrop.Text = gift.Backdrop.Name;
            UpgradedBackdropRarity.Glyph = gift.Backdrop.Rarity.ToText();
            gift.Backdrop.Rarity.ToColor(UpgradedBackdropRarity);
            UpgradedSymbol.Text = gift.Symbol.Name;
            UpgradedSymbolRarity.Glyph = gift.Symbol.Rarity.ToText();
            gift.Symbol.Rarity.ToColor(UpgradedSymbolRarity);
        }

        private string GetUpgradedPrimaryButtonText(UpgradedGift gift)
        {
            // Opened from a link, the current user's own gift arrives without a received gift identifier.
            return gift.ResaleParameters != null && !_owned
                ? gift.ResaleParameters.GramOnly
                    ? string.Format(Strings.ResellGiftBuyTON, TransferGiftPopup.FormatGram(gift.ResaleParameters.GramCentCount)).ReplaceStar(Icons.Ton)
                    : Locale.Declension(Strings.R.ResellGiftBuy, gift.ResaleParameters.StarCount).ReplaceStar(Icons.Premium)
                : _nextUpgrade != null
                ? Strings.Gift2UpgradeNext
                : Strings.OK;
        }

        private void InitializeResaleStarCount(long regularGiftId)
        {
            _clientService.Send(new SearchGiftsForResale(regularGiftId, new GiftForResaleOrderPrice(), false, false, Array.Empty<UpgradedGiftAttributeId>(), string.Empty, 1), result =>
            {
                if (result is GiftsForResale gifts && gifts.Gifts.Count > 0)
                {
                    _resaleStarCount.TrySetResult(gifts.Gifts[0].Gift.ResaleParameters.StarCount);
                }
                else
                {
                    _resaleStarCount.TrySetResult(0);
                }
            });
        }

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, Gift gift)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            UpdateVisibility();

            From.Visibility = Visibility.Collapsed;

            Title.Text = Strings.Gift2SoldOutSheetTitle;
            Subtitle.Text = Strings.Gift2SoldOutSheetSubtitle;
            Subtitle.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

            if (gift.FirstSendDate != 0)
            {
                FirstSale.Visibility = Visibility.Visible;
                FirstSale.Content = Formatter.DateAt(gift.FirstSendDate);
            }

            if (gift.LastSendDate != 0)
            {
                LastSale.Visibility = Visibility.Visible;
                LastSale.Content = Formatter.DateAt(gift.LastSendDate);
            }

            StarCountRoot.Header = Strings.Gift2SoldOutSheetValue;

            Convert.Visibility = Visibility.Collapsed;
            Info.Visibility = Visibility.Collapsed;

            AnimatedPhoto.LoopCount = 0;
            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Sticker);

            Date.Visibility = Visibility.Collapsed;

            StarCount.Text = gift.StarCount.ToString("N0");

            Availability.Visibility = Visibility.Visible;
            Availability.Content = gift.RemainingText();

            PrimaryButtonText = Strings.OK;
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (_rolling)
            {
                args.Cancel = true;
                SkipRoll();
                return;
            }

            // The popup hides after the click unless cancelled. Branches that stay open cancel;
            // the resale purchase must not, because its confirmation queues behind this popup.
            if (_submitted)
            {
                args.Cancel = true;
                return;
            }

            _submitted = true;

            if (_page == GiftPage.Wear)
            {
                args.Cancel = true;
                _shownWearInfo = true;
                ToggleWear();
            }
            else if (_gift?.Gift is SentGiftRegular)
            {
                var upgradable = _owned
                    ? _gift.CanBeUpgraded
                    : _gift.PrepaidUpgradeHash.Length > 0;

                if (upgradable is false)
                {
                    Hide(ContentDialogResult.Primary);
                }
                else if (_page == GiftPage.Details)
                {
                    args.Cancel = true;
                    ShowPage(GiftPage.Upgrade);
                }
                else
                {
                    args.Cancel = true;
                    Upgrade2();
                }
            }
            else if (_gift?.Gift is SentGiftUpgraded { Gift.ResaleParameters: not null } && !_owned)
            {
                BuyResale();
            }
            else if (_nextUpgrade != null)
            {
                Hide();
                _profile.OpenGift(_nextUpgrade);
            }
            else
            {
                Hide(ContentDialogResult.Primary);
            }
        }

        private bool _submitted;
        private bool _completed;

        private async void Upgrade2()
        {
            if (_gift.Gift is not SentGiftRegular)
            {
                return;
            }

            IsPrimaryButtonPending = true;

            var starCount = _gift.PrepaidUpgradeStarCount > 0 || _gift.IsUpgradeSeparate ? 0 : _upgradeStarCount;

            Function function;
            if (_owned)
            {
                function = new UpgradeGift(_gift.ReceivedGiftId, KeepOriginalDetails.IsChecked is true, starCount);
            }
            else
            {
                function = new BuyGiftUpgrade(_receiverId, _gift.PrepaidUpgradeHash, starCount);
            }

            var response = await _clientService.SendPaymentAsync(starCount, function);
            if (response is UpgradeGiftResult result)
            {
                var id = _gift.ReceivedGiftId;

                _gift.ReceivedGiftId = result.ReceivedGiftId;
                _gift.ExportDate = result.ExportDate;
                _gift.NextResaleDate = result.NextResaleDate;
                _gift.NextTransferDate = result.NextTransferDate;
                _gift.DropOriginalDetailsStarCount = result.DropOriginalDetailsStarCount;
                _gift.TransferStarCount = result.TransferStarCount;
                _gift.CanBeTransferred = result.CanBeTransferred;
                _gift.IsSaved = result.IsSaved;
                _gift.Gift = new SentGiftUpgraded(result.Gift);

                _aggregator.Publish(new UpdateGiftUpgraded(id, result.ReceivedGiftId, _gift));

                UpgradedAnimatedPhoto.LoopCompleted -= OnLoopCompleted;
                _priceTimer?.Stop();

                UpdateOwned();
                _nextUpgrade = FindNextUpgrade();

                InitializeUpgraded(_clientService, _gift, result.Gift);
                ShowPage(GiftPage.Details);

                if (!StartRoll(result.Gift))
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2UpgradedTitle, string.Format(Strings.Gift2UpgradedText, result.Gift.ToName())), ToastPopupIcon.Gift);
                }
            }
            else if (response is Ok)
            {
                Hide();

                var receiverId = _receiverId;
                var text = string.Format("**{0}**\n{1}", Strings.StarsGiftUpgradeCompleted, string.Format(Strings.StarsGiftUpgradeCompletedText, _clientService.GetTitle(receiverId, true)));

                // Paid for from elsewhere, the upgrade lands in the receiver's chat, with a way on to
                // the rest of their gifts that can still be upgraded.
                var chat = await _clientService.GetChatFromMessageSenderAsync(receiverId);
                if (chat != null && !_navigationService.IsChatOpen(chat.Id))
                {
                    _navigationService.NavigateToChat(chat);

                    var confirm = await ToastPopup.ShowActionAsync(XamlRoot, text, Strings.StarsGiftUpgradeCompletedMoreButton, ToastPopupIcon.Gift);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _navigationService.NavigateToSender(receiverId, new NavigationState { { "open_gifts", true }, { "open_gifts_upgradable", true } });
                    }
                }
                else
                {
                    ToastPopup.Show(XamlRoot, text, ToastPopupIcon.Gift);
                }
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            _submitted = false;
            IsPrimaryButtonPending = false;

            //Hide();
            //ViewModel.Submit();
        }

        /// <summary>
        /// The first gift after this one in the profile list, wrapping around, that can still be
        /// upgraded.
        /// </summary>
        private ReceivedGift FindNextUpgrade()
        {
            if (_profile == null || !_owned)
            {
                return null;
            }

            // The list may already have swapped this gift for its upgraded copy, and then the scan
            // starts from the top.
            var items = _profile.Items;
            var start = items.IndexOf(_gift) + 1;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[(start + i) % items.Count];
                if (item.Gift is SentGiftRegular && item.CanBeUpgraded)
                {
                    return item;
                }
            }

            return null;
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (_gift?.Gift is SentGiftRegular)
            {
                var expiration = Formatter.ToLocalTime(_gift.Date + _clientService.Options.GiftSellPeriod);
                var diff = expiration - DateTime.Now;

                var message = Locale.Declension(Strings.R.Gift2ConvertText2, (long)diff.TotalDays, _clientService.GetTitle(_gift.SenderId), Locale.Declension(Strings.R.StarsCount, _gift.SellStarCount));

                var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, message, Strings.Gift2ConvertTitle, Strings.Gift2ConvertButton, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SellGift(_gift.ReceivedGiftId));
                    if (response is Ok)
                    {
                        Hide(ContentDialogResult.Secondary);

                        _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));

                        // A channel's Stars land on its own balance, not the user's.
                        if (_receiverId is MessageSenderChat receiverChat)
                        {
                            _navigationService.Navigate(typeof(RevenuePage), receiverChat.ChatId, new NavigationState { { "selectedIndex", 2 } });
                            ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2ConvertedTitle, Locale.Declension(Strings.R.Gift2ConvertedChannel, _gift.SellStarCount)), ToastPopupIcon.StarsTopup);
                        }
                        else
                        {
                            _navigationService.Navigate(typeof(StarsPage));
                            ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2ConvertedTitle, Locale.Declension(Strings.R.Gift2Converted, _gift.SellStarCount)), ToastPopupIcon.StarsTopup);
                        }
                    }
                }
            }
        }

        private void UpdateUpgradedInfo(ReceivedGift receivedGift, UpgradedGift gift)
        {
            if (receivedGift.IsSaved)
            {
                Info.Text = HolderOf(gift) is MessageSenderUser
                    ? Strings.Gift2ProfileVisible4
                    : Strings.Gift2ChannelProfileVisible3;
            }
            else
            {
                Info.Text = HolderOf(gift) is MessageSenderUser
                    ? Strings.Gift2ProfileInvisible4
                    : Strings.Gift2ChannelProfileInvisible3;
            }
        }

        private void UpdateSavedInfo()
        {
            if (_gift.Gift is SentGiftRegular regular)
            {
                InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
            }
            else if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                UpdateUpgradedInfo(_gift, upgraded.Gift);
            }
        }

        private void Visibility_Click(object sender, TextUrlClickEventArgs e)
        {
            ToggleSaved();
        }

        private async void ToggleSaved()
        {
            var response = await _clientService.SendAsync(new ToggleGiftIsSaved(_gift.ReceivedGiftId, !_gift.IsSaved));
            if (response is Ok)
            {
                _gift.IsSaved = !_gift.IsSaved;
                _aggregator.Publish(new UpdateGiftIsSaved(_gift.ReceivedGiftId, _gift.IsSaved));

                UpdateSavedInfo();

                var owner = (_gift.Gift as SentGiftUpgraded)?.Gift.OwnerId ?? _receiverId;
                if (owner is MessageSenderChat)
                {
                    ToastPopup.Show(XamlRoot, _gift.IsSaved
                        ? string.Format("**{0}**\n{1}", Strings.Gift2ChannelMadePublicTitle, Strings.Gift2ChannelMadePublic)
                        : string.Format("**{0}**\n{1}", Strings.Gift2ChannelMadePrivateTitle, Strings.Gift2ChannelMadePrivate), new DelayedFileSource(_clientService, _gift.GetSticker()));
                }
                else if (_gift.IsSaved)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(_clientService, _gift.GetSticker()));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(_clientService, _gift.GetSticker()));
                }
            }
        }

        private async void UpgradedModelRarity_Click(object sender, RoutedEventArgs e)
        {
            ShowVariants();
        }

        private void UpgradedBackdropRarity_Click(object sender, RoutedEventArgs e)
        {
            ShowVariants();
        }

        private void UpgradedSymbolRarity_Click(object sender, RoutedEventArgs e)
        {
            ShowVariants();
        }

        private async void ShowVariants()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                var response = await _clientService.SendAsync(new GetUpgradedGiftVariants(upgraded.Gift.RegularGiftId, !upgraded.Gift.IsCrafted, upgraded.Gift.IsCrafted));
                if (response is GiftUpgradeVariants variants)
                {
                    Hide();

                    await _navigationService.ShowPopupAsync(new GiftVariantsPopup(_clientService, _navigationService, _gift, variants));
                    await this.ShowQueuedAsync(XamlRoot);
                }
            }
        }

        private void ShowPage(GiftPage page)
        {
            if (_page == page)
            {
                return;
            }

            var previous = _page;
            var upgradedHeader = UpgradedHeader.Visibility == Visibility.Visible;

            _priceTimer?.Stop();

            // Leaving the wear page, the badge has to be measured while it is still laid out.
            var badgeOffset = previous == GiftPage.Wear ? GetBadgeOffset() : Vector3.Zero;

            _page = page;
            _submitted = false;

            UpdateVisibility();

            if (page == GiftPage.Upgrade)
            {
                InitializeUpgradePage();
            }
            else if (page == GiftPage.Wear)
            {
                InitializeWearPage();
            }
            else if (_gift.Gift is SentGiftRegular regular)
            {
                InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
            }
            else if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                PrimaryButtonText = GetUpgradedPrimaryButtonText(upgraded.Gift);
            }

            // The card's height eases by itself (ModalPopup), so what is left is the content.
            FadeIn(page switch
            {
                GiftPage.Upgrade => UpgradeRoot,
                GiftPage.Wear => WearRoot,
                _ => DetailRoot
            });

            if (upgradedHeader != (UpgradedHeader.Visibility == Visibility.Visible))
            {
                FadeIn(upgradedHeader ? Header : UpgradedHeader);
            }

            // After the page is filled in: the badge sits right after the owner's name.
            if (page == GiftPage.Wear)
            {
                PlayWearTransition(true, Vector3.Zero);
            }
            else if (previous == GiftPage.Wear)
            {
                PlayWearTransition(false, badgeOffset);
            }
        }

        private static void FadeIn(UIElement element)
        {
            var visual = ElementComposition.GetElementVisual(element);

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);
            opacity.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);
        }

        /// <summary>
        /// Where the badge next to the owner's name is, as a translation from the gift's own spot.
        /// </summary>
        private Vector3 GetBadgeOffset()
        {
            if (WearIcon.ActualWidth == 0 || UpgradedAnimatedPhoto.ActualWidth == 0)
            {
                return Vector3.Zero;
            }

            var badge = WearIcon.TransformToVisual(UpgradedHeader).TransformPoint(new Point(WearIcon.ActualWidth / 2, WearIcon.ActualHeight / 2));
            var photo = UpgradedAnimatedPhoto.TransformToVisual(UpgradedHeader).TransformPoint(new Point(UpgradedAnimatedPhoto.ActualWidth / 2, UpgradedAnimatedPhoto.ActualHeight / 2));

            return new Vector3((float)(badge.X - photo.X), (float)(badge.Y - photo.Y), 0);
        }

        /// <summary>
        /// Flies the gift into the badge after the owner's name while their photo takes its place,
        /// or back out of it. The large image does the flying in both directions, so it is only
        /// ever drawn at or below its own size; the 24 px badge takes over once it has landed.
        /// </summary>
        private void PlayWearTransition(bool wear, Vector3 badgeOffset)
        {
            if (wear)
            {
                // WearFooter was collapsed until now.
                UpdateLayout();
                badgeOffset = GetBadgeOffset();
            }

            var photo = ElementComposition.GetElementVisual(UpgradedAnimatedPhoto);
            var badge = ElementComposition.GetElementVisual(WearIcon);
            var avatar = ElementComposition.GetElementVisual(WearPhoto);
            var compositor = photo.Compositor;

            var landed = UpgradedAnimatedPhoto.ActualWidth > 0
                ? (float)(WearIcon.ActualWidth / UpgradedAnimatedPhoto.ActualWidth)
                : 0.2f;

            photo.CenterPoint = new Vector3(UpgradedAnimatedPhoto.ActualSize / 2, 0);
            avatar.CenterPoint = new Vector3(WearPhoto.ActualSize / 2, 0);

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, wear ? Vector3.One : new Vector3(landed, landed, 1));
            scale.InsertKeyFrame(1, wear ? new Vector3(landed, landed, 1) : Vector3.One);
            scale.Duration = Constants.FastAnimation;

            var translation = compositor.CreateVector3KeyFrameAnimation();
            translation.InsertKeyFrame(0, wear ? Vector3.Zero : badgeOffset);
            translation.InsertKeyFrame(1, wear ? badgeOffset : Vector3.Zero);
            translation.Duration = Constants.FastAnimation;

            // The swap between the flying image and the badge happens on the last frame, with no
            // completion handler: a page change in the middle simply starts the other direction.
            var photoOpacity = compositor.CreateScalarKeyFrameAnimation();
            photoOpacity.InsertKeyFrame(0, 1);
            photoOpacity.InsertKeyFrame(0.99f, 1);
            photoOpacity.InsertKeyFrame(1, wear ? 0 : 1);
            photoOpacity.Duration = Constants.FastAnimation;

            photo.StartAnimation("Scale", scale);
            photo.StartAnimation("Translation", translation);
            photo.StartAnimation("Opacity", photoOpacity);

            if (wear)
            {
                var badgeOpacity = compositor.CreateScalarKeyFrameAnimation();
                badgeOpacity.InsertKeyFrame(0, 0);
                badgeOpacity.InsertKeyFrame(0.99f, 0);
                badgeOpacity.InsertKeyFrame(1, 1);
                badgeOpacity.Duration = Constants.FastAnimation;

                var avatarScale = compositor.CreateVector3KeyFrameAnimation();
                avatarScale.InsertKeyFrame(0, new Vector3(0.5f, 0.5f, 1));
                avatarScale.InsertKeyFrame(1, Vector3.One);
                avatarScale.Duration = Constants.FastAnimation;

                var avatarOpacity = compositor.CreateScalarKeyFrameAnimation();
                avatarOpacity.InsertKeyFrame(0, 0);
                avatarOpacity.InsertKeyFrame(1, 1);
                avatarOpacity.Duration = Constants.FastAnimation;

                badge.StartAnimation("Opacity", badgeOpacity);
                avatar.StartAnimation("Scale", avatarScale);
                avatar.StartAnimation("Opacity", avatarOpacity);

                // The name and status come in with the photo; the badge stays out of it.
                FadeIn(WearName);
                FadeIn(WearStatus);
            }
            else
            {
                FadeIn(UpgradedFooter);
            }
        }

        private void InitializeWearPage()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var gift = upgraded.Gift;

            if (_clientService.TryGetUser(gift.OwnerId, out User user))
            {
                WearPhoto.Source = ProfilePictureSource.User(_clientService, user);
                WearName.Text = user.FullName();
                WearStatus.Text = Strings.Online;
            }
            else if (_clientService.TryGetChat(gift.OwnerId, out Chat chat))
            {
                WearPhoto.Source = ProfilePictureSource.Chat(_clientService, chat);
                WearName.Text = chat.Title;

                if (_clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    WearStatus.Text = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroup.MemberCount);
                }
            }

            WearIcon.Source = DelayedFileSource.FromSticker(_clientService, gift.Model.Sticker);
            WearTitle.Text = string.Format(Strings.Gift2WearTitle, gift.ToName());

            // Only a user's own emoji status needs Premium; a channel's is gated by its boost level instead.
            PrimaryButtonText = gift.OwnerId is MessageSenderUser && !_clientService.IsPremium
                ? Strings.Gift2WearStart + " " + Icons.LockClosedFilled14
                : Strings.Gift2WearStart;
        }

        private void InitializeUpgradePage()
        {
            if (_gift.Gift is not SentGiftRegular regular)
            {
                return;
            }

            if (_owned)
            {
                UpgradedTitle.Text = Strings.Gift2UpgradeTitle;
                UpgradedSubtitle.Text = Strings.Gift2UpgradeText;

                KeepOriginalDetails.Content = _gift.IsPrivate
                    ? _receiverId is MessageSenderChat ? Strings.Gift2AddMyNameNameChannel : Strings.Gift2AddMyNameName
                    : _gift.Text?.Text.Length > 0 ? Strings.Gift2AddSenderNameComment : Strings.Gift2AddSenderName;

                // An upgrade the sender paid for along with the gift is one they meant to sign.
                KeepOriginalDetails.IsChecked = !_gift.IsPrivate && _gift.PrepaidUpgradeStarCount > 0 && !_gift.IsUpgradeSeparate;
                KeepOriginalDetails.Visibility = Visibility.Visible;
            }
            else
            {
                var receiverName = _clientService.GetTitle(_receiverId, true);

                UpgradedTitle.Text = Strings.Gift2PrepayUpgradeTitle;
                UpgradedSubtitle.Text = string.Format(Strings.Gift2PrepayUpgradeText, receiverName);

                UpgradeText1.Content = Strings.Gift2UpgradeFeature1Title;
                UpgradeText1.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature1Text, receiverName);
                UpgradeText2.Content = Strings.Gift2UpgradeFeature2Title;
                UpgradeText2.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature2Text, receiverName);
                UpgradeText3.Content = Strings.Gift2UpgradeFeature3Title;
                UpgradeText3.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature3Text, receiverName);

                KeepOriginalDetails.Visibility = Visibility.Collapsed;
            }

            UpdateGift();
            UpdateUpgradePrice();
        }

        /// <summary>
        /// Prices the upgrade from the schedule in the preview, falling back to the gift's own
        /// price until the preview arrives, and arms the timer for the next drop.
        /// </summary>
        private void UpdateUpgradePrice()
        {
            if (_gift.Gift is not SentGiftRegular regular)
            {
                return;
            }

            var now = DateTime.Now.ToUnixTimeSeconds();
            var price = regular.Gift.UpgradeStarCount;
            var effective = 0L;
            var next = 0L;

            if (_preview != null)
            {
                FindUpgradePrice(_preview.Prices, now, ref price, ref effective, ref next);
                FindUpgradePrice(_preview.NextPrices, now, ref price, ref effective, ref next);
            }

            _upgradeStarCount = price;

            PrimaryButtonText = _gift.PrepaidUpgradeHash.Length > 0
                ? string.Format(Strings.Gift2PrepayUpgradeButton.ReplaceStar(Icons.Premium), price)
                : _gift.PrepaidUpgradeStarCount > 0
                ? Strings.Gift2UpgradeButtonFree
                : string.Format(Strings.Gift2UpgradeButton.ReplaceStar(Icons.Premium), price);

            var paid = _gift.PrepaidUpgradeStarCount == 0;

            UpgradeCostsInfo.Visibility = paid && next != 0 && _preview?.Prices.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (paid && next != 0)
            {
                if (_priceTimer == null)
                {
                    _priceTimer = new DispatcherTimer();
                    _priceTimer.Tick += OnPriceTick;
                }

                _priceTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, next - now));
                _priceTimer.Start();
            }
            else
            {
                _priceTimer?.Stop();
            }
        }

        /// <summary>
        /// The entry that took effect last is the price now; the earliest one still ahead is when
        /// it next drops.
        /// </summary>
        private static void FindUpgradePrice(Vector<GiftUpgradePrice> prices, long now, ref long price, ref long effective, ref long next)
        {
            foreach (var entry in prices)
            {
                if (entry.Date <= now)
                {
                    if (entry.Date >= effective)
                    {
                        effective = entry.Date;
                        price = entry.StarCount;
                    }
                }
                else if (next == 0 || entry.Date < next)
                {
                    next = entry.Date;
                }
            }
        }

        private void OnPriceTick(object sender, object e)
        {
            _priceTimer.Stop();

            if (_page == GiftPage.Upgrade)
            {
                UpdateUpgradePrice();
            }
        }

        private void OnClosed(ModalPopup sender, ModalPopupClosedEventArgs args)
        {
            _priceTimer?.Stop();
            StopRoll();
        }

        private async void UpgradeCosts_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_preview == null)
            {
                return;
            }

            Hide();

            await _navigationService.ShowPopupAsync(new GiftUpgradePricesPopup(_preview));
            await this.ShowQueuedAsync(XamlRoot);

            // Closing stopped the timer, and the price may have dropped meanwhile.
            UpdateUpgradePrice();
        }

        private async void InitializeValue(UpgradedGift gift)
        {
            _valueInfo = new TaskCompletionSource<UpgradedGiftValueInfo>();

            var response = await _clientService.SendAsync(new GetUpgradedGiftValueInfo(gift.Name));
            if (response is UpgradedGiftValueInfo valueInfo)
            {
                _valueInfo.SetResult(valueInfo);
            }
            else
            {
                _valueInfo.SetResult(null);
            }
        }

        private async void InitializeGift()
        {
            UpgradedAnimatedPhoto.LoopCompleted += OnLoopCompleted;

            var gift = _gift.Gift as SentGiftRegular;

            var response = await _clientService.SendAsync(new GetGiftUpgradePreview(gift.Gift.Id));
            if (response is GiftUpgradePreview preview)
            {
                // The roll after the upgrade only spins through models that have loaded.
                foreach (var item in preview.Models.Reverse())
                {
                    PreloadRoll(item.Sticker.StickerValue, 32);
                }

                foreach (var item in preview.Symbols.Reverse())
                {
                    PreloadRoll(item.Sticker.StickerValue, 31);
                }

                _preview = preview;

                if (_page == GiftPage.Upgrade)
                {
                    UpdateGift();
                    UpdateUpgradePrice();
                }
            }
        }

        private void OnLoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            this.BeginOnUIThread(UpdateGift);
        }

        private void UpdateGift()
        {
            if (_preview == null)
            {
                return;
            }

            var random = new Random(_index++);

            var model = _preview.Models[random.Next(_preview.Models.Count)];
            var symbol = _preview.Symbols[random.Next(_preview.Symbols.Count)];
            var backdrop = _preview.Backdrops[random.Next(_preview.Backdrops.Count)];

            _shownModel = model;
            _shownSymbol = symbol;
            _shownBackdrop = backdrop;

            var pattern = new DelayedFileSource(_clientService, symbol.Sticker);
            var centerColor = backdrop.Colors.CenterColor.ToColor();
            var edgeColor = backdrop.Colors.EdgeColor.ToColor();
            var symbolColor = backdrop.Colors.SymbolColor.ToColor();

            UpgradedHeader.Update(pattern, centerColor, edgeColor, symbolColor);
            UpgradedAnimatedPhoto.Source = new DelayedFileSource(_clientService, model.Sticker);
        }

        protected override void OnDismissButtonClick()
        {
            // The dismiss button goes back from a page to the details.
            if (_page == GiftPage.Details)
            {
                base.OnDismissButtonClick();
            }
            else
            {
                ShowPage(GiftPage.Details);
            }
        }

        // Pinning and the profile toggle edit the profile list, so they are offered where it is on
        // screen; everywhere else the footer's link still toggles the profile.
        private bool CanToggleSaved => _profile != null && _owned && _gift?.ReceivedGiftId.Length > 0;

        /// <summary>
        /// Whose gifts View in Profile opens; null when the popup was opened from that list already.
        /// </summary>
        private MessageSender ProfileOwner => _profile != null ? null : _gift?.Gift switch
        {
            SentGiftUpgraded upgraded => HolderOf(upgraded.Gift),
            SentGiftRegular => _receiverId,
            _ => null
        };

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            var upgraded = (_gift.Gift as SentGiftUpgraded)?.Gift;

            // setPinnedGifts takes upgraded gifts only.
            if (upgraded != null && CanToggleSaved)
            {
                flyout.CreateFlyoutItem(Pin, _gift.IsPinned ? Strings.Gift2Unpin : Strings.Gift2Pin, _gift.IsPinned ? Icons.PinOff : Icons.Pin);
            }

            if (upgraded != null && _owned)
            {
                if (!upgraded.IsCrafted && _gift.CraftDate > 0)
                {
                    // TODO: Icon
                    flyout.CreateFlyoutItem(Craft, Strings.GiftCraft, Icons.Hamburger);
                }

                if (upgraded.ResaleParameters != null)
                {
                    flyout.CreateFlyoutItem(ChangePrice, Strings.Gift2ChangePrice, Icons.Tag);
                }
            }

            if (upgraded != null)
            {
                flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);
                flyout.CreateFlyoutItem(Share, Strings.ShareFile, Icons.Share);

                // A chat theme belongs to a user, so a channel's gift cannot be one.
                if (upgraded.IsThemeAvailable && upgraded.OwnerId.IsUser(_clientService.Options.MyId))
                {
                    flyout.CreateFlyoutItem(SetTheme, Strings.GiftThemesSetIn, Icons.PaintBrush);
                }
            }

            if (CanToggleSaved)
            {
                flyout.CreateFlyoutItem(ToggleSaved, _gift.IsSaved ? Strings.Gift2HideFromProfile : Strings.Gift2ShowOnProfile, _gift.IsSaved ? Icons.EyeOff : Icons.Eye);
            }

            if (_gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(Transfer, Strings.Gift2TransferOption, Icons.Replace);
            }

            if (ProfileOwner != null)
            {
                flyout.CreateFlyoutItem(ViewInProfile, Strings.Gift2ViewInProfile, Icons.PersonCircle);
            }

            if (_gift.CanBeDeleted)
            {
                flyout.CreateFlyoutItem(Delete, Strings.Gift2DeleteOption, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void Pin()
        {
            await _profile.PinGiftAsync(_gift);

            // Pinning saves a hidden gift first.
            UpdateSavedInfo();
        }

        private async void Delete()
        {
            var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, Strings.Gift2DeleteText, Strings.Gift2DeleteOption, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await _clientService.SendAsync(new DeleteGift(_gift.ReceivedGiftId));
            if (response is Ok)
            {
                Hide(ContentDialogResult.Secondary);

                // The profile list drops a sold gift and a deleted one the same way.
                _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));

                ToastPopup.Show(XamlRoot, Strings.Gift2Deleted, new DelayedFileSource(_clientService, _gift.GetSticker()));
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }
        }

        private void ViewInProfile()
        {
            if (ProfileOwner is MessageSender owner)
            {
                Hide();
                _navigationService.NavigateToSender(owner, new NavigationState { { "open_gifts", true } });
            }
        }

        private void Craft()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            if (_gift.CraftDate > DateTime.Now.ToUnixTimeSeconds())
            {
                _ = MessagePopup.ShowNestedAsync(XamlRoot, string.Format(Strings.GiftCraftLaterText, Formatter.DateAt(_gift.CraftDate)), Strings.GiftCraftLaterTitle, Strings.OK);
            }
            else if (upgraded.Gift.GiftAddress.Length > 0)
            {
                _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.GiftCraftCantChooseFirst, Strings.GiftCraftCantChooseFirstTitle, Strings.OK);
            }
            else
            {
                Hide();
                _navigationService.ShowPopup(new GiftCraftPopup(_clientService, _navigationService, _gift));
            }
        }

        private void SetTheme()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                Hide();
                _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationSetTheme(upgraded.Gift));
            }
        }

        private void CopyLink()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                MessageHelper.CopyLink(_clientService, XamlRoot, new InternalLinkTypeUpgradedGift(upgraded.Gift.Name));
            }
        }

        private void Share()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                Hide();
                _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeUpgradedGift(upgraded.Gift.Name)));
            }
        }

        private async void Resell()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToUnixTimeSeconds();
            if (now < _gift.NextResaleDate)
            {
                var date = Formatter.ToLocalTime(_gift.NextResaleDate);
                var diff = date - DateTime.Now;

                string message;
                if (diff.TotalDays >= 1)
                {
                    message = Formatter.DateAt(_gift.NextResaleDate);
                    message = string.Format(Strings.Gift2ResellTimeoutDate, message);
                }
                else
                {
                    message = Locale.FormatTtl(_gift.NextResaleDate - now);
                    message = string.Format(Strings.Gift2ResellTimeout, message);
                }

                _navigationService.ShowPopup(message, Strings.Gift2ResellTimeoutTitle, Strings.OK);
            }
            else if (upgraded.Gift.ResaleParameters != null)
            {
                var confirm = await _navigationService.ShowPopupAsync(Strings.Gift2UnlistText, string.Format(Strings.Gift2UnlistTitle, upgraded.Gift.ToName()), Strings.Gift2ActionUnlist, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SetGiftResalePrice(_gift.ReceivedGiftId, null));
                    if (response is Ok)
                    {
                        upgraded.Gift.ResaleParameters = null;

                        _aggregator.Publish(new UpdateGiftUpgraded(_gift.ReceivedGiftId, _gift.ReceivedGiftId, _gift));

                        UpdateVisibility();

                        ResellButton.Glyph = Icons.TagFilled;
                        ResellButton.Content = Strings.Gift2ActionResell;

                        ToastPopup.Show(XamlRoot, string.Format(Strings.Gift2ResaleDisable, upgraded.Gift.ToName()), ToastPopupIcon.Success);
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                }
            }
            else
            {
                ChangePrice();
            }
        }

        private async void ChangePrice()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            GiftResalePrice price;

            if (upgraded.Gift.ResaleParameters is GiftResaleParameters listed)
            {
                price = listed.GramOnly
                    ? new GiftResalePriceGram(listed.GramCentCount)
                    : new GiftResalePriceStar(listed.StarCount);
            }
            else
            {
                var floor = await _resaleStarCount.Task;
                price = new GiftResalePriceStar(Math.Clamp(floor, _clientService.Options.GiftResaleStarCountMin, _clientService.Options.GiftResaleStarCountMax));
            }

            var popup = new GiftResalePricePopup(_clientService, price);

            var confirm = await popup.ShowAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var listing = upgraded.Gift.ResaleParameters == null;

            var response = await _clientService.SendAsync(new SetGiftResalePrice(_gift.ReceivedGiftId, popup.Price));
            if (response is Ok)
            {
                upgraded.Gift.ResaleParameters = popup.Parameters;

                _aggregator.Publish(new UpdateGiftUpgraded(_gift.ReceivedGiftId, _gift.ReceivedGiftId, _gift));

                UpdateVisibility();
                UpdateResalePrice(upgraded.Gift.ResaleParameters);

                ResellButton.Glyph = Icons.TagOffFilled;
                ResellButton.Content = Strings.Gift2ActionUnlist;

                if (listing)
                {
                    ToastPopup.Show(XamlRoot, string.Format(Strings.Gift2ResaleEnable, upgraded.Gift.ToName()), ToastPopupIcon.Success);
                }
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }
        }

        private void ResaleStarCountRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_owned)
            {
                ChangePrice();
            }
        }

        private void UpdateResalePrice(GiftResaleParameters parameters)
        {
            ResaleStarIcon.Visibility = parameters.GramOnly ? Visibility.Collapsed : Visibility.Visible;
            ResaleGramIcon.Visibility = parameters.GramOnly ? Visibility.Visible : Visibility.Collapsed;

            ResaleStarCount.Text = parameters.GramOnly
                ? (parameters.GramCentCount / 100d).ToString("0.##")
                : parameters.StarCount.ToString("N0");
        }

        private void Transfer()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToUnixTimeSeconds();
            if (now < _gift.NextTransferDate)
            {
                var date = Formatter.ToLocalTime(_gift.NextTransferDate);
                var diff = date - DateTime.Now;

                string message;
                if (diff.TotalDays >= 1)
                {
                    message = Formatter.DateAt(_gift.NextTransferDate);
                    message = string.Format(Strings.Gift2TransferTimeoutDate, message);
                }
                else
                {
                    message = Locale.FormatTtl(_gift.NextTransferDate - now);
                    message = string.Format(Strings.Gift2TransferTimeout, message);
                }

                _navigationService.ShowPopup(message, Strings.Gift2TransferTimeoutTitle, Strings.OK);
            }
            else
            {
                Hide();
                _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(_gift));
            }
        }

        private async void BuyResale()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var chat = await _clientService.GetChatFromMessageSenderAsync(_sendGiftTo);

            // TODO: text, isPrivate
            var response = await TransferGiftPopup.BuyAsync(_clientService, _navigationService, upgraded.Gift, chat, _sendGiftTo ?? _clientService.MyId, string.Empty.AsFormattedText(), false);
            if (response is GiftResaleResultOk)
            {
                _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));
                Hide(ContentDialogResult.Primary);

                if (chat != null)
                {
                    _navigationService.NavigateToChat(chat.Id);
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftToTitle, string.Format(Strings.BoughtResoldGiftToText, chat.Title)), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftTitle, string.Format(Strings.BoughtResoldGiftText, upgraded.Gift.ToName())), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                }

                return;
            }

            _submitted = false;

            // A raised price was confirmed or declined in the loop, and the sheet has to show it either way.
            PrimaryButtonText = GetUpgradedPrimaryButtonText(upgraded.Gift);
            UpdateResalePrice(upgraded.Gift.ResaleParameters);

            if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }
            else if (response is ErrorStarsNeeded)
            {
                Hide();
                await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(upgraded.Gift.ResaleParameters.StarCount, 0));
            }
        }

        private void From_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_gift.SenderId);
        }

        private void To_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_receiverId);
        }

        private async void FromSendGift_Click(object sender, RoutedEventArgs e)
        {
            if (!_clientService.TryGetUser(_gift.SenderId, out User user))
            {
                return;
            }

            if (!_clientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo))
            {
                fullInfo = await _clientService.SendAsync(new GetUserFullInfo(user.Id)) as UserFullInfo;
            }

            if (fullInfo != null)
            {
                Hide();
                _navigationService.ShowPopup(new GiftPopup(_clientService, _navigationService, user, fullInfo));
            }
        }

        private void Owner_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();

            if (_gift.Gift is SentGiftUpgraded upgraded && HolderOf(upgraded.Gift) is MessageSender holder)
            {
                _navigationService.NavigateToSender(holder);
            }
            else
            {
                _navigationService.NavigateToSender(_receiverId);
            }
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            if (_owned)
            {
                Transfer();
            }
            else
            {
                ShowActionLocked();
            }
        }

        private async void ShowActionLocked()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, Strings.Gift2CantDoText, Strings.Gift2CantDoTitle, Strings.OpenFragment, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                MessageHelper.OpenUrl(_clientService, _navigationService, "https://fragment.com/gift/" + upgraded.Gift.Name);
            }
        }

        private void OwnerAddress_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(upgraded.Gift.OwnerAddress);
                ClipboardEx.TrySetContent(dataPackage);

                ToastPopup.Show(XamlRoot, Strings.WalletAddressCopied, ToastPopupIcon.Copied);
            }
        }

        private void Blockchain_Click(object sender, TextUrlClickEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                MessageHelper.OpenUrl(_clientService, _navigationService, _clientService.Options.TonBlockchainExplorerUrl + upgraded.Gift.GiftAddress);
            }
        }

        private void WearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            if (_shownWearInfo || IsWorn(upgraded.Gift))
            {
                ToggleWear();
            }
            else
            {
                ShowPage(GiftPage.Wear);
            }
        }

        private async void ToggleWear()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                _submitted = false;
                return;
            }

            var gift = upgraded.Gift;
            var worn = IsWorn(gift);

            EmojiStatus status = null;

            if (worn is false)
            {
                if (HolderOf(gift) is MessageSenderChat senderChat)
                {
                    if (await CanSetChatEmojiStatusAsync(senderChat.ChatId) is false)
                    {
                        _submitted = false;
                        return;
                    }
                }
                else if (_clientService.IsPremium is false)
                {
                    _submitted = false;
                    ToastPopup.ShowFeaturePromo(_navigationService, Strings.Gift2ActionWearNeededPremium, new PremiumFeatureEmojiStatus());
                    return;
                }

                var type = ToEmojiStatusType(gift);
                if (type == null)
                {
                    _submitted = false;
                    return;
                }

                status = new EmojiStatus(type, 0);
            }

            Function function = HolderOf(gift) is MessageSenderChat ownerChat
                ? new SetChatEmojiStatus(ownerChat.ChatId, status)
                : new SetEmojiStatus(status);

            var response = await _clientService.SendAsync(function);
            if (response is Ok)
            {
                UpdateWearButton(!worn);
                ShowPage(GiftPage.Details);

                ToastPopup.Show(XamlRoot, string.Format(worn ? Strings.Gift2ActionWearOffDone : Strings.Gift2ActionWearDone, gift.ToName()), new DelayedFileSource(_clientService, gift.Model.Sticker));
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            _submitted = false;
        }

        private async Task<bool> CanSetChatEmojiStatusAsync(long chatId)
        {
            if (!_clientService.TryGetChat(chatId, out Chat chat))
            {
                return false;
            }

            var response1 = await _clientService.SendAsync(new GetChatBoostFeatures(true));
            var response2 = await _clientService.SendAsync(new GetChatBoostStatus(chatId));

            // If the level can't be read, the server has the last word.
            if (response1 is not ChatBoostFeatures features || response2 is not ChatBoostStatus status || status.Level >= features.MinEmojiStatusBoostLevel)
            {
                return true;
            }

            Hide();

            await _navigationService.ShowPopupAsync(new ChatBoostFeaturesPopup(_clientService, _navigationService, chat, status, null, features, ChatBoostFeature.EmojiStatus, features.MinEmojiStatusBoostLevel));
            await this.ShowQueuedAsync(XamlRoot);

            return false;
        }

        /// <summary>
        /// The Telegram account the gift is with: its owner, or for a gift on the blockchain the
        /// account hosting it.
        /// </summary>
        private static MessageSender HolderOf(UpgradedGift gift)
        {
            return gift.HostId ?? gift.OwnerId;
        }

        private bool IsWorn(UpgradedGift gift)
        {
            EmojiStatus status = null;

            var holder = HolderOf(gift);

            if (_clientService.TryGetUser(holder, out User user))
            {
                status = user.EmojiStatus;
            }
            else if (_clientService.TryGetChat(holder, out Chat chat))
            {
                status = chat.EmojiStatus;
            }

            return status?.Type is EmojiStatusTypeUpgradedGift upgradedGift && upgradedGift.UpgradedGiftId == gift.Id;
        }

        private static EmojiStatusTypeUpgradedGift ToEmojiStatusType(UpgradedGift gift)
        {
            if (gift.Model.Sticker.FullType is StickerFullTypeCustomEmoji model && gift.Symbol.Sticker.FullType is StickerFullTypeCustomEmoji symbol)
            {
                return new EmojiStatusTypeUpgradedGift(gift.Id, gift.Title, gift.Name, model.CustomEmojiId, symbol.CustomEmojiId, gift.Backdrop.Colors);
            }

            return null;
        }

        private void UpdateWearButton(bool worn)
        {
            WearButton.Glyph = worn ? Icons.CrownOffFilled : Icons.CrownFilled;
            WearButton.Content = worn ? Strings.Gift2ActionWearOff : Strings.Gift2ActionWear;
        }

        private void ResellButton_Click(object sender, RoutedEventArgs e)
        {
            if (_owned)
            {
                Resell();
            }
            else
            {
                Share();
            }
        }

        private void Publisher_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            NavigateToPublisher();
        }

        private void UpgradedPublisher_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPublisher();
        }

        private void NavigateToPublisher()
        {
            Hide();

            if (_gift.Gift is SentGiftRegular regular)
            {
                _navigationService.NavigateToChat(regular.Gift.PublisherChatId);
            }
            else if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                _navigationService.NavigateToChat(upgraded.Gift.PublisherChatId);
            }
        }

        private async void UpgradedValueRarity_Click(object sender, RoutedEventArgs e)
        {
            var valueInfo = await _valueInfo.Task;
            if (valueInfo != null && _gift.Gift is SentGiftUpgraded upgraded)
            {
                await UpgradedGiftValuePopup.ShowAsync(XamlRoot, _clientService, _navigationService, upgraded.Gift, valueInfo);
            }
        }

        private async void UpgradedCaptionRemove_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await _navigationService.ShowPopupAsync(Strings.Gift2RemoveDescriptionText, Strings.Gift2RemoveDescriptionTitle, string.Format(Strings.Gift2RemoveDescriptionButton.ReplaceStar(Icons.Premium), _gift.DropOriginalDetailsStarCount));
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await _clientService.SendPaymentAsync(_gift.DropOriginalDetailsStarCount, new DropGiftOriginalDetails(_gift.ReceivedGiftId, _gift.DropOriginalDetailsStarCount));
                if (response is Ok)
                {
                    _gift.DropOriginalDetailsStarCount = 0;

                    if (_gift.Gift is SentGiftUpgraded upgraded)
                    {
                        upgraded.Gift.OriginalDetails = null;
                    }

                    UpgradedTableRoot.BorderThickness = new Thickness(1);
                    UpgradedTableRoot.CornerRadius = new CornerRadius(4);

                    UpgradedCaptionRoot.Visibility = Visibility.Collapsed;

                    if (_gift.Gift is SentGiftUpgraded removed)
                    {
                        ToastPopup.Show(XamlRoot, ClientEx.ParseMarkdown(string.Format(Strings.GiftRemovedDescription, removed.Gift.ToName())), new DelayedFileSource(_clientService, removed.Gift.Model.Sticker));
                    }
                }
                else if (response is Error error)
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
                else if (response is ErrorStarsNeeded)
                {
                    Hide();
                    await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(_gift.DropOriginalDetailsStarCount, 0));
                }
            }
        }
    }
}
