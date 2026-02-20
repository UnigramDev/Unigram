//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Stars;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class SendGiftPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly Gift _gift;

        private AvailableGift _giftForResale;

        private readonly PremiumGiftPaymentOption _option;

        private readonly MessageSender _receiverId;

        public SendGiftPopup(IClientService clientService, INavigationService navigationService, Gift gift, MessageSender receiverId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _gift = gift;
            _receiverId = receiverId;

            base.Title = Strings.Gift2Title;

            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var content = new MessageGift(gift, clientService.MyId, _receiverId, string.Empty, string.Empty.AsFormattedText(), 0, gift.DefaultSellStarCount, 0, false, false, false, false, false, false, false, false, false, string.Empty, string.Empty);
            var message = new Message(0, new MessageSenderUser(clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, content, null);

            var settings = clientService.Session.Resolve<ISettingsService>();

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, delegato, chat, null, null, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            var emoji = EmojiDrawerViewModel.Create(clientService.Session);
            EmojiPanel.DataContext = emoji;
            CaptionInput.DataContext = emoji;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.MaxLength = (int)clientService.Options.GiftTextLengthMax;
            CaptionInput.PlaceholderText = Strings.Gift2Message;
            CaptionInput.AllowedEntities = FormattedTextEntity.Bold
                | FormattedTextEntity.Italic
                | FormattedTextEntity.Underline
                | FormattedTextEntity.Strikethrough
                | FormattedTextEntity.Spoiler
                | FormattedTextEntity.CustomEmoji;

            CaptionInfo.Visibility = Visibility.Collapsed;

            if (receiverId.IsUser(clientService.Options.MyId))
            {
                HideMyName.Content = Strings.Gift2HideSelf;
                HideMyNameInfo.Text = Strings.Gift2HideSelfInfo;

                UpgradeableRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                HideMyNameInfo.Text = string.Format(Strings.Gift2HideInfo, _clientService.GetTitle(receiverId));

                if (gift.UpgradeStarCount > 0)
                {
                    UpgradeableInfo.Text = string.Format(Strings.Gift2UpgradeInfo, _clientService.GetTitle(receiverId));

                    var text = string.Format(Strings.Gift2Upgrade, gift.UpgradeStarCount);
                    var index = text.IndexOf("\u2B50\uFE0F");

                    if (index != -1)
                    {
                        UpgradeableTextPart1.Text = text.Substring(0, index);
                        UpgradeableTextPart2.Text = text.Substring(index + 2);
                    }
                }
                else
                {
                    UpgradeableRoot.Visibility = Visibility.Collapsed;
                }
            }

            if (clientService.TryGetChat(gift.PublisherChatId, out Chat publisherChat)
                && clientService.TryGetSupergroup(publisherChat, out Supergroup publisher)
                && publisher.HasActiveUsername(out string username))
            {
                Publisher.Visibility = Visibility.Visible;
                TextBlockHelper.SetMarkdown(PublisherLabel, string.Format(Strings.Gift2ActionReleasedBy, $"@{username}"));
            }
            else
            {
                Publisher.Visibility = Visibility.Collapsed;
            }

            if (gift.OverallLimits != null)
            {
                LimitedRoot.Visibility = Visibility.Visible;

                PrevLimit.Text = PrevLimitAbove.Text = Locale.Declension(Strings.R.Gift2AvailabilitySold, gift.OverallLimits.TotalCount - gift.OverallLimits.RemainingCount);
                NextLimit.Text = NextLimitBelow.Text = Locale.Declension(Strings.R.Gift2AvailabilityLeft, gift.OverallLimits.RemainingCount);
            }

            PrimaryButtonText = Locale.Declension(Strings.R.Gift2Send, gift.StarCount).ReplaceStar(Icons.Premium);

            InitializeGiftsForResale();
        }

        private async void InitializeGiftsForResale()
        {
            var response = await _clientService.SendAsync(new SearchGiftsForResale(_gift.Id, new GiftForResaleOrderPrice(), false, Array.Empty<UpgradedGiftAttributeId>(), string.Empty, 1));
            if (response is GiftsForResale gifts && gifts.Gifts.Count > 0)
            {
                _giftForResale = new AvailableGift(_gift, gifts.TotalCount, 0, gifts.Gifts[0].Gift.Title);

                ResaleButton.Visibility = Visibility.Visible;
                TextBlockHelper.SetMarkdown(Resale, string.Format("{0} **{1}**", Strings.Gift2AvailableForResale, gifts.TotalCount));
            }
        }

        public SendGiftPopup(IClientService clientService, INavigationService navigationService, PremiumGiftPaymentOption option, long userId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _option = option;
            _receiverId = new MessageSenderUser(userId);

            base.Title = Strings.Gift2Title;

            clientService.TryGetChatFromUser(clientService.Options.MyId, out Chat chat);

            var content = new MessageGiftedPremium(_clientService.Options.MyId, userId, string.Empty.AsFormattedText(), _option.Currency, _option.Amount, string.Empty, 0, _option.MonthCount, 0, _option.Sticker);
            var message = new Message(0, new MessageSenderUser(clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, content, null);

            var settings = clientService.Session.Resolve<ISettingsService>();

            var delegato = new ChatMessageDelegate(clientService, settings, chat);
            var viewModel = new MessageViewModel(clientService, delegato, chat, null, null, message, true);

            BackgroundControl.Update(clientService, null);
            Message.UpdateMessage(viewModel);

            var emoji = EmojiDrawerViewModel.Create(clientService.Session);
            EmojiPanel.DataContext = emoji;
            CaptionInput.DataContext = emoji;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.MaxLength = (int)clientService.Options.GiftTextLengthMax;
            CaptionInput.PlaceholderText = Strings.Gift2MessageOptional;
            CaptionInput.AllowedEntities = FormattedTextEntity.Bold
                | FormattedTextEntity.Italic
                | FormattedTextEntity.Underline
                | FormattedTextEntity.Strikethrough
                | FormattedTextEntity.Spoiler
                | FormattedTextEntity.CustomEmoji;

            HideMyNameRoot.Visibility = Visibility.Collapsed;
            UpgradeableRoot.Visibility = Visibility.Collapsed;

            if (clientService.TryGetUser(userId, out User user))
            {
                CaptionInfo.Text = string.Format(Strings.Gift2MessagePremiumInfo, user.FirstName);
            }

            PrimaryButtonText = string.Format(Strings.Gift2SendPremium, Formatter.FormatAmount(option.Amount, option.Currency));
        }

        private void OnTextChanged(object sender, RoutedEventArgs e)
        {
            var text = CaptionInput.GetFormattedText();

            MessageContent content;
            if (_gift != null)
            {
                content = new MessageGift(_gift, _clientService.MyId, _receiverId, string.Empty, text, 0, _gift.DefaultSellStarCount, Upgradeable.IsChecked is true ? _gift.UpgradeStarCount : 0, false, false, false, false, false, false, false, false, false, string.Empty, string.Empty);

                PrimaryButtonText = Locale.Declension(Strings.R.Gift2Send, _gift.StarCount + (Upgradeable.IsChecked is true ? _gift.UpgradeStarCount : 0)).ReplaceStar(Icons.Premium);
            }
            else if (_option != null && _receiverId is MessageSenderUser user)
            {
                content = new MessageGiftedPremium(_clientService.Options.MyId, user.UserId, text, _option.Currency, _option.Amount, string.Empty, 0, _option.MonthCount, 0, _option.Sticker);

                PrimaryButtonText = string.Format(Strings.Gift2SendPremium, Formatter.FormatAmount(_option.Amount, _option.Currency));
            }
            else
            {
                return;
            }

            _clientService.TryGetChatFromUser(_clientService.Options.MyId, out Chat chat);
            var message = new Message(0, new MessageSenderUser(_clientService.Options.MyId), 0, null, null, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, content, null);

            var settings = _clientService.Session.Resolve<ISettingsService>();

            var delegato = new ChatMessageDelegate(_clientService, settings, chat);
            var viewModel = new MessageViewModel(_clientService, delegato, chat, null, null, message, true);

            Message.UpdateMessage(viewModel);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private bool _submitted;
        private bool _completed;

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = !_completed;

            if (_submitted)
            {
                return;
            }

            _submitted = true;
            IsPrimaryButtonPending = true;

            var result = await SubmitAsync();
            if (result != PayResult.Failed)
            {
                _completed = true;
                Hide(result == PayResult.Succeeded
                    ? ContentDialogResult.Primary
                    : ContentDialogResult.Secondary);

                if (result == PayResult.StarsNeeded)
                {
                    await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(_gift.StarCount, 0));
                }

                return;
            }

            _submitted = false;
            IsPrimaryButtonPending = false;

            //Hide();
            //ViewModel.Submit();
        }

        private Task<PayResult> SubmitAsync()
        {
            if (_gift != null)
            {
                return SubmitGiftAsync();
            }
            else if (_option != null)
            {
                return SubmitGiftCodeAsync();
            }

            return Task.FromResult(PayResult.Failed);
        }

        public Task<PayResult> SubmitGiftCodeAsync()
        {
            var text = CaptionInput.GetFormattedText();
            var user = _receiverId as MessageSenderUser;

            _navigationService.NavigateToInvoice(new InputInvoiceTelegram(new TelegramPaymentPurposePremiumGift(_option.Currency, _option.Amount, user.UserId, _option.MonthCount, text)));
            return Task.FromResult(PayResult.Succeeded);
        }

        public async Task<PayResult> SubmitGiftAsync()
        {
            var text = CaptionInput.GetFormattedText();

            var response = await _clientService.SendPaymentAsync(_gift.StarCount, new SendGift(_gift.Id, _receiverId, text, HideMyName.IsChecked is true, Upgradeable.IsChecked is true));
            if (response is Ok result)
            {
                //var user = ClientService.GetUser(PaymentForm.SellerBotUserId);
                //var extended = Locale.Declension(Strings.R.StarsPurchaseCompletedInfo, stars.StarCount, PaymentForm.ProductInfo.Title, user.FullName());

                //var message = Strings.StarsPurchaseCompleted + Environment.NewLine + extended;
                //var entity = new TextEntity(0, Strings.StarsPurchaseCompleted.Length, new TextEntityTypeBold());

                //var text = new FormattedText(message, new[] { entity });
                //var formatted = ClientEx.ParseMarkdown(text);

                //Aggregator.Publish(new UpdateConfetti());
                if (_gift.UserLimits != null)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsGiftCompleted, Locale.Declension(Strings.R.Gift2SentRemainsLimit, _gift.UserLimits.RemainingCount - 1)), new DelayedFileSource(_clientService, _gift.Sticker));
                }
                else
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsGiftCompleted, Locale.Declension(Strings.R.StarsGiftCompletedText, _gift.StarCount)), new DelayedFileSource(_clientService, _gift.Sticker));
                }

                return PayResult.Succeeded;
            }
            else if (response is Error error)
            {
                if (error.Message == "STARGIFT_USAGE_LIMITED" && _gift.OverallLimits != null)
                {
                    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2SoldOutTitle, Locale.Declension(Strings.R.Gift2SoldOutHint, _gift.OverallLimits.TotalCount)), new DelayedFileSource(_clientService, _gift.Sticker));
                }
                else if (error.Message == "STARGIFT_USER_USAGE_LIMITED" && _gift.UserLimits != null)
                {
                    ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.Gift2PerUserLimit, _gift.UserLimits.TotalCount), new DelayedFileSource(_clientService, _gift.Sticker));
                }
                else
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
            }
            else if (response is ErrorStarsNeeded)
            {
                return PayResult.StarsNeeded;
            }

            return PayResult.Failed;
        }

        private void UpgradeableInfo_Click(object sender, TextUrlClickEventArgs e)
        {

        }

        private void LimitedRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_gift.OverallLimits != null)
            {
                var percent = (double)(_gift.OverallLimits.TotalCount - _gift.OverallLimits.RemainingCount) / _gift.OverallLimits.TotalCount;
                var width = e.NewSize.Width * percent;

                var next = ElementComposition.GetElementVisual(NextPanel);
                next.Clip = next.Compositor.CreateInsetClip(0, 0, (float)width, 0);
            }
        }

        private void Resale_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
            _navigationService.ShowPopup(new ResoldGiftsPopup(_clientService, _navigationService, _giftForResale, _receiverId));
        }
    }
}
