//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class FoundAffiliateProgramPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly FoundAffiliateProgram _program;

        private AffiliateType _selectedType;

        private readonly ObservableCollection<AffiliateType> _items;

        public FoundAffiliateProgramPopup(IClientService clientService, INavigationService navigationService, FoundAffiliateProgram program, AffiliateType affiliateType)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _program = program;

            _items = new ObservableCollection<AffiliateType>();

            InitializeOwnedChats();
            UpdateAlias(affiliateType);

            TableRoot.Visibility = program.Info.DailyRevenuePerUserAmount != null
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (clientService.TryGetUser(program.BotUserId, out User botUser) && botUser.Type is UserTypeBot userTypeBot)
            {
                var percent = program.Info.Parameters.CommissionPercent();
                var duration = program.Info.Parameters.MonthCount > 0
                    ? program.Info.Parameters.MonthCount >= 12
                    ? Locale.Declension(Strings.R.ChannelAffiliateProgramJoinText_Years, program.Info.Parameters.MonthCount / 12)
                    : Locale.Declension(Strings.R.ChannelAffiliateProgramJoinText_Months, program.Info.Parameters.MonthCount)
                    : Strings.ChannelAffiliateProgramJoinText_Lifetime;

                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.ChannelAffiliateProgramJoinText, botUser.FirstName, percent, duration));

                Photo1.Source = ProfilePictureSource.User(clientService, botUser);

                MonthlyUsers.Content = userTypeBot.ActiveUserCount;
            }

            StarCount.Text = program.Info.DailyRevenuePerUserAmount.ToValue();
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

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.ChannelAffiliateProgramJoinButtonInfoLink);
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

        private void UpdateAlias(AffiliateType type)
        {
            if (_selectedType.AreTheSame(type))
            {
                return;
            }

            _selectedType = type;

            if (_clientService.TryGetUser(type, out User senderUser))
            {
                Photo2.Source = ProfilePictureSource.User(_clientService, senderUser);

                Photo.Source = ProfilePictureSource.User(_clientService, senderUser);
                TitleText.Text = senderUser.FullName();
            }
            else if (_clientService.TryGetChat(type, out Chat senderChat))
            {
                Photo2.Source = ProfilePictureSource.Chat(_clientService, senderChat);

                Photo.Source = ProfilePictureSource.Chat(_clientService, senderChat);
                TitleText.Text = senderChat.Title;
            }
        }
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

            var result = await SubmitAsync();
            if (result)
            {
                return;
            }

            _submitted = false;

            translate1.InsertKeyFrame(0, 32);
            translate1.InsertKeyFrame(1, 0);

            translate2.InsertKeyFrame(0, 0);
            translate2.InsertKeyFrame(1, -32);

            visual1.StartAnimation("Translation.Y", translate1);
            visual2.StartAnimation("Translation.Y", translate2);
        }

        private async Task<bool> SubmitAsync()
        {
            var response = await _clientService.SendAsync(new ConnectAffiliateProgram(_selectedType, _program.BotUserId));
            if (response is ConnectedAffiliateProgram program)
            {
                Hide();

                var popup = new ConnectedAffiliateProgramPopup(_clientService, _navigationService, program, _selectedType);
                var aggregator = _clientService.Session.Resolve<IEventAggregator>();

                aggregator.Publish(new UpdateChatAffiliatePrograms(_selectedType));

                void handler(object sender, object args)
                {
                    _navigationService.ShowToast(string.Format("**{0}**\n{1}", Strings.AffiliateProgramJoinedTitle, Strings.AffiliateProgramJoinedText), ToastPopupIcon.Success);
                    popup.Opened -= handler;
                }

                popup.Opened += handler;

                _navigationService.ShowPopup(popup);
                return true;
            }
            else if (response is Error error)
            {
                ToastPopup.ShowError(XamlRoot, error);
            }

            return false;
        }
    }
}
