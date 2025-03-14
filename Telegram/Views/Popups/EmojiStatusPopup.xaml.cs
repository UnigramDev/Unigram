//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;

namespace Telegram.Views.Popups
{
    public sealed partial class EmojiStatusPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly StarTransaction _transaction;

        private readonly long _customEmojiId;
        private readonly int _expirationDate;

        public EmojiStatusPopup(IClientService clientService, INavigationService navigationService, long sourceUserId, Sticker sticker, int expirationDate)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _customEmojiId = customEmoji.CustomEmojiId;
                _expirationDate = expirationDate;
            }

            Animated.Source = new DelayedFileSource(clientService, sticker);
            Status.Source = new DelayedFileSource(clientService, sticker);

            if (clientService.TryGetUser(sourceUserId, out User user))
            {
                var diff = expirationDate - DateTime.Now.ToTimestamp();
                if (diff > 0)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.BotEmojiStatusTextFor, user.FirstName, Locale.FormatTtl(diff)));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.BotEmojiStatusText, user.FirstName));
                }
            }

            if (clientService.TryGetUser(clientService.Options.MyId, out User self))
            {
                Photo.SetUser(clientService, self, 28);
                TitleText.Text = self.FullName();
            }
        }

        private bool _submitted;

        private async void Purchase_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_submitted)
            {
                return;
            }

            _submitted = true;

            PurchaseRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

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

            if (_clientService.IsPremium)
            {
                var result = await _clientService.SendAsync(new SetEmojiStatus(new EmojiStatus(new EmojiStatusTypeCustomEmoji(_customEmojiId), _expirationDate)));
                Hide(result is Ok
                    ? ContentDialogResult.Primary
                    : ContentDialogResult.Secondary);
            }
            else
            {
                Hide();
                _navigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureEmojiStatus()));
            }
        }
    }
}
