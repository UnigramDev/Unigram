//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
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
using Telegram.Views.Popups;

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
        private readonly MessageSender _senderId;

        private GiftUpgradePreview _preview;
        private int _index;

        public ReceivedGiftPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, MessageSender receiverId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _aggregator = TypeResolver.Current.Resolve<IEventAggregator>(clientService.SessionId);

            _gift = gift;
            _senderId = receiverId;

            if (gift.Gift is SentGiftRegular regular)
            {
                if (gift.CanBeUpgraded && IsOwned(clientService, receiverId))
                {
                    InitializeGift();
                }

                InitializeRegular(clientService, gift, regular.Gift, receiverId);
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                InitializeUpgraded(clientService, gift, upgraded.Gift, receiverId);
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
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(receivedGift.SenderId, out Chat chat))
            {
                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
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

                    Info.Text = Strings.Gift2ProfileVisible;
                    PurchaseText.Text = Strings.Gift2ProfileMakeInvisible;
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

                    Info.Text = Strings.Gift2ProfileInvisible;
                    PurchaseText.Text = Strings.Gift2ProfileMakeVisible;
                }

                if (receivedGift.CanBeUpgraded && receivedGift.PrepaidUpgradeStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoInFreeUpgrade);

                    PurchaseText.Text = Strings.Gift2UpgradeButtonFree;
                }

                Info.Visibility = Visibility.Visible;

                VisibilityRoot.Visibility = Visibility.Visible;
                VisibilityText.Text = receivedGift.IsSaved
                    ? Strings.Gift2Visible
                    : Strings.Gift2Invisible;
                Toggle.Glyph = receivedGift.IsSaved
                    ? Strings.Gift2VisibleHide
                    : Strings.Gift2InvisibleShow;
            }
            else
            {
                Subtitle.Visibility = Visibility.Collapsed;
                Convert.Visibility = Visibility.Collapsed;
                Status.Visibility = Visibility.Collapsed;
                Info.Visibility = Visibility.Collapsed;

                PurchaseText.Text = Strings.OK;

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

            if (gift.TotalCount > 0)
            {
                Availability.Visibility = Visibility.Visible;
                Availability.Content = gift.RemainingText();
            }

            if (receivedGift.CanBeUpgraded && IsOwned(clientService, receiverId))
            {
                Status.Visibility = Visibility.Visible;
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

        private void InitializeUpgraded(IClientService clientService, ReceivedGift receivedGift, UpgradedGift gift, MessageSender receiverId)
        {
            DismissButtonRequestedTheme = ElementTheme.Dark;
            Header.Visibility = Visibility.Collapsed;
            RegularRoot.Visibility = Visibility.Collapsed;
            MoreButton.Visibility = Visibility.Visible;

            var source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker);
            var centerColor = gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = gift.Backdrop.Colors.EdgeColor.ToColor();

            UpgradedHeader.Update(source, centerColor, edgeColor);
            UpgradedAnimatedPhoto.Source = DelayedFileSource.FromSticker(clientService, gift.Model.Sticker);
            UpgradedTitle.Text = gift.Title;
            UpgradedSubtitle.Text = Locale.Declension(Strings.R.Gift2CollectionNumber, gift.Number);

            if (clientService.TryGetUser(gift.OwnerId, out User user))
            {
                UpgradedFromPhoto.SetUser(clientService, user, 24);
                UpgradedFromPhoto.Visibility = Visibility.Visible;
                UpgradedFromTitle.Text = user.FullName();
            }
            else if (clientService.TryGetChat(gift.OwnerId, out Chat chat))
            {
                UpgradedFromPhoto.SetChat(clientService, chat, 24);
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
            UpgradedModelRarity.Glyph = (gift.Model.RarityPerMille / 10d).ToString("0.##") + "%";
            UpgradedBackdrop.Text = gift.Backdrop.Name;
            UpgradedBackdropRarity.Glyph = (gift.Backdrop.RarityPerMille / 10d).ToString("0.##") + "%";
            UpgradedSymbol.Text = gift.Symbol.Name;
            UpgradedSymbolRarity.Glyph = (gift.Symbol.RarityPerMille / 10d).ToString("0.##") + "%";

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
                    Info.Text = Strings.Gift2ProfileVisible;
                    PurchaseText.Text = Strings.Gift2ProfileMakeInvisible;
                }
                else
                {
                    Info.Text = Strings.Gift2ProfileInvisible;
                    PurchaseText.Text = Strings.Gift2ProfileMakeVisible;
                }
            }
            else
            {
                Info.Visibility = Visibility.Collapsed;
                PurchaseText.Text = Strings.OK;
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

            PurchaseText.Text = Strings.OK;
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            if (_gift.Gift is SentGiftRegular && (_gift.PrepaidUpgradeStarCount > 0 || !_upgradeCollapsed))
            {
                Upgrade2();
            }
            else
            {
                Hide(ContentDialogResult.Primary);
                Toggle_Click(sender, e);
            }
        }

        private bool _submitted;

        private async void Upgrade2()
        {
            if (_gift.Gift is not SentGiftRegular regular)
            {
                return;
            }

            PurchaseRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(PurchaseText);
            var visual2 = ElementComposition.GetElementVisual(PurchaseRing);

            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseText, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PurchaseRing, true);

            var translate1 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate1.InsertKeyFrame(0, 0);
            translate1.InsertKeyFrame(1, -32);

            var translate2 = visual1.Compositor.CreateScalarKeyFrameAnimation();
            translate2.InsertKeyFrame(0, 32);
            translate2.InsertKeyFrame(1, 0);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

            //await Task.Delay(2000);

            var response = await _clientService.SendAsync(new UpgradeGift(_gift.ReceivedGiftId, KeepOriginalDetails.IsChecked is true, _gift.PrepaidUpgradeStarCount > 0 ? 0 : regular.Gift.UpgradeStarCount));
            if (response is UpgradeGiftResult result)
            {
                var id = _gift.ReceivedGiftId;

                _gift.ReceivedGiftId = result.ReceivedGiftId;
                _gift.ExportDate = result.ExportDate;
                _gift.TransferStarCount = result.TransferStarCount;
                _gift.CanBeTransferred = result.CanBeTransferred;
                _gift.IsSaved = result.IsSaved;
                _gift.Gift = new SentGiftUpgraded(result.Gift);

                _aggregator.Publish(new UpdateGiftUpgraded(id, _gift));

                UpgradedAnimatedPhoto.LoopCompleted -= OnLoopCompleted;

                DismissButtonRequestedTheme = ElementTheme.Dark;
                UpgradedHeader.Visibility = Visibility.Visible;
                UpgradedRoot.Visibility = Visibility.Visible;

                DetailRoot.Visibility = Visibility.Visible;
                UpgradeRoot.Visibility = Visibility.Collapsed;

                InitializeUpgraded(_clientService, _gift, result.Gift, _senderId);
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            _submitted = false;

            translate1.InsertKeyFrame(0, 32);
            translate1.InsertKeyFrame(1, 0);

            translate2.InsertKeyFrame(0, 0);
            translate2.InsertKeyFrame(1, -32);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);

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

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            var response = await _clientService.SendAsync(new ToggleGiftIsSaved(_gift.ReceivedGiftId, !_gift.IsSaved));
            if (response is Ok)
            {
                _gift.IsSaved = !_gift.IsSaved;
                _aggregator.Publish(new UpdateGiftIsSaved(_gift.ReceivedGiftId, _gift.IsSaved));

                if (_gift.Gift is SentGiftRegular regular)
                {
                    InitializeRegular(_clientService, _gift, regular.Gift, _senderId);
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

        private void UpgradedModelRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedModelRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Model.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
            }
        }

        private void UpgradedBackdropRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedBackdropRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Backdrop.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
            }
        }

        private void UpgradedSymbolRarity_Click(object sender, RoutedEventArgs e)
        {
            if (_gift.Gift is SentGiftUpgraded upgraded)
            {
                ToastPopup.Show(UpgradedSymbolRarity, string.Format(Strings.Gift2RarityHint, (upgraded.Gift.Symbol.RarityPerMille / 10d).ToString("0.##") + "%"), TeachingTipPlacementMode.Top);
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


            DismissButtonRequestedTheme = show ? ElementTheme.Dark : ElementTheme.Default;
            Header.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradedHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            DetailRoot.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpgradeRoot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            UpgradedTitle.Text = Strings.Gift2UpgradeTitle;
            UpgradedSubtitle.Text = Strings.Gift2UpgradeText;

            if (show)
            {
                UpdateGift();

                if (_gift.Gift is SentGiftRegular regular)
                {
                    PurchaseText.Text = _gift.PrepaidUpgradeStarCount > 0
                        ? Strings.Gift2UpgradeButtonFree
                        : string.Format(Strings.Gift2UpgradeButton, regular.Gift.UpgradeStarCount).Replace("\u2B50", Icons.Premium);
                }
            }
        }

        private void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            ShowHideUpgrade(true);
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

            UpgradedAnimatedPhoto.Source = new DelayedFileSource(_clientService, model.Sticker);
            UpgradedHeader.Update(pattern, centerColor, edgeColor);
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
                    InitializeRegular(_clientService, _gift, regular.Gift, _senderId);
                }
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);
            flyout.CreateFlyoutItem(Share, Strings.ShareFile, Icons.Share);

            if (_gift.CanBeTransferred)
            {
                flyout.CreateFlyoutItem(Transfer, Strings.Gift2TransferOption, Icons.Link);
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
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

        private void Transfer()
        {
            Hide();
            _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationTransferGift(_gift));
        }
    }
}
