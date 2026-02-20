//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

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
                Photo.Source = ProfilePictureSource.User(clientService, self);
                TitleText.Text = self.FullName();
            }

            PrimaryButtonText = Strings.BotEmojiStatusConfirm;
        }

        private bool _submitted;
        private bool _completed;

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = !_completed;

            if (_submitted)
            {
                return;
            }

            _submitted = true;
            IsPrimaryButtonPending = true;

            if (_clientService.IsPremium)
            {
                var result = await _clientService.SendAsync(new SetEmojiStatus(new EmojiStatus(new EmojiStatusTypeCustomEmoji(_customEmojiId), _expirationDate)));

                _completed = true;
                Hide(result is Ok
                    ? ContentDialogResult.Primary
                    : ContentDialogResult.Secondary);
            }
            else
            {
                _completed = true;
                Hide();
                _navigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureEmojiStatus()));
            }
        }
    }
}
