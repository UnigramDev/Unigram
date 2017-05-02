using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Services.SerializationService;
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
        public GamePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var args = SerializationService.Json.Deserialize<NavigationParameters>((string)e.Parameter);
            if (args != null)
            {
                TitleLabel.Text = args.Title;
                UsernameLabel.Text = "@" + args.Username;

                TitleLabel.Visibility = string.IsNullOrWhiteSpace(args.Title) ? Visibility.Collapsed : Visibility.Visible;
                UsernameLabel.Visibility = string.IsNullOrWhiteSpace(args.Username) ? Visibility.Collapsed : Visibility.Visible;

                View.Navigate(new Uri(args.Url));
            }
        }

        public class NavigationParameters
        {
            public string Url { get; set; }

            public string Title { get; set; }

            public string Username { get; set; }
        }
    }
}
