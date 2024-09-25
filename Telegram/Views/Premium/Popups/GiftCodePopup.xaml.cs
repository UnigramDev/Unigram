//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class GiftCodePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly string _giftCode;

        public GiftCodePopup(IClientService clientService, INavigationService navigationService, PremiumGiftCodeInfo info, string code)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _giftCode = code;

            // TODO:
            Link.Text = "t.me/giftcode/" + code;

            clientService.TryGetUser(info.UserId, out User user);

            if (info.UseDate == 0)
            {
                Title.Text = Strings.BoostingGiftLink;
                TextBlockHelper.SetMarkdown(Subtitle, user == null
                    ? Strings.BoostingLinkAllowsAnyone
                    : user.Id == clientService.Options.MyId
                    ? Strings.BoostingLinkAllows
                    : string.Format(Strings.BoostingLinkAllowsToUser, user.FullName()));

                var footer = Strings.BoostingSendLinkToFriends;

                var markdown = ClientEx.ParseMarkdown(footer);
                if (markdown.Entities.Count == 1)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = markdown.Text.Substring(markdown.Entities[0].Offset, markdown.Entities[0].Length) });
                    hyperlink.Click += ShareLink_Click;

                    Footer.Inlines.Add(new Run { Text = markdown.Text.Substring(0, markdown.Entities[0].Offset) });
                    Footer.Inlines.Add(hyperlink);
                    Footer.Inlines.Add(new Run { Text = markdown.Text.Substring(markdown.Entities[0].Offset + markdown.Entities[0].Length) });
                }
                else
                {
                    Footer.Text = markdown.Text;
                }

                PurchaseCommand.Content = Strings.BoostingUseLink;
            }
            else
            {
                Title.Text = Strings.BoostingUsedGiftLink;
                TextBlockHelper.SetMarkdown(Subtitle, Strings.BoostingLinkUsed);

                Footer.Text = string.Format(Strings.BoostingUsedLinkDate, Formatter.DateAt(info.UseDate));

                PurchaseCommand.Content = Strings.OK;
            }

            if (clientService.TryGetChat(info.CreatorId, out var creatorChat))
            {
                FromPhoto.SetChat(clientService, creatorChat, 24);
                FromTitle.Text = creatorChat.Title;
            }
            else if (clientService.TryGetUser(info.CreatorId, out var creatorUser))
            {
                FromPhoto.SetUser(clientService, creatorUser, 24);
                FromTitle.Text = creatorUser.FullName();
            }

            if (user != null)
            {
                ToPhoto.SetUser(clientService, user, 24);
                ToTitle.Text = user.FullName();
            }
            else
            {
                To.Visibility = Visibility.Collapsed;
            }

            Gift.Content = string.Format(Strings.BoostingTelegramPremiumFor, Locale.Declension(Strings.R.Months, info.MonthCount));

            if (info.IsFromGiveaway)
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run
                {
                    Text = info.UserId == 0
                        ? Strings.BoostingIncompleteGiveaway
                        : Strings.BoostingGiveaway
                });

                var textBlock = new TextBlock();
                textBlock.Inlines.Add(hyperlink);

                Reason.Content = textBlock;
            }
            else
            {
                Reason.Content = Strings.BoostingYouWereSelected;
            }

            Date.Content = Formatter.DateAt(info.CreationDate);
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.DropShadow(PurchaseShadow);
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            Hide();

            var response = await _clientService.SendAsync(new ApplyPremiumGiftCode(_giftCode));
            if (response is Ok)
            {
                _navigationService.ShowPromo();
            }
            else
            {
                // TODO:
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(_clientService, XamlRoot, new InternalLinkTypePremiumGiftCode(_giftCode));
        }

        private async void ShareLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new HttpUrl("https://")));
        }
    }
}
