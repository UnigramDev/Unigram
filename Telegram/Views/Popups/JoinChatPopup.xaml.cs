//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class JoinChatPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public JoinChatPopup(IClientService clientService, ChatInviteLinkInfo info)
        {
            InitializeComponent();

            _clientService = clientService;

            Photo.SetChat(clientService, info, 72);

            Title.Text = info.Title;
            Subtitle.Text = ConvertCount(info.MemberCount, info.MemberUserIds.Count == 0);

            PrimaryButtonText = Strings.ChannelJoin;
            SecondaryButtonText = Strings.Cancel;

            if (info.MemberUserIds.Count > 0)
            {
                FooterPanel.Visibility = ConvertMoreVisibility(info.MemberCount, info.MemberUserIds.Count);
                Footer.Text = ConvertMore(info.MemberCount, info.MemberUserIds.Count);

                Members.Visibility = Visibility.Visible;
                Members.ItemsSource = clientService.GetUsers(info.MemberUserIds);
            }
            else
            {
                Members.Visibility = Visibility.Collapsed;
            }
        }

        public string ConvertCount(int total, bool broadcast)
        {
            return Locale.Declension(broadcast ? "Subscribers" : "Members", total);
        }

        public Visibility ConvertMoreVisibility(int total, int count)
        {
            return total - count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public string ConvertMore(int total, int count)
        {
            return string.Format("+{0}", total - count);
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var user = args.Item as User;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(_clientService, user, 48);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
