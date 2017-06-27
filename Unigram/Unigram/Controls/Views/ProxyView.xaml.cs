using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Services.Cache;
using Unigram.Common;
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
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested"))
            {
                DataTransferManager.GetForCurrentView().ShareProvidersRequested -= OnShareProvidersRequested;
                DataTransferManager.GetForCurrentView().ShareProvidersRequested += OnShareProvidersRequested;
            }

            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
            DataTransferManager.GetForCurrentView().DataRequested += OnDataRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsEventPresent("Windows.ApplicationModel.DataTransfer.DataTransferManager", "ShareProvidersRequested"))
            {
                DataTransferManager.GetForCurrentView().ShareProvidersRequested -= OnShareProvidersRequested;
            }

            DataTransferManager.GetForCurrentView().DataRequested -= OnDataRequested;
        }

        private void OnShareProvidersRequested(DataTransferManager sender, ShareProvidersRequestedEventArgs args)
        {
            if (args.Data.Contains(StandardDataFormats.WebLink))
            {
                var icon = RandomAccessStreamReference.CreateFromUri(new Uri(@"ms-appx:///Assets/Images/ShareProvider_CopyLink24x24.png"));
                var provider = new ShareProvider("Copy link", icon, (Color)App.Current.Resources["SystemAccentColor"], OnShareToClipboard);
                args.Providers.Add(provider);
            }
        }

        private async void OnShareToClipboard(ShareProviderOperation operation)
        {
            var webLink = await operation.Data.GetWebLinkAsync();
            var package = new DataPackage();
            package.SetText(webLink.ToString());

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Clipboard.SetContent(package);
                operation.ReportCompleted();
            });
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var config = InMemoryCacheService.Current.GetConfig();
            if (config == null)
            {
                return;
            }

            var linkPrefix = config.MeUrlPrefix;
            if (linkPrefix.EndsWith("/"))
            {
                linkPrefix = linkPrefix.Substring(0, linkPrefix.Length - 1);
            }
            if (linkPrefix.StartsWith("https://"))
            {
                linkPrefix = linkPrefix.Substring(8);
            }
            else if (linkPrefix.StartsWith("http://"))
            {
                linkPrefix = linkPrefix.Substring(7);
            }

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

            var package = args.Request.Data;
            package.Properties.Title = "Proxy Settings";
            package.SetText($"https://{linkPrefix}/socks?{string.Join("&", builder)}");
            package.SetWebLink(new Uri($"https://{linkPrefix}/socks?{string.Join("&", builder)}"));
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
            if (string.IsNullOrEmpty(Server) || !IPAddress.TryParse(Server, out IPAddress server))
            {
                VisualUtilities.ShakeView(FieldServer);
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrEmpty(Port) || !int.TryParse(Port, out int port))
            {
                VisualUtilities.ShakeView(FieldPort);
                args.Cancel = true;
                return;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }
    }
}
