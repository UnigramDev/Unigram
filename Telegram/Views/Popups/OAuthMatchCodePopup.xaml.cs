//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class OAuthMatchCode : BindableBase
    {
        public OAuthMatchCode(string code)
        {
            Code = code;
        }

        public string Code { get; }

        private AnimatedImageSource _source;
        public AnimatedImageSource Source
        {
            get => _source;
            set => Set(ref _source, value);
        }

        private bool _isGlyphVisible = true;
        public bool IsGlyphVisible
        {
            get => _isGlyphVisible;
            set => Set(ref _isGlyphVisible, value);
        }
    }

    /// <summary>
    /// The codes of a login, to pick the one the login screen shows.
    /// </summary>
    /// <remarks>
    /// Asked either before the request is shown, in which case the pick is checked here and a
    /// wrong one ends the request, or after the user confirmed it, in which case the pick goes
    /// with the acceptance and cancelling goes back to the confirmation.
    /// </remarks>
    public sealed partial class OAuthMatchCodePopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly string _url;
        private readonly bool _check;

        public OAuthMatchCodePopup(IClientService clientService, string url, OauthLinkInfo info, string name, bool check)
        {
            InitializeComponent();

            _clientService = clientService;
            _url = url;
            _check = check;

            var codes = new List<OAuthMatchCode>(info.MatchCodes.Count);
            var emoji = true;

            foreach (var code in info.MatchCodes)
            {
                codes.Add(new OAuthMatchCode(code));
                emoji &= Emoji.ContainsSingleEmoji(code);
            }

            Codes.ItemsSource = codes;

            Title.Text = emoji
                ? Strings.BotAuthSelectEmoji
                : Strings.BotAuthSelectCode;

            TextBlockHelper.SetMarkdown(Footer, string.Format(Strings.BotAuthLoginRequestFrom, name));

            // Declining is final, so it is only offered where there is nothing to go back to.
            if (check)
            {
                SecondaryButtonText = Strings.Decline;
            }
            else
            {
                CloseButtonText = Strings.Cancel;
            }

            if (emoji)
            {
                LoadAnimations(codes);
            }
        }

        public string MatchCode { get; private set; }

        public bool IsFailed { get; private set; }

        private async void LoadAnimations(List<OAuthMatchCode> codes)
        {
            var response = await _clientService.SendAsync(new SearchStickerSet("RestrictedEmoji", false));
            if (response is not StickerSet stickerSet)
            {
                return;
            }

            foreach (var code in codes)
            {
                var emoji = code.Code.Replace("️", string.Empty);

                foreach (var sticker in stickerSet.Stickers)
                {
                    if (string.Equals(sticker.Emoji.Replace("️", string.Empty), emoji))
                    {
                        code.Source = new DelayedFileSource(_clientService, sticker);
                        break;
                    }
                }
            }
        }

        private void Animation_Ready(object sender, EventArgs e)
        {
            // The emoji stays until the animation has a frame to show in its place.
            if (sender is FrameworkElement { DataContext: OAuthMatchCode code })
            {
                code.IsGlyphVisible = false;
            }
        }

        private async void Code_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OAuthMatchCode code } || !Codes.IsEnabled)
            {
                return;
            }

            if (_check)
            {
                Codes.IsEnabled = false;

                var response = await _clientService.SendAsync(new CheckOauthRequestMatchCode(_url, code.Code));
                if (response is not Ok)
                {
                    IsFailed = true;
                    Hide();
                    return;
                }
            }

            MatchCode = code.Code;
            Hide();
        }

        private void OnSecondaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            _clientService.Send(new DeclineOauthRequest(_url));
        }
    }
}
