//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

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
                FromHeader.Text = Strings.StarsTransactionSource;

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
                FromHeader.Text = Strings.StarsTransactionSource;

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
                FromHeader.Text = Strings.StarsTransactionSource;

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
                FromHeader.Text = Strings.StarsTransactionRecipient;

                if (sourceBot.ProductInfo != null)
                {
                    Title.Text = sourceBot.ProductInfo.Title;
                    TextBlockHelper.SetFormattedText(Subtitle, sourceBot.ProductInfo.Description);

                    var small = sourceBot.ProductInfo.Photo?.GetSmall();
                    if (small != null)
                    {
                        UpdateManager.Subscribe(this, _clientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                        UpdateThumbnail(small.Photo);
                    }
                    else
                    {
                        Photo.SetUser(clientService, botUser, 120);
                    }
                }

                AnimatedPhoto.Visibility = Visibility.Collapsed;
                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerUser sourceUser && clientService.TryGetUser(sourceUser.UserId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
                FromHeader.Text = Strings.StarsTransactionRecipient;

                Title.Text = transaction.StarCount < 0
                    ? Strings.StarsGiftSent
                    : Strings.StarsGiftReceived;
                Subtitle.Text = transaction.StarCount < 0
                    ? string.Format(Strings.ActionGiftStarsSubtitle, user.FirstName)
                    : Strings.ActionGiftStarsSubtitleYou;
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Visible;

                if (sourceUser.Sticker != null)
                {
                    AnimatedPhoto.Source = new DelayedFileSource(clientService, sourceUser.Sticker);
                }
                
                MediaPreview.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerChannel sourceChannel && clientService.TryGetChat(sourceChannel.ChatId, out Chat chat))
            {
                FromPhoto.SetChat(clientService, chat, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = chat.Title;
                FromHeader.Text = Strings.StarsTransactionRecipient;

                Title.Text = Strings.StarMediaPurchase;
                Subtitle.Visibility = Visibility.Collapsed;

                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Visible;

                UpdateMedia(clientService, sourceChannel.Media[0], Media1, ref _media1Token);

                if (sourceChannel.Media.Count > 1)
                {
                    UpdateMedia(clientService, sourceChannel.Media[1], Media2, ref _media2Token);

                    Media2.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    Media2.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Media1.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
                    Media1.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
                }
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionUnsupported;
                FromHeader.Text = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);

                MediaPreview.Visibility = Visibility.Collapsed;
            }

            Identifier.Text = transaction.Id;
            Date.Text = Formatter.DateAt(transaction.Date);

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
                FromHeader.Text = Strings.StarsTransactionRecipient;

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
                    Photo.SetUser(clientService, user, 120);
                }
            }
            else
            {
                FromPhoto.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                FromPhoto.Visibility = Visibility.Collapsed;
                FromTitle.Text = Strings.StarsTransactionUnsupported;
                FromHeader.Text = Strings.StarsTransactionSource;

                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
            }

            Identifier.Text = stars.TransactionId;
            Date.Text = Formatter.DateAt(receipt.Date);

            StarCount.Text = (stars.StarCount < 0 ? string.Empty : "+") + stars.StarCount.ToString("N0");
            StarCount.Foreground = BootStrapper.Current.Resources[stars.StarCount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

            Refund.Visibility = Visibility.Collapsed;
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private async void ShareLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            await _navigationService.ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostLink(new HttpUrl("https://")));
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
            File file;
            if (media is PaidMediaPhoto photo)
            {
                file = photo.Photo.GetSmall()?.Photo;
            }
            else if (media is PaidMediaVideo video)
            {
                file = video.Video.Thumbnail.File;
            }
            else
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

            ToastPopup.Show(Strings.StarsTransactionIDCopied, new LocalFileSource("ms-appx:///Assets/Toasts/Copied.tgs"));
        }

        private async void MediaPreview_Click(object sender, RoutedEventArgs e)
        {
            GalleryMedia item = null;
            GalleryMedia Filter(PaidMedia x)
            {
                GalleryMedia result = null;
                if (x is PaidMediaPhoto photo)
                {
                    result = new GalleryPhoto(_clientService, photo.Photo, true);
                }
                else if (x is PaidMediaVideo video)
                {
                    result = new GalleryVideo(_clientService, video.Video, true);
                }

                item ??= result;
                return result;
            }

            if (_transaction.Partner is not StarTransactionPartnerChannel channel)
            {
                return;
            }

            var items = channel.Media
                .Select(Filter)
                .Where(x => x is not null)
                .ToList();

            var storageService = TypeResolver.Current.Resolve<IStorageService>(_clientService.SessionId);
            var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);

            var viewModel = new StandaloneGalleryViewModel(_clientService, storageService, aggregator, items, item);

            viewModel.NavigationService = _navigationService;
            await GalleryWindow.ShowAsync(viewModel, () => Media1);
        }
    }
}
