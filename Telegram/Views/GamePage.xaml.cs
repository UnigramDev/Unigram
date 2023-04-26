//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class GamePage : HostedPage
    {
        private Message _shareMessage;

        public GamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var bundle = e.Parameter as Dictionary<string, object>;
            if (bundle == null)
            {
                return;
            }

            bundle.TryGet("title", out string title);
            bundle.TryGet("username", out string username);

            bundle.TryGet("url", out string url);

            bundle.TryGet("message", out long messageId);
            bundle.TryGet("chat", out long chatId);

            _shareMessage = new Message { ChatId = chatId, Id = messageId };

            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<string, string, string, TLMessage>(from);

            //    _shareMessage = tuple.Item4;

            TitleLabel.Text = title ?? string.Empty;
            UsernameLabel.Text = "@" + (username ?? string.Empty);

            TitleLabel.Visibility = string.IsNullOrWhiteSpace(title) ? Visibility.Collapsed : Visibility.Visible;
            UsernameLabel.Visibility = string.IsNullOrWhiteSpace(username) ? Visibility.Collapsed : Visibility.Visible;

            View.Navigate(url);
            //}
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            await SharePopup.GetForCurrentView().ShowAsync(_shareMessage);
        }

        private async void View_EventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            if (e.EventName == "share_game")
            {
                await SharePopup.GetForCurrentView().ShowAsync(_shareMessage, false);
            }

            else if (e.EventName == "share_score")
            {
                await SharePopup.GetForCurrentView().ShowAsync(_shareMessage, true);
            }
        }
    }
}
