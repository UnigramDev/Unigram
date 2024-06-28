//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ReceiptPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly string _transactionId;

        private long _thumbnailToken;

        public ReceiptPopup(IClientService clientService, INavigationService navigationService, StarTransaction transaction)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

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
            }
            else if (transaction.Partner is StarTransactionPartnerBot sourceBot && clientService.TryGetUser(sourceBot.BotUserId, out User user))
            {
                FromPhoto.SetUser(clientService, user, 24);
                FromPhoto.Visibility = Visibility.Visible;
                FromTitle.Text = user.FullName();
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
                        Photo.SetUser(clientService, user, 120);
                    }
                }
            }
            else if (transaction.Partner is StarTransactionPartnerChannel sourceChannel && clientService.TryGetChat(sourceChannel.ChatId, out Chat chat))
            {
                // TODO:
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

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_transactionId);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(Strings.StarsTransactionIDCopied, new LocalFileSource("ms-appx:///Assets/Toasts/Copied.tgs"));
        }
    }
}
