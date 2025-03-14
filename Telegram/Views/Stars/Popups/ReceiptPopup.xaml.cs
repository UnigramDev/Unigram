//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Gallery;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceiptPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly StarTransaction _transaction;

        private readonly string _transactionId;

        private long _thumbnailToken;

        private long _media1Token;
        private long _media2Token;

        public ReceiptPopup(IClientService clientService, INavigationService navigationService, StarTransaction transaction)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _transaction = transaction;
            _transactionId = transaction.Id;

            if (transaction.Type is StarTransactionTypePremiumBotDeposit)
            {
                FromPhoto.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionBot;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionBot;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Visibility = Visibility.Collapsed;
                AnimatedPhoto.Visibility = Visibility.Collapsed;

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeFragmentWithdrawal or StarTransactionTypeFragmentDeposit)
            {
                FromPhoto.Source = new PlaceholderImage(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.Fragment;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionFragment;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Visibility = Visibility.Collapsed;
                AnimatedPhoto.Visibility = Visibility.Collapsed;

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeAppStoreDeposit or StarTransactionTypeGooglePlayDeposit)
            {
                FromPhoto.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionInApp;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionInApp;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Visibility = Visibility.Collapsed;
                AnimatedPhoto.Visibility = Visibility.Collapsed;

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotInvoicePurchase botInvoicePurchase)
            {
                var botUser = clientService.GetUser(botInvoicePurchase.UserId);

                FromPhoto.SetUser(clientService, botUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = botUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = botInvoicePurchase.ProductInfo.Title;
                TextBlockHelper.SetFormattedText(Subtitle, botInvoicePurchase.ProductInfo.Description);

                var small = botInvoicePurchase.ProductInfo.Photo?.GetSmall();
                if (small != null)
                {
                    UpdateManager.Subscribe(this, _clientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                    UpdateThumbnail(small.Photo);
                }
                else
                {
                    Photo.SetUser(clientService, botUser, 96);
                }

                MediaPreview.Visibility = Visibility.Collapsed;
                AnimatedPhoto.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotPaidMediaPurchase botPaidMediaPurchase)
            {
                var botUser = clientService.GetUser(botPaidMediaPurchase.UserId);

                FromPhoto.SetUser(clientService, botUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = botUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, botPaidMediaPurchase.Media, botUser, null);
            }
            else if (transaction.Type is StarTransactionTypeBotInvoiceSale botInvoiceSale)
            {
                var botUser = clientService.GetUser(botInvoiceSale.UserId);

                FromPhoto.SetUser(clientService, botUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = botUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = botInvoiceSale.ProductInfo.Title;
                TextBlockHelper.SetFormattedText(Subtitle, botInvoiceSale.ProductInfo.Description);

                var small = botInvoiceSale.ProductInfo.Photo?.GetSmall();
                if (small != null)
                {
                    UpdateManager.Subscribe(this, _clientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                    UpdateThumbnail(small.Photo);
                }
                else
                {
                    Photo.SetUser(clientService, botUser, 96);
                }

                MediaPreview.Visibility = Visibility.Collapsed;
                AnimatedPhoto.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeBotPaidMediaSale botPaidMediaSale)
            {
                var botUser = clientService.GetUser(botPaidMediaSale.UserId);

                FromPhoto.SetUser(clientService, botUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = botUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, botPaidMediaSale.Media, botUser, null);
            }
            else if (transaction.Type is StarTransactionTypeGiftSale giftSale)
            {
                var user = clientService.GetUser(giftSale.UserId);

                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = transaction.StarAmount.IsNegative()
                    ? Strings.Gift2TransactionRefundedConverted
                    : Strings.Gift2TransactionConverted;
                Subtitle.Visibility = Visibility.Collapsed;

                AnimatedPhoto.Source = new DelayedFileSource(clientService, giftSale.Gift.Sticker);
                MediaPreview.Visibility = Visibility.Collapsed;

                if (giftSale.Gift.TotalCount > 0)
                {
                    Availability.Visibility = Visibility.Visible;
                    Availability.Content = giftSale.Gift.RemainingText();
                }
            }
            else if (transaction.Type is StarTransactionTypeUserDeposit userDeposit)
            {
                var user = clientService.GetUser(userDeposit.UserId);
                if (user != null)
                {
                    FromPhoto.SetUser(clientService, user, 24);
                    FromTitle.Text = user.FullName();
                }
                else
                {
                    FromPhoto.Source = new PlaceholderImage(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
                    FromTitle.Text = Strings.StarsTransactionUnknown;
                }

                FromPhoto.Visibility = Visibility.Visible;
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = transaction.StarAmount.IsNegative()
                    ? Strings.StarsGiftSent
                    : Strings.StarsGiftReceived;
                Subtitle.Text = transaction.StarAmount.IsNegative()
                    ? string.Format(Strings.ActionGiftStarsSubtitle, user.FirstName)
                    : Strings.ActionGiftStarsSubtitleYou;
                Subtitle.Visibility = Visibility.Visible;

                AnimatedPhoto.Source = new DelayedFileSource(clientService, userDeposit.Sticker);
            }
            else if (transaction.Type is StarTransactionTypeGiftPurchase giftPurchase)
            {
                if (clientService.TryGetUser(giftPurchase.OwnerId, out User user))
                {
                    FromPhoto.SetUser(clientService, user, 24);
                    FromTitle.Text = user.FullName();
                }
                else if (clientService.TryGetChat(giftPurchase.OwnerId, out Chat chat))
                {
                    FromPhoto.SetChat(clientService, chat, 24);
                    FromTitle.Text = chat.Title;
                }

                FromPhoto.Visibility = Visibility.Visible;
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = transaction.StarAmount.IsNegative()
                    ? Strings.Gift2TransactionSent
                    : Strings.Gift2TransactionRefundedSent;
                Subtitle.Visibility = Visibility.Collapsed;

                AnimatedPhoto.Source = new DelayedFileSource(clientService, giftPurchase.Gift.Sticker);

                if (giftPurchase.Gift.TotalCount > 0)
                {
                    Availability.Visibility = Visibility.Visible;
                    Availability.Content = giftPurchase.Gift.RemainingText();
                }
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidMediaPurchase channelPaidMediaPurchase)
            {
                var chat = clientService.GetChat(channelPaidMediaPurchase.ChatId);

                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, channelPaidMediaPurchase.Media, null, chat);
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidReactionSend channelPaidReactionSend)
            {
                var chat = clientService.GetChat(channelPaidReactionSend.ChatId);

                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarsReactionsSent;
                Photo.SetChat(clientService, chat, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelSubscriptionPurchase channelSubscriptionPurchase)
            {
                var chat = clientService.GetChat(channelSubscriptionPurchase.ChatId);

                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarsTransactionSubscriptionMonthly;
                Photo.SetChat(clientService, chat, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidMediaSale channelPaidMediaSale)
            {
                var user = clientService.GetUser(channelPaidMediaSale.UserId);

                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarMediaPurchase;
                UpdatePaidMedia(clientService, channelPaidMediaSale.Media, user, null);
            }
            else if (transaction.Type is StarTransactionTypeChannelPaidReactionReceive channelPaidReactionReceive)
            {
                var user = clientService.GetUser(channelPaidReactionReceive.UserId);

                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarsReactionsSent;
                Photo.SetUser(clientService, user, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeChannelSubscriptionSale channelSubscriptionSale)
            {
                var user = clientService.GetUser(channelSubscriptionSale.UserId);

                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarsTransactionSubscriptionMonthly;
                Photo.SetUser(clientService, user, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeGiveawayDeposit giveawayDeposit)
            {
                var chat = clientService.GetChat(giveawayDeposit.ChatId);

                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                Title.Text = Strings.StarsGiveawayPrizeReceived;
                Photo.SetChat(clientService, chat, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is StarTransactionTypeTelegramApiUsage telegramApiUsage)
            {
                Title.Text = Strings.StarsTransactionFloodskip;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.ChatStarsFilled, 3);

                MediaPreview.Visibility = Visibility.Collapsed;

                From.Visibility = Visibility.Collapsed;
                Messages.Visibility = Visibility.Visible;
                Messages.Content = Locale.Declension(Strings.R.StarsTransactionFloodskipNumber, telegramApiUsage.RequestCount);
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionUnsupported;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);

                MediaPreview.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(transaction.Id))
            {
                Transaction.Visibility = Visibility.Collapsed;
            }

            Identifier.Text = transaction.Id;
            Date.Content = Formatter.DateAt(transaction.Date);

            StarCount.Text = transaction.StarAmount.ToValue(true);
            StarCount.Foreground = BootStrapper.Current.Resources[transaction.StarAmount.IsNegative() ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

            Refund.Visibility = transaction.IsRefund
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public ReceiptPopup(IClientService clientService, INavigationService navigationService, PaymentReceipt receipt)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            if (receipt.Type is not PaymentReceiptTypeStars stars)
            {
                return;
            }

            _transactionId = stars.TransactionId;

            if (clientService.TryGetUser(receipt.SellerBotUserId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = receipt.ProductInfo.Title;
                TextBlockHelper.SetFormattedText(Subtitle, receipt.ProductInfo.Description);

                var small = receipt.ProductInfo.Photo?.GetSmall();
                if (small != null)
                {
                    UpdateManager.Subscribe(this, _clientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                    UpdateThumbnail(small.Photo);
                }
                else
                {
                    Photo.SetUser(clientService, user, 96);
                }
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionUnsupported;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
            }

            Identifier.Text = stars.TransactionId;
            Date.Content = Formatter.DateAt(receipt.Date);

            StarCount.Text = (stars.StarCount < 0 ? string.Empty : "+") + stars.StarCount.ToString("N0");
            StarCount.Foreground = BootStrapper.Current.Resources[stars.StarCount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

            Refund.Visibility = Visibility.Collapsed;
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }

        private async void ShareLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new HttpUrl("https://")));
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsTOSLink);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateThumbnail(file);
        }

        private void UpdateThumbnail(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Photo.Source = UriEx.ToBitmap(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _clientService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdatePaidMedia(IClientService clientService, IList<PaidMedia> paidMedia, User fallbackUser, Chat fallbackChat)
        {
            if (paidMedia.Count > 0)
            {
                MediaPreview.Visibility = Visibility.Visible;

                UpdateMedia(clientService, paidMedia[0], Media1, ref _media1Token);

                if (paidMedia.Count > 1)
                {
                    UpdateMedia(clientService, paidMedia[1], Media2, ref _media2Token);

                    Media2.Visibility = Visibility.Visible;
                }
                else
                {
                    Media2.Visibility = Visibility.Collapsed;
                    Media1.HorizontalAlignment = HorizontalAlignment.Center;
                    Media1.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            else if (fallbackUser != null)
            {
                Photo.SetUser(clientService, fallbackUser, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (fallbackChat != null)
            {
                Photo.SetChat(clientService, fallbackChat, 96);

                MediaPreview.Visibility = Visibility.Collapsed;
            }

            AnimatedPhoto.Visibility = Visibility.Collapsed;
        }

        private void UpdateMedia(IClientService clientService, PaidMedia media, Border target, ref long token)
        {
            File file = null;
            if (media is PaidMediaPhoto photo)
            {
                file = photo.Photo.GetSmall()?.Photo;
            }
            else if (media is PaidMediaVideo video)
            {
                file = video.Video.Thumbnail?.File;
            }

            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                UpdateMedia(target, file);
            }
            else if (file.Local.CanBeDownloaded)
            {
                UpdateManager.Subscribe(this, clientService, file, ref token, target == Media1 ? UpdateMedia1 : UpdateMedia2, true);
                clientService.DownloadFile(file.Id, 16);

                target.Background = null;
            }
        }

        private void UpdateMedia1(object target, File file)
        {
            UpdateMedia(Media1, file);
        }

        private void UpdateMedia2(object target, File file)
        {
            UpdateMedia(Media2, file);
        }

        private void UpdateMedia(Border target, File file)
        {
            target.Background = new ImageBrush
            {
                ImageSource = UriEx.ToBitmap(file.Local.Path),
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_transactionId);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(XamlRoot, Strings.StarsTransactionIDCopied, ToastPopupIcon.Copied);
        }

        private void MediaPreview_Click(object sender, RoutedEventArgs e)
        {
            GalleryMedia item = null;
            GalleryMedia Filter(PaidMedia x)
            {
                GalleryMedia result = null;
                if (x is PaidMediaPhoto photo)
                {
                    result = new GalleryPhoto(_clientService, photo.Photo, null, true);
                }
                else if (x is PaidMediaVideo video)
                {
                    result = new GalleryVideo(_clientService, video.Video, null, true);
                }

                item ??= result;
                return result;
            }

            if (_transaction.Type is not StarTransactionTypeChannelPaidMediaPurchase channelPaidMediaPurchase)
            {
                return;
            }

            var items = channelPaidMediaPurchase.Media
                .Select(Filter)
                .Where(x => x is not null)
                .ToList();

            var storageService = TypeResolver.Current.Resolve<IStorageService>(_clientService.SessionId);
            var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);

            var viewModel = new StandaloneGalleryViewModel(_clientService, storageService, aggregator, items, item);
            _navigationService.ShowGallery(viewModel, Media1);
        }
    }
}
