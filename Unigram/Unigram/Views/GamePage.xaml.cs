using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
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
        private TLMessage _shareMessage;

        public GamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var buffer = TLSerializationService.Current.Deserialize((string)e.Parameter) as byte[];
            if (buffer == null)
            {
                return;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<string, string, string, TLMessage>(from);

                _shareMessage = tuple.Item4;

                TitleLabel.Text = tuple.Item1;
                UsernameLabel.Text = "@" + tuple.Item2;

                TitleLabel.Visibility = string.IsNullOrWhiteSpace(tuple.Item1) ? Visibility.Collapsed : Visibility.Visible;
                UsernameLabel.Visibility = string.IsNullOrWhiteSpace(tuple.Item2) ? Visibility.Collapsed : Visibility.Visible;

                View.Navigate(new Uri(tuple.Item3));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            await ShareView.Current.ShowAsync(_shareMessage);
        }

        private void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramGameProxy(withMyScore =>
            {
                this.BeginOnUIThread(async () =>
                {
                    await ShareView.Current.ShowAsync(_shareMessage, withMyScore);
                });
            }));
        }
    }
}
