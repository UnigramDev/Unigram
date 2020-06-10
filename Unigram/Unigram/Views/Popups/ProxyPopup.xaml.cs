using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ProxyPopup : ContentPopup
    {
        public ProxyPopup()
        {
            InitializeComponent();
        }

        public ProxyPopup(ProxyViewModel proxy)
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
            }
        }

        public string Server
        {
            get
            {
                return FieldServer.Text ?? string.Empty;
            }
        }

        public int Port
        {
            get
            {
                int.TryParse(FieldPort.Text, out int port);
                return port;
            }
        }

        public ProxyType Type
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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(FieldServer.Text) /* || !IPAddress.TryParse(Server, out IPAddress server)*/)
            {
                VisualUtilities.ShakeView(FieldServer);
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrEmpty(FieldPort.Text) || !int.TryParse(FieldPort.Text, out int port))
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

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            var builder = new List<string>();
            if (Server != null)
            {
                builder.Add("server=" + Server);
            }
            if (Port != null)
            {
                builder.Add("port=" + Port);
            }
            //if (Username != null)
            //{
            //    builder.Add("user=" + Username);
            //}
            //if (Password != null)
            //{
            //    builder.Add("pass=" + Password);
            //}

            var title = Strings.Resources.ProxySettings;
            var link = new Uri(MeUrlPrefixConverter.Convert(TLContainer.Current.Resolve<IProtoService>(), $"socks?{string.Join("&", builder)}"));

            await SharePopup.GetForCurrentView().ShowAsync(link, title);
        }

        private void Type_Toggled(object sender, RoutedEventArgs e)
        {
            if (TypeSocksPanel != null)
            {
                TypeSocksPanel.Visibility = TypeSocks.IsChecked == true || TypeHttp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            if (TypeProtoPanel != null)
            {
                TypeProtoPanel.Visibility = TypeProto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            if (TypeHttpPanel != null)
            {
                TypeHttpPanel.Visibility = TypeHttp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            if (FieldPanel != null)
            {
                FieldPanel.Footer = TypeSocks.IsChecked == true
                    ? Strings.Resources.UseProxyInfo
                    : TypeProto.IsChecked == true
                    ? Strings.Resources.UseProxyTelegramInfo + Environment.NewLine + Environment.NewLine + Strings.Resources.UseProxyTelegramInfo2
                    : TypeHttp.IsChecked == true
                    ? "Enable if server supports transparent TCP connections via HTTP CONNECT method." + Environment.NewLine + Environment.NewLine + "When supported, it may improve connection speed dramatically. Try changing this option if this proxy doesn't work."
                    : String.Empty;
            }
        }
    }
}
