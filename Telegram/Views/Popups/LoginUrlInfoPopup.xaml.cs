//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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
    public sealed partial class LoginUrlInfoPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly LoginUrlInfoRequestConfirmation _requestConfirmation;

        private bool _requestedPhoneNumberAccess;

        public LoginUrlInfoPopup(IClientService clientService, INavigationService navigationService, LoginUrlInfoRequestConfirmation requestConfirmation)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _requestConfirmation = requestConfirmation;

            var botUser = clientService.GetUser(requestConfirmation.BotUserId);
            if (botUser == null)
            {
                // ??
            }

            Photo.Source = ProfilePictureSource.User(clientService, botUser);

            if (LifetimeService.Current.Count > 1
                && clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                AliasRoot.Visibility = Visibility.Visible;
                Alias.Source = ProfilePictureSource.User(clientService, user);
            }

            var markdown = ClientEx.ParseMarkdown(string.Format(Strings.BotAuthTitle, requestConfirmation.Domain));
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

            if (string.IsNullOrEmpty(requestConfirmation.Domain))
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BotAuthBotSubtitle);
            }
            else
            {
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BotAuthSiteSubtitle);
            }

            if (string.IsNullOrEmpty(requestConfirmation.Platform) || string.IsNullOrEmpty(requestConfirmation.Browser))
            {
                Application.Visibility = Visibility.Collapsed;
            }
            else
            {
                Application.Title = requestConfirmation.Platform;
                Application.Subtitle = requestConfirmation.Browser;
            }

            if (string.IsNullOrEmpty(requestConfirmation.Location) || string.IsNullOrEmpty(requestConfirmation.IpAddress))
            {
                Location.Visibility = Visibility.Collapsed;
            }
            else
            {
                Location.Title = requestConfirmation.Location;
                Location.Subtitle = string.Format(Strings.BotAuthBasedOnIP, requestConfirmation.IpAddress);
            }

            if (requestConfirmation.RequestWriteAccess)
            {
                AllowMessagesInfo.Text = string.Format(Strings.BotAuthAllowMessagesInfo, botUser.FullName());
            }
            else
            {
                AllowMessagesRoot.Visibility = Visibility.Collapsed;
            }

            PrimaryButtonText = Strings.BotAuthLogin;
            SecondaryButtonText = Strings.Cancel;
        }

        public bool AllowWriteAccess => AllowMessages.IsChecked is true;

        public bool AllowPhoneNumberAccess { get; private set; }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_requestConfirmation.RequestPhoneNumberAccess && !_requestedPhoneNumberAccess)
            {
                _requestedPhoneNumberAccess = true;
                args.Cancel = true;

                var user = _clientService.GetUser(_clientService.Options.MyId);
                var phoneNumber = PhoneNumber.Format(user.PhoneNumber);

                var confirm = await _navigationService.ShowPopupAsync(string.Format(Strings.BotAuthPhoneNumberText, _requestConfirmation.Domain, phoneNumber), Strings.BotAuthPhoneNumber, Strings.BotAuthPhoneNumberAccept, Strings.BotAuthPhoneNumberDeny);
                if (confirm == ContentDialogResult.Primary)
                {
                    AllowPhoneNumberAccess = true;
                }

                Hide(ContentDialogResult.Primary);
            }
        }

        private void Alias_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            foreach (var session in LifetimeService.Current.Items)
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

        private void Account_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
