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
    public sealed partial class ReactPopup : ContentPopup
    {
        public ReactViewModel ViewModel => DataContext as ReactViewModel;

        public ReactPopup()
        {
            InitializeComponent();
        }

        public override void OnNavigatedTo()
        {

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

                //if (result == PayResult.StarsNeeded && ViewModel.PaymentForm?.Type is PaymentFormTypeStars stars)
                //{
                //    await ViewModel.NavigationService.ShowPopupAsync(typeof(BuyPopup), new BuyStarsArgs(stars.StarCount, ViewModel.PaymentForm.SellerBotUserId));
                //}

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
    }
}
