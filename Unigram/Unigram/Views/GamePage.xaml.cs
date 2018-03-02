using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TdWindows;
using Template10.Services.SerializationService;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Core.Services;
using Unigram.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class GamePage : Page
    {
        private Message _shareMessage;

        public GamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var bundle = TLSerializationService.Current.Deserialize((string)e.Parameter) as TdBundle;
            if (bundle == null)
            {
                return;
            }

            bundle.TryGetValue("title", out string title);
            bundle.TryGetValue("username", out string username);

            bundle.TryGetValue("url", out string url);

            bundle.TryGetValue("message", out long messageId);
            bundle.TryGetValue("chat", out long chatId);

            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<string, string, string, TLMessage>(from);

            //    _shareMessage = tuple.Item4;

            TitleLabel.Text = title ?? string.Empty;
            UsernameLabel.Text = "@" + (username ?? string.Empty);

            TitleLabel.Visibility = string.IsNullOrWhiteSpace(title) ? Visibility.Collapsed : Visibility.Visible;
            UsernameLabel.Visibility = string.IsNullOrWhiteSpace(username) ? Visibility.Collapsed : Visibility.Visible;

            View.Navigate(new Uri(url));
            //}
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            //await ShareView.GetForCurrentView().ShowAsync(_shareMessage);
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramGameProxy(withMyScore =>
            {
                this.BeginOnUIThread(async () =>
                {
                    //await ShareView.GetForCurrentView().ShowAsync(_shareMessage, withMyScore);
                });
            }));
        }
    }
}
