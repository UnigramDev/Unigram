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
    public sealed partial class OAuthMatchCode
    {
        public OAuthMatchCode(string code, AnimatedImageSource source)
        {
            Code = code;
            Source = source;
        }

        public string Code { get; }

        /// <summary>
        /// The emoji's animation, or null for a code that is not an emoji, which shows as text.
        /// </summary>
        public AnimatedImageSource Source { get; }

        public bool IsText => Source == null;
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

            var emoji = true;

            foreach (var code in info.MatchCodes)
            {
                emoji &= Emoji.ContainsSingleEmoji(code);
            }

            var codes = new List<OAuthMatchCode>(info.MatchCodes.Count);

            foreach (var code in info.MatchCodes)
            {
                codes.Add(new OAuthMatchCode(code, emoji ? new AnimatedEmojiFileSource(clientService, code) : null));
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
        }

        public string MatchCode { get; private set; }

        public bool IsFailed { get; private set; }

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
