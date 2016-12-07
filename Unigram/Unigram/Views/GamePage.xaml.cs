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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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
                tblDialogName.Text = args.Title;
                tblDialogStatus.Text = "@" + args.Username;

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
