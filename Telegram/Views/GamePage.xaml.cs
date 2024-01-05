//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public class GameConfiguration
    {
        public GameConfiguration(MessageViewModel message, string url, string title, string username)
        {
            Message = message;
            Url = url;
            Title = title;
            Username = username;
        }

        public MessageViewModel Message { get; }

        public string Url { get; }

        public string Title { get; }

        public string Username { get; }
    }

    public sealed partial class GamePage : HostedPage
    {
        private MessageViewModel _shareMessage;

        public GamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is GameConfiguration configuration)
            {
                _shareMessage = configuration.Message;

                TitleLabel.Text = configuration.Title ?? string.Empty;
                UsernameLabel.Text = "@" + (configuration.Username ?? string.Empty);

                TitleLabel.Visibility = string.IsNullOrWhiteSpace(configuration.Title) ? Visibility.Collapsed : Visibility.Visible;
                UsernameLabel.Visibility = string.IsNullOrWhiteSpace(configuration.Username) ? Visibility.Collapsed : Visibility.Visible;

                View.Navigate(configuration.Url);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowPopupAsync(_shareMessage.ClientService.SessionId, typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(_shareMessage.ChatId, _shareMessage.Id));
        }

        private async void View_EventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            if (e.EventName == "share_game")
            {
                await this.ShowPopupAsync(_shareMessage.ClientService.SessionId, typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(_shareMessage.ChatId, _shareMessage.Id, false));
            }

            else if (e.EventName == "share_score")
            {
                await this.ShowPopupAsync(_shareMessage.ClientService.SessionId, typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(_shareMessage.ChatId, _shareMessage.Id, true));
            }
        }
    }
}
