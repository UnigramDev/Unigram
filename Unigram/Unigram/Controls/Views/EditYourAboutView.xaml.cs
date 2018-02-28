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

namespace Unigram.Controls.Views
{
    public sealed partial class EditYourAboutView : ContentDialog
    {
        public EditYourAboutView(string bio)
        {
            this.InitializeComponent();

            FieldAbout.Text = bio;

            Title = Strings.Android.UserBio;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
        }

        public string About
        {
            get
            {
                return FieldAbout.Text;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
