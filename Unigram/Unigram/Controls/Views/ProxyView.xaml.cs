using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

        public string Server
        {
            get
            {
                return FieldServer.Text;
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
                return FieldPort.Text;
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
                return FieldUsername.Text;
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
                return FieldPassword.Password;
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
    }
}
