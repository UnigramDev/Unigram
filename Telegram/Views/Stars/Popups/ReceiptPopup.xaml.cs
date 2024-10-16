//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
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

            if (transaction.Partner is StarTransactionPartnerTelegram)
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
            else if (transaction.Partner is StarTransactionPartnerFragment)
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
            else if (transaction.Partner is StarTransactionPartnerAppStore or StarTransactionPartnerGooglePlay)
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
            else if (transaction.Partner is StarTransactionPartnerBot sourceBot && clientService.TryGetUser(sourceBot.UserId, out User botUser))
            {
                FromPhoto.SetUser(clientService, botUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = botUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                if (sourceBot.Purpose is BotTransactionPurposeInvoicePayment invoicePayment)
                {
                    Title.Text = invoicePayment.ProductInfo.Title;
                    TextBlockHelper.SetFormattedText(Subtitle, invoicePayment.ProductInfo.Description);

                    var small = invoicePayment.ProductInfo.Photo?.GetSmall();
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
                }
                else if (sourceBot.Purpose is BotTransactionPurposePaidMedia paidMedia)
                {
                    Title.Text = Strings.StarMediaPurchase;

                    if (paidMedia.Media.Count > 0)
                    {
                        MediaPreview.Visibility = Visibility.Visible;

                        UpdateMedia(clientService, paidMedia.Media[0], Media1, ref _media1Token);

                        if (paidMedia.Media.Count > 1)
                        {
                            UpdateMedia(clientService, paidMedia.Media[1], Media2, ref _media2Token);

                            Media2.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            Media2.Visibility = Visibility.Collapsed;
                            Media1.HorizontalAlignment = HorizontalAlignment.Center;
                            Media1.HorizontalAlignment = HorizontalAlignment.Center;
                        }
                    }
                    else
                    {
                        Photo.SetUser(clientService, botUser, 96);

                        MediaPreview.Visibility = Visibility.Collapsed;
                    }
                }

                AnimatedPhoto.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerBusiness sourceBusiness && clientService.TryGetUser(sourceBusiness.UserId, out User businessUser))
            {
                FromPhoto.SetUser(clientService, businessUser, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = businessUser.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                Title.Text = Strings.StarMediaPurchase;

                MediaPreview.Visibility = Visibility.Visible;

                UpdateMedia(clientService, sourceBusiness.Media[0], Media1, ref _media1Token);

                if (sourceBusiness.Media.Count > 1)
                {
                    UpdateMedia(clientService, sourceBusiness.Media[1], Media2, ref _media2Token);

                    Media2.Visibility = Visibility.Visible;
                }
                else
                {
                    Media2.Visibility = Visibility.Collapsed;
                    Media1.HorizontalAlignment = HorizontalAlignment.Center;
                    Media1.HorizontalAlignment = HorizontalAlignment.Center;
                }

                AnimatedPhoto.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerUser sourceUser && clientService.TryGetUser(sourceUser.UserId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                From.Header = Strings.StarsTransactionRecipient;

                if (sourceUser.Purpose is UserTransactionPurposeGiftedStars giftedStars)
                {
                    Title.Text = transaction.StarCount < 0
                        ? Strings.StarsGiftSent
                        : Strings.StarsGiftReceived;
                    Subtitle.Text = transaction.StarCount < 0
                        ? string.Format(Strings.ActionGiftStarsSubtitle, user.FirstName)
                        : Strings.ActionGiftStarsSubtitleYou;
                    Subtitle.Visibility = Visibility.Visible;

                    AnimatedPhoto.Source = new DelayedFileSource(clientService, giftedStars.Sticker);
                }
                else if (sourceUser.Purpose is UserTransactionPurposeGiftSell giftSell)
                {
                    Title.Text = transaction.StarCount < 0
                        ? Strings.Gift2TransactionRefundedConverted
                        : Strings.Gift2TransactionConverted;
                    Subtitle.Visibility = Visibility.Collapsed;

                    AnimatedPhoto.Source = new DelayedFileSource(clientService, giftSell.Gift.Sticker);

                    if (giftSell.Gift.TotalCount > 0)
                    {
                        Availability.Visibility = Visibility.Visible;
                        Availability.Content = giftSell.Gift.RemainingText();
                    }
                }
                else if (sourceUser.Purpose is UserTransactionPurposeGiftSend giftSend)
                {
                    Title.Text = transaction.StarCount < 0
                        ? Strings.Gift2TransactionSent
                        : Strings.Gift2TransactionRefundedSent;
                    Subtitle.Visibility = Visibility.Collapsed;

                    AnimatedPhoto.Source = new DelayedFileSource(clientService, giftSend.Gift.Sticker);

                    if (giftSend.Gift.TotalCount > 0)
                    {
                        Availability.Visibility = Visibility.Visible;
                        Availability.Content = giftSend.Gift.RemainingText();
                    }
                }

                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerChat sourceChat && clientService.TryGetChat(sourceChat.ChatId, out Chat chat))
            {
                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                From.Header = Strings.StarsTransactionRecipient;

                Subtitle.Visibility = Visibility.Collapsed;

                if (sourceChat.Purpose is ChatTransactionPurposePaidMedia paidMedia)
                {
                    Title.Text = Strings.StarMediaPurchase;

                    MediaPreview.Visibility = Visibility.Visible;

                    UpdateMedia(clientService, paidMedia.Media[0], Media1, ref _media1Token);

                    if (paidMedia.Media.Count > 1)
                    {
                        UpdateMedia(clientService, paidMedia.Media[1], Media2, ref _media2Token);

                        Media2.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Media2.Visibility = Visibility.Collapsed;
                        Media1.HorizontalAlignment = HorizontalAlignment.Center;
                        Media1.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                }
                else if (sourceChat.Purpose is ChatTransactionPurposeReaction)
                {
                    Title.Text = Strings.StarsReactionsSent;
                    Photo.SetChat(clientService, chat, 96);

                    MediaPreview.Visibility = Visibility.Collapsed;
                }
                else if (sourceChat.Purpose is ChatTransactionPurposeJoin)
                {
                    Title.Text = Strings.StarsTransactionSubscriptionMonthly;
                    Photo.SetChat(clientService, chat, 96);

                    MediaPreview.Visibility = Visibility.Collapsed;
                }
                else if (sourceChat.Purpose is ChatTransactionPurposeGiveaway)
                {
                    Title.Text = Strings.StarsGiveawayPrizeReceived;
                    Photo.SetChat(clientService, chat, 96);

                    MediaPreview.Visibility = Visibility.Collapsed;
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

                MediaPreview.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(transaction.Id))
            {
                Transaction.Visibility = Visibility.Collapsed;
            }

            Identifier.Text = transaction.Id;
            Date.Content = Formatter.DateAt(transaction.Date);

            StarCount.Text = (transaction.StarCount < 0 ? string.Empty : "+") + transaction.StarCount.ToString("N0");
            StarCount.Foreground = BootStrapper.Current.Resources[transaction.StarCount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

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

        private readonly MessageSender _giftSenderId;
        private readonly long _giftMessageId;
        private readonly long _giftStarCount;

        public ReceiptPopup(IClientService clientService, INavigationService navigationService, UserGift gift, long userId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _giftSenderId = new MessageSenderUser(gift.SenderUserId);
            _giftMessageId = gift.MessageId;
            _giftStarCount = gift.SellStarCount;

            MediaPreview.Visibility = Visibility.Collapsed;

            if (clientService.TryGetUser(gift.SenderUserId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = Strings.StarsTransactionHidden;
            }

            From.Header = Strings.Gift2From;
            Title.Text = Strings.Gift2TitleReceived;

            if (userId != clientService.Options.MyId)
            {
                Subtitle.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (gift.IsSaved)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, Strings.Gift2InfoPinned);
                    Info.Text = Strings.Gift2ProfileVisible;
                    PurchaseCommand.Content = Strings.Gift2ProfileMakeInvisible;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.Gift2Info, gift.SellStarCount));
                    Info.Text = Strings.Gift2ProfileInvisible;
                    PurchaseCommand.Content = Strings.Gift2ProfileMakeVisible;
                }

                Info.Visibility = Visibility.Visible;

                Convert.Visibility = Visibility.Visible;
                Convert.Content = Locale.Declension(Strings.R.Gift2ToBalance, gift.SellStarCount);
            }

            AnimatedPhoto.LoopCount = 0;
            AnimatedPhoto.Source = new DelayedFileSource(clientService, gift.Gift.Sticker);

            Transaction.Visibility = Visibility.Collapsed;
            Date.Content = Formatter.DateAt(gift.Date);

            StarCount.Text = "+" + gift.Gift.StarCount.ToString("N0");
            StarCount.Foreground = BootStrapper.Current.Resources["SystemFillColorSuccessBrush"] as Brush;

            Refund.Visibility = Visibility.Collapsed;
            Terms.Visibility = Visibility.Collapsed;

            if (gift.Gift.TotalCount > 0)
            {
                Availability.Visibility = Visibility.Visible;
                Availability.Content = gift.Gift.RemainingText();
            }

            if (gift.Text?.Text.Length > 0)
            {
                TableRoot.BorderThickness = new Thickness(1, 1, 1, 0);
                TableRoot.CornerRadius = new CornerRadius(2, 2, 0, 0);

                CaptionRoot.Visibility = Visibility.Visible;
                Caption.SetText(clientService, gift.Text);
            }
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

            if (_transaction.Partner is not StarTransactionPartnerChat { Purpose: ChatTransactionPurposePaidMedia paidMedia })
            {
                return;
            }

            var items = paidMedia.Media
                .Select(Filter)
                .Where(x => x is not null)
                .ToList();

            var storageService = TypeResolver.Current.Resolve<IStorageService>(_clientService.SessionId);
            var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);

            var viewModel = new StandaloneGalleryViewModel(_clientService, storageService, aggregator, items, item);
            _navigationService.ShowGallery(viewModel, Media1);
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (_clientService.TryGetUser(_giftSenderId, out User user))
            {
                var message = string.Format(Strings.Gift2ConvertText, user.FirstName, Locale.Declension(Strings.R.StarsCount, _giftStarCount));

                var confirm = await MessagePopup.ShowAsync(XamlRoot, target: null, message, Strings.Gift2ConvertTitle, Strings.Gift2ConvertButton, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await _clientService.SendAsync(new SellGift(user.Id, _giftMessageId));
                    if (response is Ok)
                    {
                        Hide(ContentDialogResult.Secondary);

                        var popup = new StarsPopup();

                        void handler(object sender, object e)
                        {
                            popup.Opened -= handler;
                            ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2ConvertedTitle, Locale.Declension(Strings.R.Gift2Converted, _giftStarCount)), ToastPopupIcon.StarsTopup);
                        }

                        _ = _navigationService.ShowPopupAsync(popup);
                        popup.Opened += handler;
                    }
                }
            }
        }
    }
}
