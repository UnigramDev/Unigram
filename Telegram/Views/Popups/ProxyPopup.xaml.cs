//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ProxyPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public ProxyPopup(IClientService clientService)
        {
            InitializeComponent();

            _clientService = clientService;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        public ProxyPopup(IClientService clientService, ProxyViewModel proxy)
            : this(clientService)
        {
            InitializeComponent();

            FieldServer.Text = proxy.Server;
            FieldPort.Text = proxy.Port.ToString();

            switch (proxy.Type)
            {
                case ProxyTypeSocks5 socks:
                    FieldUsername.Text = socks.Username;
                    FieldPassword.Password = socks.Password;
                    TypeSocks.IsChecked = true;
                    break;
                case ProxyTypeMtproto proto:
                    FieldSecret.Text = proto.Secret;
                    TypeProto.IsChecked = true;
                    break;
                case ProxyTypeHttp http:
                    FieldUsername.Text = http.Username;
                    FieldPassword.Password = http.Password;
                    FieldTransparent.IsChecked = !http.HttpOnly;
                    TypeHttp.IsChecked = true;
                    break;
                case ProxyTypeWeb web:
                    // A web proxy is entered and shown as one address, host or host/path
                    FieldServer.Text = string.IsNullOrEmpty(web.Path) ? proxy.Server : proxy.Server + "/" + web.Path;
                    FieldSecret.Text = web.Secret;
                    TypeWeb.IsChecked = true;
                    break;
            }
        }

        // The canonical form TDLib produced for a web proxy, set once the address has been validated
        private Proxy _webProxy;

        public Proxy Proxy => TypeWeb.IsChecked == true ? _webProxy : new Proxy(Server, Port, Type);

        private string Server => FieldServer.Text ?? string.Empty;

        private int Port
        {
            get
            {
                int.TryParse(FieldPort.Text, out int port);
                return port;
            }
        }

        private ProxyType Type
        {
            get
            {
                if (TypeSocks.IsChecked == true)
                {
                    return new ProxyTypeSocks5(FieldUsername.Text ?? string.Empty, FieldPassword.Password ?? string.Empty);
                }
                else if (TypeProto.IsChecked == true)
                {
                    return new ProxyTypeMtproto(FieldSecret.Text ?? string.Empty);
                }
                else if (TypeHttp.IsChecked == true)
                {
                    return new ProxyTypeHttp(FieldUsername.Text ?? string.Empty, FieldPassword.Password ?? string.Empty, FieldTransparent.IsChecked == false);
                }

                return null;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(FieldServer.Text) /* || !IPAddress.TryParse(Server, out IPAddress server)*/)
            {
                VisualUtilities.ShakeView(FieldServer);
                args.Cancel = true;
                return;
            }

            if (TypeWeb.IsChecked == true)
            {
                if (string.IsNullOrEmpty(FieldSecret.Text))
                {
                    VisualUtilities.ShakeView(FieldSecret);
                    args.Cancel = true;
                    return;
                }

                var deferral = args.GetDeferral();

                _webProxy = await NormalizeWebProxyAsync();
                if (_webProxy == null)
                {
                    VisualUtilities.ShakeView(FieldServer);
                    VisualUtilities.ShakeView(FieldSecret);
                    args.Cancel = true;
                }

                deferral.Complete();
                return;
            }

            if (string.IsNullOrEmpty(FieldPort.Text) || !int.TryParse(FieldPort.Text, out _))
            {
                VisualUtilities.ShakeView(FieldPort);
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrEmpty(FieldSecret.Text) && TypeProto.IsChecked == true)
            {
                VisualUtilities.ShakeView(FieldSecret);
                args.Cancel = true;
                return;
            }
        }

        // TDLib owns the address rules (IDNA, IP literals, path segments, fake TLS secrets), so it validates the
        // entry by turning it into a link and parsing that back, which also yields the canonical host and secret.
        private async Task<Proxy> NormalizeWebProxyAsync()
        {
            var address = FieldServer.Text.Trim();
            if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                address = address.Substring("https://".Length);
            }

            var index = address.IndexOf('/');
            var host = index < 0 ? address : address.Substring(0, index);
            var path = index < 0 ? string.Empty : address.Substring(index + 1);

            var proxy = new Proxy(host, 443, new ProxyTypeWeb(path, FieldSecret.Text.Trim()));

            var link = await _clientService.SendAsync(new GetInternalLink(new InternalLinkTypeProxy(proxy), true)) as HttpUrl;
            if (link == null)
            {
                return null;
            }

            var type = await _clientService.SendAsync(new GetInternalLinkType(link.Url)) as InternalLinkTypeProxy;
            return type?.Proxy?.Type is ProxyTypeWeb ? type.Proxy : null;
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var builder = new List<string>();
            if (Server != null)
            {
                builder.Add("server=" + Server);
            }

            builder.Add("port=" + Port);

            //if (Username != null)
            //{
            //    builder.Add("user=" + Username);
            //}
            //if (Password != null)
            //{
            //    builder.Add("pass=" + Password);
            //}

            var title = Strings.ProxySettings;
            var link = new Uri(MeUrlPrefixConverter.Convert(_clientService, $"socks?{string.Join("&", builder)}"));

            // TODO: currently not used
            //await new ChooseChatsPopup().ShowAsync(link, title);
        }

        private void Type_Toggled(object sender, RoutedEventArgs e)
        {
            TypeSocksPanel?.Visibility = TypeSocks.IsChecked == true || TypeHttp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            TypeProtoPanel?.Visibility = TypeProto.IsChecked == true || TypeWeb?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            TypeHttpPanel?.Visibility = TypeHttp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // A web proxy is always reached over HTTPS on 443
            FieldPort?.Visibility = TypeWeb?.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

            FieldPanel?.Text = TypeSocks.IsChecked == true
                ? Strings.UseProxyInfo
                : TypeProto.IsChecked == true
                ? Strings.UseProxyTelegramInfo + Environment.NewLine + Environment.NewLine + Strings.UseProxyTelegramInfo2
                : TypeHttp.IsChecked == true
                ? Strings.TransparentTcpConnectionInfo + Environment.NewLine + Environment.NewLine + Strings.TransparentTcpConnectionInfo2
                : TypeWeb?.IsChecked == true
                ? Strings.UseProxyWebWarning
                : string.Empty;
        }
    }
}
