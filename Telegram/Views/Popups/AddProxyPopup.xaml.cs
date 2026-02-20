//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Popups
{
    public sealed partial class AddProxyPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly Proxy _proxy;

        public AddProxyPopup(IClientService clientService, INavigationService navigationService, Proxy proxy)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _proxy = proxy;

            Title = Strings.UseProxyTitle;
            PrimaryButtonText = Strings.ConnectingConnectProxy;

            Server.Content = proxy.Server;
            Port.Content = proxy.Port;

            if (proxy.Type is ProxyTypeHttp http)
            {
                if (string.IsNullOrEmpty(http.Username))
                {
                    Username.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Username.Content = http.Username;
                }

                if (string.IsNullOrEmpty(http.Password))
                {
                    Password.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Password.Content = http.Password;
                }

                Secret.Visibility = Visibility.Collapsed;
            }
            else if (proxy.Type is ProxyTypeSocks5 socks5)
            {
                if (string.IsNullOrEmpty(socks5.Username))
                {
                    Username.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Username.Content = socks5.Username;
                }

                if (string.IsNullOrEmpty(socks5.Password))
                {
                    Password.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Password.Content = socks5.Password;
                }

                Secret.Visibility = Visibility.Collapsed;
            }
            else if (proxy.Type is ProxyTypeMtproto mtproto)
            {
                if (string.IsNullOrEmpty(mtproto.Secret))
                {
                    Secret.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Secret.Content = mtproto.Secret;
                }

                Username.Visibility = Visibility.Collapsed;
                Password.Visibility = Visibility.Collapsed;

                SponsorInfo.Visibility = Visibility.Visible;

                //secretText = !string.IsNullOrEmpty(mtproto.Secret) ? $"{Strings.UseProxySecret}: {mtproto.Secret}\n" : string.Empty;
                //secretInfo = !string.IsNullOrEmpty(mtproto.Secret) ? $"\n\n{Strings.UseProxyTelegramInfo2}" : string.Empty;
            }

            var hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(Strings.ProxyBottomSheetCheckStatus);
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Click += CheckStatus_Click;

            StatusInfo.Inlines.Add(hyperlink);
        }

        private async void CheckStatus_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var confirm = await _navigationService.ShowPopupAsync(Strings.ProxyBottomSheetCheckWarningText, Strings.ProxyBottomSheetCheckWarning, Strings.Proceed, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                StatusInfo.Inlines.Clear();
                StatusInfo.Text = Strings.ProxyBottomSheetChecking;

                var response = await _clientService.SendAsync(new PingProxy(_proxy));
                if (response is Seconds seconds)
                {
                    if (seconds.SecondsValue != 0)
                    {
                        StatusInfo.Text = string.Format(Strings.Ping, Math.Truncate(seconds.SecondsValue * 1000));
                    }
                    else
                    {
                        StatusInfo.Text = Strings.Available;
                    }
                }
                else
                {
                    StatusInfo.Text = Strings.Unavailable;
                }
            }
        }
    }
}
