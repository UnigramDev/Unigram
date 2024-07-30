using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Revenue
{
    public sealed partial class StarTransactionCell : Grid
    {
        public StarTransactionCell()
        {
            InitializeComponent();
        }

        private long _media1Token;
        private long _media2Token;

        public void UpdateInfo(IClientService clientService, StarTransaction transaction)
        {
            UpdateManager.Unsubscribe(this, ref _media1Token, true);
            UpdateManager.Unsubscribe(this, ref _media2Token, true);

            if (transaction.Partner is StarTransactionPartnerTelegram)
            {
                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                Title.Text = Strings.StarsTransactionBot;
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerFragment)
            {
                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                if (transaction.IsFragmentWithdrawal())
                {
                    Title.Text = Strings.StarsTransactionWithdrawFragment;
                }
                else
                {
                    Title.Text = Strings.StarsTransactionFragment;
                }
            }
            else if (transaction.Partner is StarTransactionPartnerAppStore or StarTransactionPartnerGooglePlay)
            {
                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Photo.Source = new PlaceholderImage(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
                Title.Text = Strings.StarsTransactionInApp;
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerBot sourceBot && clientService.TryGetUser(sourceBot.UserId, out User botUser))
            {
                if (sourceBot.ProductInfo != null)
                {
                    Title.Text = sourceBot.ProductInfo.Title;
                    Subtitle.Text = botUser.FullName();
                    Subtitle.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    Title.Text = botUser.FullName();
                    Subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }

                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Photo.SetUser(clientService, botUser, 36);
            }
            else if (transaction.Partner is StarTransactionPartnerUser sourceUser && clientService.TryGetUser(sourceUser.UserId, out User user))
            {
                Title.Text = transaction.StarCount < 0
                    ? Strings.StarsGiftSent
                    : Strings.StarsGiftReceived;
                Subtitle.Text = user.FullName();
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Visible;

                Photo.SetUser(clientService, user, 36);

                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else if (transaction.Partner is StarTransactionPartnerChannel sourceChannel && clientService.TryGetChat(sourceChannel.ChatId, out Chat chat))
            {
                Title.Text = Strings.StarMediaPurchase;
                Subtitle.Text = chat.Title;
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Visible;

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
                }
            }
            else
            {
                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Photo.Source = PlaceholderImage.GetGlyph(Icons.QuestionCircle, long.MinValue);
                Title.Text = Strings.StarsTransactionUnsupported;
                Subtitle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            Date.Text = Formatter.DateAt(transaction.Date);

            if (transaction.IsFragmentWithdrawal())
            {
                if (transaction.IsRefund)
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsRefunded);
                }
                else if (transaction.Partner is StarTransactionPartnerFragment { WithdrawalState: RevenueWithdrawalStateFailed })
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsFailed);
                }
                else if (transaction.Partner is StarTransactionPartnerFragment { WithdrawalState: RevenueWithdrawalStatePending })
                {
                    Date.Text += string.Format(" — {0}", Strings.StarsPending);
                }
            }

            StarCount.Text = (transaction.StarCount < 0 ? string.Empty : "+") + transaction.StarCount.ToString("N0");
            StarCount.Foreground = BootStrapper.Current.Resources[transaction.StarCount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
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
    }
}
