using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatStatisticsPage : Page
    {
        private readonly IProtoService _protoService;
        private long _chatId;

        public ChatStatisticsPage()
        {
            InitializeComponent();
            _protoService = TLContainer.Current.Resolve<IProtoService>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameter = TLSerializationService.Current.Deserialize((string)e.Parameter);
            if (parameter is long chatId)
            {
                _chatId = chatId;

                var response = await _protoService.SendAsync(new GetChatStatisticsUrl(chatId, string.Empty));
                if (response is HttpUrl url && Uri.TryCreate(url.Url, UriKind.Absolute, out Uri uri))
                {
                    View.Navigate(uri);
                }
            }
        }

        private async void View_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            if (string.Equals(args.Uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase))
            {
                args.Cancel = true;

                await ProcessQueryAsync(args.Uri);
            }
        }

        private async void View_UnsupportedUriSchemeIdentified(WebView sender, WebViewUnsupportedUriSchemeIdentifiedEventArgs args)
        {
            if (string.Equals(args.Uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase))
            {
                args.Handled = true;

                await ProcessQueryAsync(args.Uri);
            }
        }

        private async Task ProcessQueryAsync(Uri scheme)
        {
            var query = scheme.Query.ParseQueryString();
            if (query.TryGetValue("params", out string parameters))
            {
                var response = await _protoService.SendAsync(new GetChatStatisticsUrl(_chatId, string.Empty));
                if (response is HttpUrl url && Uri.TryCreate(url.Url, UriKind.Absolute, out Uri uri))
                {
                    View.Navigate(uri);
                }
            }
        }
    }
}
