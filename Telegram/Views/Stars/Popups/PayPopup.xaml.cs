//
// Copyright (c) Fela Ameghino 2015-2026
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

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class PayPopup : ContentPopup
    {
        public PayViewModel ViewModel => DataContext as PayViewModel;

        public PayPopup()
        {
            InitializeComponent();
        }

        private ThumbnailController _media1Controller;
        private ThumbnailController _media2Controller;

        private long _media1Token;
        private long _media2Token;

        public override void OnNavigatedTo(object parameter)
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

                UpdateMedia(ViewModel.Media[0], Thumbnail1, ref _media1Controller);

                if (ViewModel.Media.Count > 1)
                {
                    UpdateMedia(ViewModel.Media[1], Thumbnail2, ref _media1Controller);

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
                    Photo.Source = new ProfilePictureSourcePhoto(ViewModel.ClientService, user.Id, small.Photo, ViewModel.PaymentForm.ProductInfo.Photo.Minithumbnail);
                }
                else
                {
                    Photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
                }
            }

            TextBlockHelper.SetMarkdown(Subtitle, text);

            PurchaseText.Text = Locale.Declension(Strings.R.StarsConfirmPurchaseButton, stars.StarCount).ReplaceStar(Icons.Premium);
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

            Hide(result == PayResult.Succeeded
                ? ContentDialogResult.Primary
                : ContentDialogResult.Secondary);

            if (result == PayResult.StarsNeeded && ViewModel.PaymentForm?.Type is PaymentFormTypeStars stars)
            {
                await ViewModel.NavigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForSellerBotUser(stars.StarCount, ViewModel.PaymentForm.SellerBotUserId));
            }
        }

        private void UpdateMedia(PaidMedia media, ImageBrush brush, ref ThumbnailController controller)
        {
            controller ??= new ThumbnailController(brush);

            Minithumbnail minithumbnail = null;
            if (media is PaidMediaPhoto photo)
            {
                minithumbnail = photo.Photo.Minithumbnail;
            }
            else if (media is PaidMediaVideo video)
            {
                minithumbnail = video.Cover?.Minithumbnail ?? video.Video.Minithumbnail;
            }
            else if (media is PaidMediaPreview preview)
            {
                minithumbnail = preview.Minithumbnail;
            }

            if (minithumbnail != null)
            {
                controller.Blur(minithumbnail.Data, 3);
            }
            else
            {
                controller.Recycle();
            }
        }
    }
}
