//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Calls.Popups
{
    public sealed partial class ShareGroupCallPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly GroupCall _groupCall;

        public ShareGroupCallPopup(IClientService clientService, INavigationService navigationService, GroupCall groupCall)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _groupCall = groupCall;

            Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.LinkDiagonal);
            InitializeInviteLink();
        }

        private async void InitializeInviteLink()
        {
            var response = await _clientService.SendAsync(new GetInternalLink(new InternalLinkTypeGroupCall(_groupCall.InviteLink), true));
            if (response is HttpUrl httpUrl)
            {
                InviteLink.Content = httpUrl.Url.Replace("https://", string.Empty);
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(RevokeLink, Strings.RevokeLink, Icons.Delete, destructive: true);
            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void RevokeLink()
        {
            Hide();
            _clientService.Send(new RevokeGroupCallInviteLink(_groupCall.Id));
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(_clientService, XamlRoot, new InternalLinkTypeGroupCall(_groupCall.InviteLink));
        }

        private void ShareLink_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeGroupCall(_groupCall.InviteLink)));
        }

        private void Join_Click(object sender, TextUrlClickEventArgs e)
        {
            Hide();
            _clientService.Session.Resolve<IVoipService>().JoinGroupCall(_navigationService, new InputGroupCallLink(_groupCall.InviteLink));
        }
    }
}
