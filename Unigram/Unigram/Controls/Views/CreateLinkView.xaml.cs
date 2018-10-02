using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Unigram.Common;

namespace Unigram.Controls.Views
{
    public sealed partial class CreateLinkView : ContentDialog
    {
        public CreateLinkView()
        {
            this.InitializeComponent();

            Title = Strings.Resources.CreateLink;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public string Text
        {
            get
            {
                return TextField.Text;
            }
            set
            {
                TextField.Text = value;
            }
        }

        public string Link
        {
            get
            {
                return LinkField.Text;
            }
            set
            {
                LinkField.Text = value;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(Text))
            {
                VisualUtilities.ShakeView(TextField);
                args.Cancel = true;
                return;
            }

            if (!Uri.TryCreate(Link, UriKind.Absolute, out Uri result))
            {
                VisualUtilities.ShakeView(LinkField);
                args.Cancel = true;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
