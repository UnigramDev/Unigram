//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class SubscribeChannelPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        private readonly string _inviteLink;
        private readonly string _title;
        private readonly ChatInviteLinkSubscriptionInfo _subscription;

        public SubscribeChannelPopup(IClientService clientService, string inviteLink, ChatInviteLinkInfo info)
        {
            InitializeComponent();

            _clientService = clientService;

            _inviteLink = inviteLink;
            _title = info.Title;
            _subscription = info.SubscriptionInfo;

            Balance.Text = Icons.PremiumStar + " " + clientService.OwnedStarCount.ToValue();
            Photo.Source = ProfilePictureSource.Chat(clientService, info);

            TextBlockHelper.SetMarkdown(Subtitle, Locale.Declension(Strings.R.StarsSubscribeText, _subscription.Pricing.StarCount, info.Title));

            if (string.IsNullOrEmpty(info.Description))
            {
                Description.Visibility = Visibility.Collapsed;
            }
            else
            {
                Description.Text = info.Description;
            }
        }

        public bool IsStarsNeeded { get; private set; }

        private bool _submitted;

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            PurchaseRing.Visibility = Visibility.Visible;

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

            var starCount = _subscription.Pricing.StarCount;
            var function = new SendPaymentForm(new InputInvoiceTelegram(new TelegramPaymentPurposeJoinChat(_inviteLink)), _subscription.FormId, string.Empty, string.Empty, null, 0);

            var response = await _clientService.SendPaymentAsync(starCount, function);
            if (response is PaymentResult { Success: true })
            {
                var aggregator = _clientService.Session.Resolve<IEventAggregator>();
                aggregator.Publish(new UpdateConfetti());

                var text = Locale.Declension(Strings.R.StarsSubscriptionCompletedText, starCount, _title);
                ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.StarsSubscriptionCompleted, text), ToastPopupIcon.Success);

                Hide(ContentDialogResult.Primary);
                return;
            }
            else if (response is ErrorStarsNeeded)
            {
                IsStarsNeeded = true;
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            Hide(ContentDialogResult.Secondary);
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsSubscribeInfoLink);
        }
    }
}
