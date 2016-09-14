using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages
{
    public sealed partial class UserMessageControl : MessageControlBase
    {
        public UserMessageControl()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Bindings.Update();
                }
            };
        }

        private void mfbtnCopy_Click(object sender, RoutedEventArgs e)
        {
            // Create datapackage with the copy-operation
            DataPackage copyContent = new DataPackage();
            copyContent.RequestedOperation = DataPackageOperation.Copy;

            // Now we just have to set the content and copy this to the clipboard! :)
            StringBuilder copyText = new StringBuilder();

            copyText.AppendLine(ViewModel.Message);

            // Trim off the last space/enter in case there were multiple lines copied
            copyContent.SetText(copyText.ToString().TrimEnd());
            Clipboard.SetContent(copyContent);
        }
    }
}
