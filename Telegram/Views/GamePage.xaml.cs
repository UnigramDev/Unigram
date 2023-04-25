//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Data.Json;
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

            InitializeWebView(url);
            //}
        }

        private async void InitializeWebView(string url)
        {
            await View.EnsureCoreWebView2Async();
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"window.external={invoke:s=>window.chrome.webview.postMessage(s)}");
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
window.TelegramWebviewProxy = {
postEvent: function(eventType, eventData) {
	if (window.external && window.external.invoke) {
		window.external.invoke(JSON.stringify([eventType, eventData]));
	}
}
}");

            View.CoreWebView2.Navigate(url);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            await SharePopup.GetForCurrentView().ShowAsync(_shareMessage);
        }

        private void View_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = args.TryGetWebMessageAsString();

            if (JsonArray.TryParse(json, out JsonArray message))
            {
                var eventName = message.GetStringAt(0);
                var eventData = message.GetStringAt(1);

                if (JsonObject.TryParse(eventData, out JsonObject data))
                {
                    ReceiveEvent(eventName, data);
                }
            }
        }

        private async void ReceiveEvent(string eventName, JsonObject data)
        {
            if (eventName == "share_game")
            {
                await SharePopup.GetForCurrentView().ShowAsync(_shareMessage, false);
            }

            else if (eventName == "share_score")
            {
                await SharePopup.GetForCurrentView().ShowAsync(_shareMessage, true);
            }
        }
    }
}
