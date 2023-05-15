//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChatJoinRequestsPopup : ContentPopup
    {
        public ChatJoinRequestsViewModel ViewModel => DataContext as ChatJoinRequestsViewModel;

        public ChatJoinRequestsPopup(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, Chat chat, string inviteLink)
        {
            InitializeComponent();
            DataContext = new ChatJoinRequestsViewModel(chat, inviteLink, clientService, settingsService, aggregator);

            Title = Strings.MemberRequests;
            PrimaryButtonText = Strings.Close;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var request = args.Item as ChatJoinRequest;

            var user = ViewModel.ClientService.GetUser(request.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();

                var stack = content.Children[4] as StackPanel;
                var primary = stack.Children[0] as Button;
                var secondary = stack.Children[1] as HyperlinkButton;

                primary.CommandParameter = request;
                primary.Command = ViewModel.AcceptCommand;

                secondary.CommandParameter = request;
                secondary.Command = ViewModel.DismissCommand;

                primary.Content = ViewModel.IsChannel
                    ? Strings.AddToChannel
                    : Strings.AddToGroup;
            }
            else if (args.Phase == 1)
            {
                var time = content.Children[2] as TextBlock;
                time.Text = Formatter.DateExtended(request.Date);

                if (string.IsNullOrEmpty(request.Bio))
                {
                    var subtitle = content.Children[3] as TextBlock;
                    subtitle.Visibility = Visibility.Collapsed;

                    Grid.SetRow(content.Children[4] as StackPanel, 1);
                }
                else
                {
                    var subtitle = content.Children[3] as TextBlock;
                    subtitle.Text = request.Bio;
                    subtitle.Visibility = Visibility.Visible;

                    Grid.SetRow(content.Children[4] as StackPanel, 2);
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
