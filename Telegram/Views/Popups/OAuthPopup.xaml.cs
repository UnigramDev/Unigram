//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Popups
{
    /// <summary>
    /// The confirmation of a login, which also asks for whatever else the request wants and
    /// accepts it.
    /// </summary>
    /// <remarks>
    /// Closing it any way but Decline leaves the request alone, to expire on its own: only Decline
    /// tells the server no.
    /// </remarks>
    public sealed partial class OAuthPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly string _url;
        private readonly OauthLinkInfo _info;
        private readonly string _name;
        private readonly string _matchCode;
        private readonly TonConnectConnectData _connectData;

        public OAuthPopup(IClientService clientService, INavigationService navigationService, string url, OauthLinkInfo info, string matchCode, TonConnectConnectData connectData)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _url = url;
            _info = info;
            _name = GetName(info);
            _matchCode = matchCode;
            _connectData = connectData;

            if (clientService.TryGetUser(info.BotUserId, out User botUser))
            {
                Photo.Source = ProfilePictureSource.User(clientService, botUser);
            }

            if (GetSessions(clientService).Count > 1
                && clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                AliasRoot.Visibility = Visibility.Visible;
                Alias.Source = ProfilePictureSource.User(clientService, user);
            }

            var markdown = ClientEx.ParseMarkdown(string.Format(Strings.BotAuthTitle, _name));
            if (markdown.Entities.Count == 1)
            {
                var prefix = markdown.Text.Substring(0, markdown.Entities[0].Offset);
                var suffix = markdown.Text.Substring(markdown.Entities[0].Offset + markdown.Entities[0].Length);

                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(markdown.Text.Substring(markdown.Entities[0].Offset, markdown.Entities[0].Length));
                hyperlink.UnderlineStyle = UnderlineStyle.None;

                Title.Inlines.Add(prefix);
                Title.Inlines.Add(hyperlink);
                Title.Inlines.Add(suffix);
            }
            else
            {
                Title.Text = markdown.Text;
            }

            if (info.FromApp)
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BotAuthAppSubtitle);
            }
            else if (string.IsNullOrEmpty(info.Domain))
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BotAuthBotSubtitle);
            }
            else
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BotAuthSiteSubtitle);
            }

            if (string.IsNullOrEmpty(info.Platform) || string.IsNullOrEmpty(info.Browser))
            {
                Application.Visibility = Visibility.Collapsed;
            }
            else
            {
                Application.Title = info.Platform;
                Application.Subtitle = info.Browser;
            }

            if (string.IsNullOrEmpty(info.Location) || string.IsNullOrEmpty(info.IpAddress))
            {
                Location.Visibility = Visibility.Collapsed;
            }
            else
            {
                Location.Title = info.Location;
                Location.Subtitle = string.Format(Strings.BotAuthBasedOnIP, info.IpAddress);
            }

            if (info.RequestWriteAccess)
            {
                AllowMessagesInfo.Text = string.Format(Strings.BotAuthAllowMessagesInfo, botUser?.FullName() ?? _name);
            }
            else
            {
                AllowMessagesRoot.Visibility = Visibility.Collapsed;
            }

            PrimaryButtonText = Strings.BotAuthLogin;
            SecondaryButtonText = Strings.Decline;
        }

        /// <summary>
        /// What accepting the request answered, or the error that ended it; null while it is
        /// neither accepted nor over.
        /// </summary>
        public Object Response { get; private set; }

        public bool AllowPhoneNumberAccess { get; private set; }

        /// <summary>
        /// The account the user moved the login to, with the request as that account sees it.
        /// </summary>
        public IClientService SwitchedClientService { get; private set; }

        public OauthLinkInfo SwitchedInfo { get; private set; }

        public static string GetName(OauthLinkInfo info)
        {
            if (info.FromApp)
            {
                return string.IsNullOrEmpty(info.VerifiedAppName)
                    ? Strings.UnverifiedApp
                    : info.VerifiedAppName;
            }

            return info.Domain;
        }

        /// <summary>
        /// The accounts a login can be done with: those signed in on the same servers as this one.
        /// </summary>
        public static List<ISession> GetSessions(IClientService clientService)
        {
            var sessions = new List<ISession>();
            var test = clientService.Session.Settings.UseTestDC;

            foreach (var session in LifetimeService.Current.Items)
            {
                if (session.Settings.UseTestDC == test && session.ClientService.TryGetUser(session.ClientService.Options.MyId, out User _))
                {
                    sessions.Add(session);
                }
            }

            return sessions;
        }

        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;

            try
            {
                if (IsPrimaryButtonPending)
                {
                    return;
                }

                var allowPhoneNumberAccess = false;

                if (_info.RequestPhoneNumberAccess && _clientService.TryGetUser(_clientService.Options.MyId, out User user))
                {
                    var phoneNumber = PhoneNumber.Format(user.PhoneNumber);

                    // Deny still logs in, without the number; only dismissing the question stops.
                    var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, string.Format(Strings.BotAuthPhoneNumberText, _name, phoneNumber), Strings.BotAuthPhoneNumber, Strings.BotAuthPhoneNumberAccept, Strings.BotAuthPhoneNumberDeny);
                    if (confirm == ContentDialogResult.None)
                    {
                        return;
                    }

                    allowPhoneNumberAccess = confirm == ContentDialogResult.Primary;
                }

                var matchCode = _matchCode;

                if (matchCode == null && _info.MatchCodes.Count > 0)
                {
                    var popup = new OAuthMatchCodePopup(_clientService, _url, _info, _name, false);
                    await popup.ShowAsync(XamlRoot);

                    if (popup.MatchCode == null)
                    {
                        return;
                    }

                    matchCode = popup.MatchCode;
                }

                IsPrimaryButtonPending = true;

                var response = await _clientService.SendAsync(new AcceptOauthRequest(_url, matchCode ?? string.Empty, AllowMessages.IsChecked is true, allowPhoneNumberAccess, _connectData));

                IsPrimaryButtonPending = false;

                Response = response;
                AllowPhoneNumberAccess = allowPhoneNumberAccess;

                Hide();
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnSecondaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (IsPrimaryButtonPending)
            {
                args.Cancel = true;
                return;
            }

            _clientService.Send(new DeclineOauthRequest(_url));
        }

        private void OnClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            // The answer is on its way and decides what comes after the popup.
            args.Cancel = IsPrimaryButtonPending;
        }

        private void Alias_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            foreach (var session in GetSessions(_clientService))
            {
                if (session.ClientService.TryGetUser(session.ClientService.Options.MyId, out User user))
                {
                    var photo = new ProfilePicture();
                    photo.Size = 20;
                    photo.Source = ProfilePictureSource.User(session.ClientService, user);

                    var item = new ToggleMenuFlyoutItem();
                    item.Style = BootStrapper.Current.Resources["ProfilePictureToggleMenuFlyoutItemStyle"] as Style;
                    item.IsChecked = session == _clientService.Session;
                    item.Icon = new SymbolIcon();
                    item.Text = user.FullName();
                    item.Tag = photo;
                    item.CommandParameter = session.Id;
                    item.Click += Account_Click;

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(AliasRoot, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        private async void Account_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { CommandParameter: int sessionId } || IsPrimaryButtonPending)
            {
                return;
            }

            ISession session = null;

            foreach (var item in LifetimeService.Current.Items)
            {
                if (item.Id == sessionId)
                {
                    session = item;
                    break;
                }
            }

            if (session == null || session == _clientService.Session)
            {
                return;
            }

            // The request reads differently from every account - who is logging in, what is asked
            // of them - so it is fetched again rather than carried over.
            IsPrimaryButtonPending = true;

            var response = await session.ClientService.SendAsync(new GetOauthLinkInfo(_url, string.Empty));

            IsPrimaryButtonPending = false;

            if (response is OauthLinkInfo info)
            {
                SwitchedClientService = session.ClientService;
                SwitchedInfo = info;
                Hide();
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.URL_EXPIRED))
                {
                    Response = error;
                    Hide();
                }
                else
                {
                    _navigationService.ShowToast(error);
                }
            }
        }
    }
}
