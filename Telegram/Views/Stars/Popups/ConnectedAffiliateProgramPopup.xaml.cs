//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class ConnectedAffiliateProgramPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly ConnectedAffiliateProgram _program;

        private AffiliateType _selectedType;

        private readonly ObservableCollection<AffiliateType> _items;

        public ConnectedAffiliateProgramPopup(IClientService clientService, INavigationService navigationService, ConnectedAffiliateProgram program, AffiliateType affiliateType)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _program = program;

            _selectedType = affiliateType;

            _clientService.Send(new GetUserFullInfo(program.BotUserId));
            _items = new ObservableCollection<AffiliateType>();

            InitializeOwnedChats();

            Photo1.Source = ProfilePictureSourceText.GetGlyph(Icons.LinkDiagonal);

            Link.Text = program.Url.Replace("https://", string.Empty);

            _selectedType = affiliateType;

            if (_clientService.TryGetUser(affiliateType, out User senderUser))
            {
                Photo.Source = ProfilePictureSource.User(_clientService, senderUser);
                TitleText.Text = senderUser.FullName();
            }
            else if (_clientService.TryGetChat(affiliateType, out Chat senderChat))
            {
                Photo.Source = ProfilePictureSource.Chat(_clientService, senderChat);
                TitleText.Text = senderChat.Title;
            }

            if (_clientService.TryGetUser(_program.BotUserId, out User botUser) && botUser.Type is UserTypeBot userTypeBot)
            {
                var percent = _program.Parameters.CommissionPercent();
                var duration = _program.Parameters.MonthCount > 0
                    ? _program.Parameters.MonthCount >= 12
                    ? Locale.Declension(Strings.R.ChannelAffiliateProgramJoinText_Years, _program.Parameters.MonthCount / 12)
                    : Locale.Declension(Strings.R.ChannelAffiliateProgramJoinText_Months, _program.Parameters.MonthCount)
                    : Strings.ChannelAffiliateProgramJoinText_Lifetime;

                var message = affiliateType is AffiliateTypeChannel
                    ? Strings.ChannelAffiliateProgramLinkTextChannel
                    : Strings.ChannelAffiliateProgramLinkTextUser;

                TextBlockHelper.SetMarkdown(Subtitle, string.Format(message, percent, botUser.FirstName, duration));

                Usage.Text = program.UserCount > 0
                    ? Locale.Declension(Strings.R.ChannelAffiliateProgramLinkOpened, program.UserCount, botUser.FirstName)
                    : string.Format(Strings.ChannelAffiliateProgramLinkOpenedNone, botUser.FirstName);
            }
        }

        private async void InitializeOwnedChats()
        {
            _items.Add(new AffiliateTypeCurrentUser());

            var response1 = await _clientService.SendAsync(new GetOwnedBots());
            if (response1 is Td.Api.Users users)
            {
                foreach (var userId in users.UserIds)
                {
                    _items.Add(new AffiliateTypeBot(userId));
                }
            }

            var response2 = await _clientService.SendAsync(new GetCreatedPublicChats(new PublicChatTypeHasUsername()));
            if (response2 is Td.Api.Chats chats)
            {
                foreach (var chatId in chats.ChatIds)
                {
                    _items.Add(new AffiliateTypeChannel(chatId));
                }
            }
        }

        private void Alias_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            void handler(object sender, RoutedEventArgs _)
            {
                if (sender is MenuFlyoutItem item && item.CommandParameter is AffiliateType type)
                {
                    item.Click -= handler;
                    UpdateAlias(type);
                }
            }

            foreach (var type in _items)
            {
                var picture = new ProfilePicture();
                picture.Size = 36;
                picture.Margin = new Thickness(-4, -2, 0, -2);

                var item = new MenuFlyoutProfile();
                item.Click += handler;
                item.CommandParameter = type;
                item.Style = BootStrapper.Current.Resources["SendAsMenuFlyoutItemStyle"] as Style;
                item.Icon = new FontIcon();
                item.Tag = picture;

                if (_clientService.TryGetUser(type, out User senderUser))
                {
                    picture.Source = ProfilePictureSource.User(_clientService, senderUser);

                    item.Text = senderUser.FullName();
                    item.Info = senderUser.Type is UserTypeRegular
                        ? Strings.VoipGroupPersonalAccount
                        : Strings.Bot;
                }
                else if (_clientService.TryGetChat(type, out Chat senderChat))
                {
                    picture.Source = ProfilePictureSource.Chat(_clientService, senderChat);

                    item.Text = senderChat.Title;
                    item.Info = Strings.DiscussChannel;
                }

                flyout.Items.Add(item);
            }

            flyout.ShowAt(Title, FlyoutPlacementMode.Bottom);
        }

        private async void UpdateAlias(AffiliateType type)
        {
            if (_selectedType.AreTheSame(type))
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetConnectedAffiliateProgram(type, _program.BotUserId));
            if (response is ConnectedAffiliateProgram program)
            {
                Hide();
                _navigationService.ShowPopup(new ConnectedAffiliateProgramPopup(_clientService, _navigationService, _program, type));
            }
            else if (_clientService.TryGetUserFull(_program.BotUserId, out UserFullInfo fullInfo))
            {
                Hide();
                _navigationService.ShowPopup(new FoundAffiliateProgramPopup(_clientService, _navigationService, new FoundAffiliateProgram(_program.BotUserId, fullInfo.BotInfo.AffiliateProgram), type));
            }
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(XamlRoot, _program.Url);
        }
    }
}
