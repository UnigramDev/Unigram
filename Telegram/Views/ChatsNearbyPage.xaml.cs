//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Views
{
    public sealed partial class ChatsNearbyPage : HostedPage
    {
        public ChatsNearbyViewModel ViewModel => DataContext as ChatsNearbyViewModel;

        public ChatsNearbyPage()
        {
            InitializeComponent();
            Title = Strings.PeopleNearby;
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var nearby = button.DataContext as ChatNearby;

            var chat = ViewModel.ClientService.GetChat(nearby.ChatId);
            if (chat == null)
            {
                return;
            }

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ClientService.GetTitle(chat);

            if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = string.Format("{0}, {1}", Formatter.Distance(nearby.Distance), Locale.Declension(Strings.R.Members, supergroup.MemberCount));
            }
            else
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = Formatter.Distance(nearby.Distance);
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.SetChat(ViewModel.ClientService, chat, 36);

            button.CommandParameter = nearby;
            button.Command = ViewModel.OpenChatCommand;
        }
    }
}
