//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceiptPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly string _transactionId;

        private Vector<PaidMedia> _media;

        private long _media1Token;
        private long _media2Token;

        public ReceiptPopup(IClientService clientService, INavigationService navigationService, StarTransaction transaction)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _transactionId = transaction.Id;

            var info = TransactionInfo.FromStarTransaction(clientService, transaction);

            Title.Text = info.Heading;

            Photo.Visibility = Visibility.Collapsed;
            AnimatedPhoto.Visibility = Visibility.Collapsed;
            MediaPreview.Visibility = Visibility.Collapsed;
            Subtitle.Visibility = Visibility.Collapsed;

            From.Header = info.Header;
            FromPhoto.Source = info.Photo;
            FromPhoto.Visibility = info.HasSender
                ? Visibility.Visible
                : Visibility.Collapsed;
            FromTitle.Text = info.HasSender
                ? info.Title
                : GetSourceName(transaction.Type, info.Title);

            if (info.HasSender)
            {
                Photo.Source = info.Photo;
                Photo.Visibility = Visibility.Visible;
            }

            UpdateType(clientService, transaction, info);

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

        // Deposits name the platform they came from rather than repeating the heading.
        private static string GetSourceName(StarTransactionType type, string fallback)
        {
            return type switch
            {
                StarTransactionTypeAppStoreDeposit => Strings.AppStore,
                StarTransactionTypeGooglePlayDeposit => Strings.PlayMarket,
                StarTransactionTypeFragmentDeposit or StarTransactionTypeFragmentWithdrawal => Strings.Fragment,
                StarTransactionTypePremiumBotDeposit => Strings.StarsTransactionBot,
                _ => fallback
            };
        }

        private void UpdateType(IClientService clientService, StarTransaction transaction, in TransactionInfo info)
        {
            if (info.Media != null)
            {
                UpdatePaidMedia(clientService, info.Media);
                return;
            }

            switch (transaction.Type)
            {
                case StarTransactionTypePremiumPurchase premiumPurchase:
                    SetAnimated(clientService, premiumPurchase.Sticker);
                    SetRow(Duration, Locale.Declension(Strings.R.Months, premiumPurchase.MonthCount));
                    break;

                case StarTransactionTypeUserDeposit userDeposit:
                    SetAnimated(clientService, userDeposit.Sticker);

                    if (clientService.TryGetUser(userDeposit.UserId, out User sender))
                    {
                        SetSubtitle(transaction.IsRefund
                            ? string.Format(Strings.ActionGiftStarsSubtitle, sender.FirstName)
                            : Strings.ActionGiftStarsSubtitleYou);
                    }
                    break;

                case StarTransactionTypeBotInvoicePurchase botInvoicePurchase:
                    SetProduct(clientService, botInvoicePurchase.UserId, botInvoicePurchase.ProductInfo);
                    break;
                case StarTransactionTypeBotInvoiceSale botInvoiceSale:
                    SetProduct(clientService, botInvoiceSale.UserId, botInvoiceSale.ProductInfo);
                    break;
                case StarTransactionTypeBotSubscriptionPurchase botSubscriptionPurchase:
                    SetProduct(clientService, botSubscriptionPurchase.UserId, botSubscriptionPurchase.ProductInfo);
                    break;
                case StarTransactionTypeBotSubscriptionSale botSubscriptionSale:
                    SetProduct(clientService, botSubscriptionSale.UserId, botSubscriptionSale.ProductInfo);
                    break;

                case StarTransactionTypeGiftPurchase giftPurchase:
                    SetGift(clientService, giftPurchase.Gift);
                    break;
                case StarTransactionTypeGiftSale giftSale:
                    SetGift(clientService, giftSale.Gift);
                    break;
                case StarTransactionTypeGiftUpgradePurchase giftUpgradePurchase:
                    SetGift(clientService, giftUpgradePurchase.Gift);
                    break;
                case StarTransactionTypeGiftAuctionBid giftAuctionBid:
                    SetGift(clientService, giftAuctionBid.Gift);
                    break;

                case StarTransactionTypeGiftUpgrade giftUpgrade:
                    SetUpgradedGift(clientService, giftUpgrade.Gift, Strings.StarGiftReasonUpgrade);
                    break;
                case StarTransactionTypeGiftTransfer giftTransfer:
                    SetUpgradedGift(clientService, giftTransfer.Gift, Strings.StarGiftReasonTransfer);
                    break;
                case StarTransactionTypeGiftOriginalDetailsDrop giftOriginalDetailsDrop:
                    SetUpgradedGift(clientService, giftOriginalDetailsDrop.Gift, Strings.StarGiftReasonRemovedDescription);
                    break;
                case StarTransactionTypeGiftPurchaseOffer giftPurchaseOffer:
                    SetUpgradedGift(clientService, giftPurchaseOffer.Gift, transaction.IsRefund
                        ? Strings.StarGiftReasonSale
                        : Strings.StarGiftReasonOffer);
                    break;
                case StarTransactionTypeUpgradedGiftPurchase upgradedGiftPurchase:
                    SetUpgradedGift(clientService, upgradedGiftPurchase.Gift, transaction.IsRefund
                        ? Strings.StarGiftReasonSale
                        : Strings.StarGiftReasonPurchase);
                    break;
                case StarTransactionTypeUpgradedGiftSale upgradedGiftSale:
                    // A refunded sale reads as the buyer's side of the same deal, and an
                    // offer is worded apart from an ordinary resale.
                    SetUpgradedGift(clientService, upgradedGiftSale.Gift, transaction.IsRefund
                        ? upgradedGiftSale.ViaOffer
                            ? Strings.StarGiftReasonOfferRefund
                            : Strings.StarGiftReasonPurchase
                        : Strings.StarGiftReasonSale);
                    SetFullPrice(transaction.StarAmount, upgradedGiftSale.CommissionStarAmount);
                    break;

                case StarTransactionTypeAffiliateProgramCommission affiliateProgramCommission:
                    Reason.Header = Strings.StarAffiliateReason;
                    SetRow(Reason, Strings.StarAffiliateReasonProgram);
                    SetRow(Commission, affiliateProgramCommission.CommissionPerMille.CommissionPercent());
                    break;

                case StarTransactionTypeGiveawayDeposit:
                    Reason.Header = Strings.StarGiveawayReason;
                    SetRow(Reason, Strings.StarGiveawayReasonLink);
                    break;

                case StarTransactionTypeTelegramApiUsage telegramApiUsage:
                    From.Visibility = Visibility.Collapsed;
                    SetRow(Messages, Locale.Declension(Strings.R.StarsTransactionFloodskipNumber, telegramApiUsage.RequestCount));
                    break;

                case StarTransactionTypePaidMessageReceive paidMessageReceive:
                    SetFullPrice(transaction.StarAmount, paidMessageReceive.CommissionStarAmount);
                    SetInfo(string.Format(Strings.StarsTransactionMessageFeeInfo, (1000 - paidMessageReceive.CommissionPerMille).CommissionPercent()));
                    break;
                case StarTransactionTypePaidGroupCallMessageReceive paidGroupCallMessageReceive:
                    SetFullPrice(transaction.StarAmount, paidGroupCallMessageReceive.CommissionStarAmount);
                    break;
                case StarTransactionTypePaidGroupCallReactionReceive paidGroupCallReactionReceive:
                    SetFullPrice(transaction.StarAmount, paidGroupCallReactionReceive.CommissionStarAmount);
                    break;
            }
        }

        private static void SetRow(TableViewItem row, string content)
        {
            row.Content = content;
            row.Visibility = Visibility.Visible;
        }

        private void SetSubtitle(string text)
        {
            Subtitle.Text = text;
            Subtitle.Visibility = Visibility.Visible;
        }

        private void SetInfo(string text)
        {
            TextBlockHelper.SetMarkdown(Info, text);
            Info.Visibility = Visibility.Visible;
        }

        private void SetAnimated(IClientService clientService, Sticker sticker)
        {
            var source = DelayedFileSource.FromSticker(clientService, sticker);
            if (source == null)
            {
                return;
            }

            AnimatedPhoto.Source = source;
            AnimatedPhoto.Visibility = Visibility.Visible;

            Photo.Visibility = Visibility.Collapsed;
        }

        private void SetGift(IClientService clientService, Gift gift)
        {
            SetAnimated(clientService, gift.Sticker);

            if (gift.OverallLimits != null)
            {
                SetRow(Availability, gift.RemainingText());
            }
        }

        private void SetUpgradedGift(IClientService clientService, UpgradedGift gift, string reason)
        {
            SetAnimated(clientService, gift.Model.Sticker);
            SetRow(GiftName, gift.ToName());

            Reason.Header = Strings.StarGiftReason;
            SetRow(Reason, reason);
        }

        private void SetProduct(IClientService clientService, long userId, ProductInfo productInfo)
        {
            TextBlockHelper.SetFormattedText(Subtitle, productInfo.Description);
            Subtitle.Visibility = Visibility.Visible;

            var small = productInfo.Photo?.GetSmall();
            if (small != null)
            {
                Photo.Source = new ProfilePictureSourcePhoto(clientService, userId, small.Photo, productInfo.Photo.Minithumbnail);
                Photo.Visibility = Visibility.Visible;
            }
        }

        // What the transaction would have cost without Telegram's cut, which is the amount
        // that reached us plus the commission that didn't.
        private void SetFullPrice(StarAmount amount, StarAmount commission)
        {
            if (commission == null || (commission.StarCount == 0 && commission.NanostarCount == 0))
            {
                return;
            }

            var nanostars = Math.Abs(amount.NanostarCount) + Math.Abs(commission.NanostarCount);
            var stars = Math.Abs(amount.StarCount) + Math.Abs(commission.StarCount) + nanostars / 1000000000;

            SetRow(FullPrice, new StarAmount(stars, nanostars % 1000000000).ToValue());
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

            MediaPreview.Visibility = Visibility.Collapsed;
            AnimatedPhoto.Visibility = Visibility.Collapsed;

            if (clientService.TryGetUser(receipt.SellerBotUserId, out User user))
            {
                FromPhoto.Source = ProfilePictureSource.User(clientService, user);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = receipt.ProductInfo.Title;
                Photo.Source = ProfilePictureSource.User(clientService, user);

                SetProduct(clientService, user.Id, receipt.ProductInfo);
            }
            else
            {
                FromPhoto.Source = ProfilePictureSourceText.GetGlyph(Icons.QuestionCircle, long.MinValue);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionUnsupported;
                From.Header = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.QuestionCircle, long.MinValue);
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

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsTOSLink);
        }

        private void UpdatePaidMedia(IClientService clientService, Vector<PaidMedia> paidMedia)
        {
            if (paidMedia.Count == 0)
            {
                return;
            }

            _media = paidMedia;

            MediaPreview.Visibility = Visibility.Visible;
            Photo.Visibility = Visibility.Collapsed;

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
                Media1.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void UpdateMedia(IClientService clientService, PaidMedia media, Border target, ref long token)
        {
            var file = media.GetThumbnailFile();
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

        private void UpdateMedia1(File file)
        {
            UpdateMedia(Media1, file);
        }

        private void UpdateMedia2(File file)
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
            if (_media == null)
            {
                return;
            }

            GalleryMedia item = null;
            GalleryMedia Filter(PaidMedia x)
            {
                GalleryMedia result = null;
                if (x is PaidMediaPhoto photo)
                {
                    result = new GalleryMedia(_clientService, photo.Photo, null, true);
                }
                else if (x is PaidMediaVideo video)
                {
                    result = new GalleryMedia(_clientService, video.Video, null, true);
                }

                item ??= result;
                return result;
            }

            var items = _media
                .Select(Filter)
                .Where(x => x is not null)
                .ToList();

            var storageService = _clientService.Session.Resolve<IStorageService>();
            var aggregator = _clientService.Session.Resolve<IEventAggregator>();

            var viewModel = new StandaloneGalleryViewModel(_clientService, storageService, aggregator, items, item);
            _navigationService.ShowGallery(viewModel, Media1);
        }
    }
}
