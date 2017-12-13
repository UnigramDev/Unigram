using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Strings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class ProxyView : ContentDialog
    {
        public ProxyView()
        {
            InitializeComponent();
            ShareButton.Visibility = SettingsHelper.IsAuthorized ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsProxyEnabled
        {
            get
            {
                return FieldEnabled.IsChecked == true;
            }
            set
            {
                FieldEnabled.IsChecked = value;
            }
        }

        public bool IsCallsProxyEnabled
        {
            get
            {
                return FieldCalls.IsChecked == true;
            }
            set
            {
                FieldCalls.IsChecked = value;
            }
        }

        public string Server
        {
            get
            {
                return string.IsNullOrWhiteSpace(FieldServer.Text) ? null : FieldServer.Text;
            }
            set
            {
                FieldServer.Text = value ?? string.Empty;
            }
        }

        public string Port
        {
            get
            {
                return string.IsNullOrWhiteSpace(FieldPort.Text) ? null : FieldPort.Text;
            }
            set
            {
                FieldPort.Text = value ?? string.Empty;
            }
        }

        public string Username
        {
            get
            {
                return string.IsNullOrWhiteSpace(FieldUsername.Text) ? null : FieldUsername.Text;
            }
            set
            {
                FieldUsername.Text = value ?? string.Empty;
            }
        }

        public string Password
        {
            get
            {
                return string.IsNullOrWhiteSpace(FieldPassword.Password) ? null : FieldPassword.Password;
            }
            set
            {
                FieldPassword.Password = value ?? string.Empty;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsProxyEnabled)
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
            if (Username != null)
            {
                builder.Add("user=" + Username);
            }
            if (Password != null)
            {
                builder.Add("pass=" + Password);
            }

            var title = Strings.Android.ProxySettings;
            var link = new Uri(MeUrlPrefixConverter.Convert($"socks?{string.Join("&", builder)}"));

            await ShareView.Current.ShowAsync(link, title);
        }

        private void Enable_Toggled(object sender, RoutedEventArgs e)
        {
            FieldCalls.IsEnabled = FieldEnabled.IsChecked == true;
            FieldCalls.IsChecked = false;
        }
    }
}
