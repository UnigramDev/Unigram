//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Stars;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class PayPopup : ContentPopup
    {
        public PayViewModel ViewModel => DataContext as PayViewModel;

        public PayPopup()
        {
            InitializeComponent();
        }

        private long _thumbnailToken;

        private long _media1Token;
        private long _media2Token;

        public override void OnNavigatedTo()
        {
            if (ViewModel.PaymentForm?.Type is not PaymentFormTypeStars stars || !ViewModel.ClientService.TryGetUser(ViewModel.PaymentForm.SellerBotUserId, out User user))
            {
                return;
            }

            string text;

            if (ViewModel.Media?.Count > 0 && ViewModel.ClientService.TryGetChat(ViewModel.ChatId, out Chat chat))
            {
                var photos = ViewModel.Media.Count(x => x.IsPhoto());
                var videos = ViewModel.Media.Count - photos;

                string photosText = photos == 1 ? Strings.StarsConfirmPurchaseMedia_SinglePhoto : Locale.Declension(Strings.R.StarsConfirmPurchaseMedia_Photos, photos);
                string videosText = videos == 1 ? Strings.StarsConfirmPurchaseMedia_SingleVideo : Locale.Declension(Strings.R.StarsConfirmPurchaseMedia_Videos, videos);

                if (photos == 0)
                {
                    text = string.Format(Strings.StarsConfirmPurchaseMedia1, videosText, chat.Title, Locale.Declension(Strings.R.StarsCount, stars.StarCount).ToLower());
                }
                else if (videos == 0)
                {
                    text = string.Format(Strings.StarsConfirmPurchaseMedia1, photosText, chat.Title, Locale.Declension(Strings.R.StarsCount, stars.StarCount).ToLower());
                }
                else
                {
                    text = string.Format(Strings.StarsConfirmPurchaseMedia2, photosText, videosText, chat.Title, Locale.Declension(Strings.R.StarsCount, stars.StarCount).ToLower());
                }

                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Visible;
                Particles.Source = new ParticlesImageSource();

                UpdateMedia(ViewModel.Media[0], Media1);

                if (ViewModel.Media.Count > 1)
                {
                    UpdateMedia(ViewModel.Media[1], Media2);

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
                text = Locale.Declension(Strings.R.StarsConfirmPurchaseText, stars.StarCount, ViewModel.PaymentForm.ProductInfo.Title, user.FullName());

                MediaPreview.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                var small = ViewModel.PaymentForm.ProductInfo.Photo?.GetSmall();
                if (small != null)
                {
                    UpdateManager.Subscribe(this, ViewModel.ClientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                    UpdateThumbnail(ViewModel.PaymentForm, small.Photo);
                }
                else
                {
                    Photo.SetUser(ViewModel.ClientService, user, 120);
                }
            }

            TextBlockHelper.SetMarkdown(Subtitle, text);

            PurchaseText.Text = Locale.Declension(Strings.R.StarsConfirmPurchaseButton, stars.StarCount).Replace("\u2B50", Icons.Premium);
        }

        public string ConvertCount(long count)
        {
            return count.ToString("N0");
        }

        private bool _submitted;

        private async void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            PurchaseRing.Visibility = Windows.UI.Xaml.Visibility.Visible;

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

            var result = await ViewModel.SubmitAsync();
            if (result != PayResult.Failed)
            {
                Hide(result == PayResult.Succeeded
                    ? ContentDialogResult.Primary
                    : ContentDialogResult.Secondary);

                if (result == PayResult.StarsNeeded && ViewModel.PaymentForm?.Type is PaymentFormTypeStars stars)
                {
                    await ViewModel.NavigationService.ShowPopupAsync(typeof(BuyPopup), new BuyStarsArgs(stars.StarCount, ViewModel.PaymentForm.SellerBotUserId));
                }

                return;
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

        private void UpdateFile(object target, File file)
        {
            UpdateFile(ViewModel.PaymentForm, file);
        }

        private void UpdateFile(PaymentForm paymentForm, File file)
        {
            var small = paymentForm.ProductInfo.Photo?.GetSmall();
            if (small != null && (file == null || small.Photo.Id == file.Id))
            {
                UpdateThumbnail(paymentForm, small.Photo);
            }
        }

        private void UpdateThumbnail(PaymentForm paymentForm, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Photo.Source = UriEx.ToBitmap(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                ViewModel.ClientService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateMedia(PaidMedia media, Grid target)
        {
            BitmapImage source = null;
            ImageBrush brush;

            if (target.Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                target.Background = brush;
            }

            Minithumbnail minithumbnail = null;
            if (media is PaidMediaPhoto photo)
            {
                minithumbnail = photo.Photo.Minithumbnail;
            }
            else if (media is PaidMediaVideo video)
            {
                minithumbnail = video.Video.Minithumbnail;
            }
            else if (media is PaidMediaPreview preview)
            {
                minithumbnail = preview.Minithumbnail;
            }

            if (minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
            }

            brush.ImageSource = source;
        }
    }
}
