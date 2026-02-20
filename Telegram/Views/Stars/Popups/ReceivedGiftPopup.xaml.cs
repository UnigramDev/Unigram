//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
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
using Telegram.Views.Gifts.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceivedGiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private readonly StarTransaction _transaction;

        private readonly string _transactionId;

        private readonly ReceivedGift _gift;
        private readonly MessageSender _receiverId;

        private readonly MessageSender _sendGiftTo;

        private TaskCompletionSource<long> _resaleStarCount;

        private TaskCompletionSource<UpgradedGiftValueInfo> _valueInfo;

        private GiftUpgradePreview _preview;
        private int _index;

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, MessageSender receiverId, MessageSender sendGiftTo)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _aggregator = clientService.Session.Resolve<IEventAggregator>();

            _gift = gift;
            _receiverId = receiverId;
            _sendGiftTo = sendGiftTo;

            if (gift.Gift is SentGiftRegular regular)
            {
                if (gift.PrepaidUpgradeHash.Length > 0 || (gift.CanBeUpgraded && IsOwned(clientService, receiverId)))
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
            DismissButtonRequestedTheme = ElementTheme.Default;
            UpgradedHeader.Visibility = Visibility.Collapsed;
            UpgradedRoot.Visibility = Visibility.Collapsed;
            MoreButton.Visibility = Visibility.Collapsed;

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
            Title.Text = Strings.Gift2TitleReceived;

            if (IsOwned(clientService, receiverId))
            {
                if (receivedGift.IsSaved)
                {
                    if (receivedGift.Date + clientService.Options.GiftSellPeriod > DateTime.Now.ToTimestamp())
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoPinned);
                        Convert.Glyph = Locale.Declension(Strings.R.Gift2ButtonSell, receivedGift.SellStarCount);
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info2Expired, gift.StarCount));
                        Convert.Visibility = Visibility.Collapsed;
                    }

                    Info.Text = receiverId is MessageSenderChat
                        ? Strings.Gift2ChannelProfileVisible3
                        : Strings.Gift2ProfileVisible4;
                }
                else
                {
                    if (receivedGift.SellStarCount > 0 && receivedGift.Date + clientService.Options.GiftSellPeriod > DateTime.Now.ToTimestamp())
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info, receivedGift.SellStarCount));
                        Convert.Glyph = Locale.Declension(Strings.R.Gift2ButtonSell, receivedGift.SellStarCount);
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info2Expired, gift.StarCount));
                        Convert.Visibility = Visibility.Collapsed;
                    }

                    Info.Text = receiverId is MessageSenderChat
                        ? Strings.Gift2ChannelProfileInvisible3
                        : Strings.Gift2ProfileInvisible4;
                }

                if (receivedGift.CanBeUpgraded)
                {
                    if (receivedGift.PrepaidUpgradeStarCount > 0)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoInFreeUpgrade);

                        PrimaryButtonText = Strings.Gift2UpgradeButtonFree;
                    }
                    else
                    {
                        PrimaryButtonText = Strings.Gift2UpgradeButtonGift;
                    }
                }
                else
                {
                    PrimaryButtonText = Strings.OK;
                }

                Info.Visibility = Visibility.Visible;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
                Convert.Visibility = Visibility.Collapsed;
                Info.Visibility = Visibility.Collapsed;

                if (string.IsNullOrEmpty(receivedGift.PrepaidUpgradeHash))
                {
                    PrimaryButtonText = Strings.OK;
                }
                else
                {
                    PrimaryButtonText = Strings.Gift2GiftAnUpgrade;
                }

                if (receivedGift.CanBeUpgraded)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Gift2ActionUpgradeOut, user.FullName(true)));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Gift2Info2OutExpired, user.FullName(true)));
                }
            }

            AnimatedPhoto.LoopCount = 0;
            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Sticker);

            StarCount.Text = gift.StarCount.ToString("N0");

            if (gift.OverallLimits != null)
            {
                Availability.Visibility = Visibility.Visible;
                Availability.Content = gift.RemainingText();
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

            if (receivedGift.Text?.Text.Length > 0)
            {
                TableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                TableRoot.CornerRadius = new CornerRadius(4, 4, 0, 0);

                CaptionRoot.Visibility = Visibility.Visible;
                Caption.SetText(clientService, receivedGift.Text);
            }
        }

        private void InitializeUpgraded(IClientService clientService, ReceivedGift receivedGift, UpgradedGift gift)
        {
            _resaleStarCount = new();
            _clientService.Send(new GetAvailableGifts(), result =>
            {
                if (result is not AvailableGifts availableGifts)
                {
                    _resaleStarCount.TrySetResult(0);
                    return;
                }

                var availableGift = availableGifts.Gifts.FirstOrDefault(x => x.Title == gift.Title);
                if (availableGift == null)
                {
                    _resaleStarCount.TrySetResult(0);
                    return;
                }

                _clientService.Send(new SearchGiftsForResale(availableGift.Gift.Id, new GiftForResaleOrderPrice(), false, Array.Empty<UpgradedGiftAttributeId>(), string.Empty, 1), result =>
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
            });

            DismissButtonRequestedTheme = ElementTheme.Dark;
            Header.Visibility = Visibility.Collapsed;
            RegularRoot.Visibility = Visibility.Collapsed;
            MoreButton.Visibility = Visibility.Visible;

            UpgradedHeader.Update(clientService, gift);
            UpgradedAnimatedPhoto.Source = DelayedFileSource.FromSticker(clientService, gift.Model.Sticker);
            UpgradedTitle.Text = gift.Title;

            if (gift.IsCrafted)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = Strings.GiftCrafted;
            }

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

            if (clientService.TryGetUser(gift.OwnerId, out User user))
            {
                UpgradedFromPhoto.Source = ProfilePictureSource.User(clientService, user);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(gift.OwnerId, out Chat chat))
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

            UpgradedModel.Text = gift.Model.Name;
            UpgradedModelRarity.Glyph = gift.Model.Rarity.ToText();
            gift.Model.Rarity.ToColor(UpgradedModelRarity);
            UpgradedBackdrop.Text = gift.Backdrop.Name;
            UpgradedBackdropRarity.Glyph = gift.Backdrop.Rarity.ToText();
            gift.Backdrop.Rarity.ToColor(UpgradedBackdropRarity);
            UpgradedSymbol.Text = gift.Symbol.Name;
            UpgradedSymbolRarity.Glyph = gift.Symbol.Rarity.ToText();
            gift.Symbol.Rarity.ToColor(UpgradedSymbolRarity);

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

                FormattedText text = null;

                if (gift.OriginalDetails.Text.Text.Length > 0)
                {
                    if (senderName.Length > 0 && receiverName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsComment, senderText, receiverText, dateText, gift.OriginalDetails.Text);
                    }
                    else if (senderName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelfComment, senderText, dateText, gift.OriginalDetails.Text);
                    }
                    else if (receiverName.Length > 0)
                    {
                        text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSenderComment, receiverText, dateText, gift.OriginalDetails.Text);
                    }
                }
                else if (senderName.Length > 0 && receiverName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetails, senderText, receiverText, dateText);
                }
                else if (senderName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsSelf, senderText, dateText);
                }
                else if (receiverName.Length > 0)
                {
                    text = ClientEx.Format(Strings.Gift2AttributeOriginalDetailsNoSender, receiverText, dateText);
                }

                UpgradedCaption.SetText(clientService, text);
            }

            if (IsOwned(clientService, gift.OwnerId) && receivedGift.ReceivedGiftId.Length > 0)
            {
                if (receivedGift.IsSaved)
                {
                    Info.Text = gift.OwnerId is MessageSenderUser
                        ? Strings.Gift2ProfileVisible4
                        : Strings.Gift2ChannelProfileVisible3;
                }
                else
                {
                    Info.Text = gift.OwnerId is MessageSenderUser
                        ? Strings.Gift2ProfileInvisible4
                        : Strings.Gift2ChannelProfileInvisible3;
                }

                UpgradedButtons.Visibility = Visibility.Visible;
                UpgradedCaptionRemove.Visibility = receivedGift.DropOriginalDetailsStarCount > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (gift.ResaleParameters != null)
                {
                    ResaleStarCountRoot.Visibility = Visibility.Visible;
                    ResaleStarCount.Text = gift.ResaleParameters.StarCount.ToString("N0");

                    ResellButton.Glyph = Icons.TagOffFilled;
                    ResellButton.Content = Strings.Gift2ActionUnlist;
                }
                else
                {
                    ResellButton.Glyph = Icons.TagFilled;
                    ResellButton.Content = Strings.Gift2ActionResell;
                }

                if (user != null)
                {
                    if (user.EmojiStatus?.Type is EmojiStatusTypeUpgradedGift upgradedGift && upgradedGift.UpgradedGiftId == gift.Id)
                    {
                        WearButton.Glyph = Icons.CrownOffFilled;
                        WearButton.Content = Strings.Gift2Unwear;
                    }
                    else
                    {
                        WearButton.Glyph = Icons.CrownFilled;
                        WearButton.Content = Strings.Gift2Wear;
                    }
                }

                PrimaryButtonText = Strings.OK;
            }
            else
            {
                Info.Visibility = Visibility.Collapsed;

                if (gift.ResaleParameters != null)
                {
                    ResaleStarCountRoot.Visibility = Visibility.Visible;
                    ResaleStarCount.Text = gift.ResaleParameters.StarCount.ToString("N0");

                    PrimaryButtonText = Locale.Declension(Strings.R.ResellGiftBuy, gift.ResaleParameters.StarCount).ReplaceStar(Icons.Premium);
                }
                else
                {
                    PrimaryButtonText = Strings.OK;
                }
            }
        }

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, Gift gift)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            DismissButtonRequestedTheme = ElementTheme.Default;
            UpgradedHeader.Visibility = Visibility.Collapsed;
            UpgradedRoot.Visibility = Visibility.Collapsed;

            From.Visibility = Visibility.Collapsed;

            Title.Text = Strings.Gift2SoldOutSheetTitle;
            Subtitle.Text = Strings.Gift2SoldOutSheetSubtitle;
            Subtitle.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

            FirstSale.Visibility = Visibility.Visible;
            LastSale.Visibility = Visibility.Visible;

            FirstSale.Content = Formatter.DateAt(gift.FirstSendDate);
            LastSale.Content = Formatter.DateAt(gift.LastSendDate);

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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            if (IsOwned(_clientService, _receiverId))
            {
                if (_gift?.Gift is SentGiftRegular && _gift.CanBeUpgraded && _upgradeCollapsed)
                {
                    ShowHideUpgrade(true);
                }
                else if (_gift?.Gift is SentGiftRegular && (_gift.PrepaidUpgradeStarCount > 0 || !_upgradeCollapsed))
                {
                    Upgrade2();
                }
                else if (_gift?.Gift is SentGiftUpgraded { Gift.ResaleParameters: not null } && !IsOwned(_clientService, _receiverId))
                {
                    BuyResale();
                }
                else
                {
                    Hide(ContentDialogResult.Primary);
                }
            }
            else if (_gift?.PrepaidUpgradeHash.Length > 0)
            {
                if (_upgradeCollapsed)
                {
                    ShowHideUpgrade(true);
                }
                else
                {
                    Upgrade2();
                }
            }
            else if (_gift?.Gift is SentGiftUpgraded { Gift.ResaleParameters: not null })
            {
                BuyResale();
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
            if (_gift.Gift is not SentGiftRegular regular)
            {
                return;
            }

            IsPrimaryButtonPending = true;

            var starCount = _gift.PrepaidUpgradeStarCount > 0 || _gift.IsUpgradeSeparate ? 0 : regular.Gift.UpgradeStarCount;

            Function function;
            if (IsOwned(_clientService, _receiverId))
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

                DismissButtonRequestedTheme = ElementTheme.Dark;
                UpgradedHeader.Visibility = Visibility.Visible;
                UpgradedRoot.Visibility = Visibility.Visible;

                DetailRoot.Visibility = Visibility.Visible;
                UpgradeRoot.Visibility = Visibility.Collapsed;

                InitializeUpgraded(_clientService, _gift, result.Gift);
            }
            else if (response is Ok)
            {
                Hide();
                ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsGiftUpgradeCompleted, string.Format(Strings.StarsGiftUpgradeCompletedText, _clientService.GetTitle(_receiverId, true))), ToastPopupIcon.Gift);
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

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (_gift?.Gift is SentGiftRegular regular)
            {
                var expiration = Formatter.ToLocalTime(_gift.Date + _clientService.Options.GiftSellPeriod);
                var diff = expiration - DateTime.Now;

                var message = Locale.Declension(Strings.R.Gift2ConvertText2, (long)diff.TotalDays, _clientService.GetTitle(_gift.SenderId), Locale.Declension(Strings.R.StarsCount, regular.Gift.StarCount));

                var confirm = await MessagePopup.ShowAsync(XamlRoot, target: null, message, Strings.Gift2ConvertTitle, Strings.Gift2ConvertButton, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SellGift(_gift.ReceivedGiftId));
                    if (response is Ok)
                    {
                        Hide(ContentDialogResult.Secondary);

                        _aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));
                        _navigationService.Navigate(typeof(StarsPage));

                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2ConvertedTitle, Locale.Declension(Strings.R.Gift2Converted, regular.Gift.StarCount)), ToastPopupIcon.StarsTopup);
                    }
                }
            }
        }

        private async void Visibility_Click(object sender, TextUrlClickEventArgs e)
        {
            var response = await _clientService.SendAsync(new ToggleGiftIsSaved(_gift.ReceivedGiftId, !_gift.IsSaved));
            if (response is Ok)
            {
                _gift.IsSaved = !_gift.IsSaved;
                _aggregator.Publish(new UpdateGiftIsSaved(_gift.ReceivedGiftId, _gift.IsSaved));

                if (_gift.Gift is SentGiftRegular regular)
                {
                    InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
                }

                if (_gift.IsSaved)
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

        private bool _upgradeCollapsed = true;

        private void ShowHideUpgrade(bool show)
        {
            if (_upgradeCollapsed != show)
            {
                return;
            }

            _upgradeCollapsed = !show;
            _submitted = false;

            if (IsOwned(_clientService, _receiverId))
            {
                UpgradedTitle.Text = Strings.Gift2UpgradeTitle;
                UpgradedSubtitle.Text = Strings.Gift2UpgradeText;
            }
            else
            {
                UpgradedTitle.Text = Strings.Gift2PrepayUpgradeTitle;
                UpgradedSubtitle.Text = string.Format(Strings.Gift2PrepayUpgradeText, _clientService.GetTitle(_receiverId, true));

                UpgradeText1.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature1Text, _clientService.GetTitle(_receiverId, true));
                UpgradeText2.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature2Text, _clientService.GetTitle(_receiverId, true));
                UpgradeText3.Badge = string.Format(Strings.Gift2PrepayUpgradeFeature3Text, _clientService.GetTitle(_receiverId, true));

                KeepOriginalDetails.Visibility = Visibility.Collapsed;
            }

            DismissButtonRequestedTheme = show ? ElementTheme.Dark : ElementTheme.Default;
            Header.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradedHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            DetailRoot.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradeRoot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            if (show)
            {
                UpdateGift();

                if (_gift.Gift is SentGiftRegular regular)
                {
                    PrimaryButtonText = _gift.PrepaidUpgradeHash.Length > 0
                        ? string.Format(Strings.Gift2PrepayUpgradeButton.ReplaceStar(Icons.Premium), regular.Gift.UpgradeStarCount)
                        : _gift.PrepaidUpgradeStarCount > 0
                        ? Strings.Gift2UpgradeButtonFree
                        : string.Format(Strings.Gift2UpgradeButton.ReplaceStar(Icons.Premium), regular.Gift.UpgradeStarCount);
                }
            }
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
                foreach (var item in preview.Models.Reverse())
                {
                    _clientService.DownloadFile(item.Sticker.StickerValue.Id, 32);
                }

                foreach (var item in preview.Symbols.Reverse())
                {
                    _clientService.DownloadFile(item.Sticker.StickerValue.Id, 31);
                }

                _preview = preview;

                if (_upgradeCollapsed is false)
                {
                    UpdateGift();
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

            var pattern = new DelayedFileSource(_clientService, symbol.Sticker);
            var centerColor = backdrop.Colors.CenterColor.ToColor();
            var edgeColor = backdrop.Colors.EdgeColor.ToColor();
            var symbolColor = backdrop.Colors.SymbolColor.ToColor();

            UpgradedHeader.Update(pattern, centerColor, edgeColor, symbolColor);
            UpgradedAnimatedPhoto.Source = new DelayedFileSource(_clientService, model.Sticker);
        }

        protected override void OnDismissButtonClick()
        {
            if (_upgradeCollapsed)
            {
                base.OnDismissButtonClick();
            }
            else
            {
                ShowHideUpgrade(false);

                if (_gift.Gift is SentGiftRegular regular)
                {
                    InitializeRegular(_clientService, _gift, regular.Gift, _receiverId);
                }
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);
            flyout.CreateFlyoutItem(Share, Strings.ShareFile, Icons.Share);

            if (_gift.Gift is SentGiftUpgraded upgraded && upgraded.Gift.OwnerId.AreTheSame(_clientService.MyId))
            {
                if (upgraded.Gift.CraftProbabilityPerMille != 0)
                {
                    // TODO: Icon
                    flyout.CreateFlyoutItem(Craft, Strings.GiftCraft, Icons.Hamburger);
                }

                if (upgraded.Gift.ResaleParameters != null)
                {
                    flyout.CreateFlyoutItem(ChangePrice, Strings.Gift2ChangePrice, Icons.Tag);
                }

                if (upgraded.Gift.IsThemeAvailable)
                {
                    flyout.CreateFlyoutItem(SetTheme, Strings.GiftThemesSetIn, Icons.PaintBrush);
                }
            }

            if (_gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(Transfer, Strings.Gift2TransferOption, Icons.Replace);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Craft()
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
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

            var now = DateTime.Now.ToTimestamp();
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

                        ResaleStarCountRoot.Visibility = Visibility.Collapsed;

                        ResellButton.Glyph = Icons.TagFilled;
                        ResellButton.Content = Strings.Gift2ActionResell;
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

            var resaleStarCount = upgraded.Gift.ResaleParameters?.StarCount ?? 0;
            if (resaleStarCount == 0)
            {
                resaleStarCount = await _resaleStarCount.Task;
            }

            var popup = new InputTeachingTip(InputPopupType.Stars);
            popup.Value = Math.Clamp(resaleStarCount, _clientService.Options.GiftResaleStarCountMin, _clientService.Options.GiftResaleStarCountMax);
            //popup.Minimum = _clientService.Options.GiftResaleStarCountMin;
            popup.Maximum = _clientService.Options.GiftResaleStarCountMax;

            popup.Title = Strings.ResellGiftTitle;
            popup.Header = Strings.ResellGiftPriceTitle;
            popup.ActionButtonContent = Strings.ResellGiftButton;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Center;
            popup.IsLightDismissEnabled = false;
            popup.ShouldConstrainToRootBounds = true;

            popup.ValueChanged += (s, args) =>
            {
                if (args.Value < _clientService.Options.GiftResaleStarCountMin)
                {
                    args.Footer = Locale.Declension(Strings.R.ResellGiftInfoMin, _clientService.Options.GiftResaleStarCountMin);
                }
                else
                {
                    var xtr = args.Value / 1000d;
                    var usd = (long)(xtr * _clientService.Options.ThousandStarToUsdRate);
                    var stars = (long)(xtr * _clientService.Options.GiftResaleStarEarningsPerMille);

                    args.Footer = string.Format("{0} ~{1}", Locale.Declension(Strings.R.ResellGiftInfo, stars), Formatter.FormatAmount(usd, "USD"));
                }
            };

            popup.Validating += (s, args) =>
            {
                if (args.Value < _clientService.Options.GiftResaleStarCountMin)
                {
                    _navigationService.ShowToast(Locale.Declension(Strings.R.ResellGiftInfoMin, _clientService.Options.GiftResaleStarCountMin), ToastPopupIcon.Info);
                    args.Cancel = true;
                }
            };

            var confirm = await popup.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await _clientService.SendAsync(new SetGiftResalePrice(_gift.ReceivedGiftId, new GiftResalePriceStar(popup.Value)));
            if (response is Ok)
            {
                upgraded.Gift.ResaleParameters = new GiftResaleParameters(popup.Value, 0, false);

                _aggregator.Publish(new UpdateGiftUpgraded(_gift.ReceivedGiftId, _gift.ReceivedGiftId, _gift));

                ResaleStarCountRoot.Visibility = Visibility.Visible;
                ResaleStarCount.Text = upgraded.Gift.ResaleParameters.StarCount.ToString("N0");

                ResellButton.Glyph = Icons.TagOffFilled;
                ResellButton.Content = Strings.Gift2ActionUnlist;
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }
        }

        private void Transfer()
        {
            if (_gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            var now = DateTime.Now.ToTimestamp();
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

            var confirm = await TransferGiftPopup.ShowAsync(XamlRoot, _clientService, _gift, chat, true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await _clientService.SendPaymentAsync(upgraded.Gift.ResaleParameters.StarCount, new SendResoldGift(upgraded.Gift.Name, _sendGiftTo ?? _clientService.MyId, new GiftResalePriceStar(upgraded.Gift.ResaleParameters.StarCount)));
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
                }
                else if (response is Error error)
                {
                    _submitted = false;
                    ToastPopup.ShowError(XamlRoot, error);
                }
                else if (response is ErrorStarsNeeded)
                {
                    Hide();
                    await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(upgraded.Gift.ResaleParameters.StarCount, 0));
                }
            }
            else
            {
                _submitted = false;
            }
        }

        private void From_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_gift.SenderId);
        }

        private void Owner_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.NavigateToSender(_receiverId);
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            Transfer();
        }

        private void WearButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ResellButton_Click(object sender, RoutedEventArgs e)
        {
            Resell();
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
